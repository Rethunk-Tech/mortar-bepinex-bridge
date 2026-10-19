using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;

namespace MortarBepInExBridge;

/// <summary>A game's own command console. Run is called on Unity's main thread only.</summary>
internal interface IGameConsole
{
    /// <summary>Runs one console line; null when it was handed to the game, else why it was not.</summary>
    string? Run(string line);
}

/// <summary>Reaches a console that is a method on a static instance (Valheim: <c>Console.instance.TryRunCommand(line)</c>)
/// by name, so the plugin references no game assembly. A method's other parameters take their declared defaults.</summary>
internal sealed class ReflectionConsole(Func<Type?> type, string instanceMember, string method) : IGameConsole
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    public string? Run(string line)
    {
        Type? t = type();
        if (t == null)
            return "this game's console is not loaded";
        object? target = Overlay.Probe.Static(t, instanceMember);
        if (target == null)
            return "the game's console is not open yet";
        foreach (MethodInfo m in target.GetType().GetMethods(Any))
        {
            ParameterInfo[] ps = m.GetParameters();
            if (m.Name != method || ps.Length == 0 || ps[0].ParameterType != typeof(string))
                continue;
            var args = new object?[ps.Length];
            args[0] = line;
            for (int i = 1; i < ps.Length; i++)
            {
                if (!ps[i].HasDefaultValue)
                    goto next;
                args[i] = ps[i].DefaultValue;
            }
            try
            {
                m.Invoke(m.IsStatic ? null : target, args);
                return null;
            }
            catch (TargetInvocationException ex)
            {
                return (ex.InnerException ?? ex).Message;
            }
            next:;
        }
        return $"the game's console has no {method}(string) method";
    }
}

/// <summary>Hands work from the socket threads to Unity's main thread and waits for its result.</summary>
internal sealed class MainThreadQueue
{
    private sealed class Job(Func<string?> work)
    {
        public readonly Func<string?> Work = work;
        public readonly ManualResetEventSlim Done = new(false);
        public string? Result;
    }

    private readonly ConcurrentQueue<Job> Pending = new();

    /// <summary>Runs work on the main thread; the result is null on success, else a message. A queue nobody drains times out.</summary>
    public string? Invoke(Func<string?> work, int timeoutMs)
    {
        var job = new Job(work);
        this.Pending.Enqueue(job);
        return job.Done.Wait(timeoutMs) ? job.Result : "the game did not run the command in time";
    }

    /// <summary>Runs what is queued; the main thread calls it every frame.</summary>
    public void Drain()
    {
        while (this.Pending.TryDequeue(out Job? job))
        {
            try
            {
                job.Result = job.Work();
            }
            catch (Exception ex)
            {
                job.Result = ex.Message;
            }
            job.Done.Set();
        }
    }
}

/// <summary>Drains the queue each frame from an object of its own, as <see cref="IntroRunner"/> does, because the plugin's object is not always kept alive.</summary>
internal sealed class ConsoleRunner : MonoBehaviour
{
    private MainThreadQueue? queue;

    public static void Start(MainThreadQueue queue, ManualLogSource log)
    {
        var host = new GameObject("MortarBridgeConsole") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        host.AddComponent<ConsoleRunner>().queue = queue;
        log.LogInfo("Console passthrough is on.");
    }

    private void Update() => this.queue?.Drain();
}
