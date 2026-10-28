using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Profiling;

namespace MortarBepInExBridge.Perf;

/// <summary>
/// In-game performance: frame times, memory and each plugin's main-thread time per frame, from the first frame on a
/// launch Mortar measures and from the first <c>perf start</c> on any other. That time is its Harmony prefixes, postfixes and
/// finalizers (each patch method is itself wrapped with a timer) and its own MonoBehaviours' Update, LateUpdate and
/// FixedUpdate. It runs on an object of its own that scene loads leave alone, since Lethal Company's first scene load
/// destroys the plugin's object. Unity is main-thread only, so the plugin creates it idle in Awake on every launch and
/// the socket threads only flip <see cref="PerfSwitch"/> and read the summary the main thread rebuilds once a second.
/// </summary>
internal sealed class PerfHost : MonoBehaviour
{
    private const string HarmonyId = Plugin.Guid + ".perf";
    private static readonly string[] UpdateMethods = ["Update", "LateUpdate", "FixedUpdate"];

    private static readonly PerfSwitch perf = new();
    private static int mainThread;
    private static CostTracker costs = new(0);
    private static Dictionary<MethodBase, int> pluginOf = [];
    private static List<string> startupPlugins = [];
    // Where the startup patcher looks for the shares (StartupPatcher.SharesKey), which it cannot reference.
    private const string SharesKey = "MortarBepInExBridge.StartupShares";

    private readonly FrameStats frames = new();
    private ManualLogSource? log;
    private List<string> plugins = [];
    private PatchGraph? graph;
    private int collectionsAtStart;
    private float nextSummary;
    private readonly Queue<MethodBase> pending = new();
    private int wrapped;
    private int failed;
    private Harmony? harmony;
    private HarmonyMethod? enter;
    private HarmonyMethod? exit;
    private long wrapTicks;
    // Wrapping hundreds of methods in one frame froze loading under a large pack; each frame gets a slice and the whole
    // job a ceiling, after which measuring carries on with what it has rather than hold the game back.
    private const double SliceMs = 8;
    private const double CeilingMs = 20_000;

    public static void Start(ManualLogSource log, bool measuredLaunch)
    {
        var host = new GameObject("MortarBridgePerf") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        host.AddComponent<PerfHost>().log = log;
        mainThread = Thread.CurrentThread.ManagedThreadId;
        if (!measuredLaunch)
            return;
        // The measured launch times plugins from the first frame, so startup has shares without a perf start. An
        // on-demand start never registers them: its window does not begin at the first frame.
        perf.Start();
        AppDomain.CurrentDomain.SetData(SharesKey, (Func<Dictionary<string, long>>)StartupShares);
    }

    /// <summary>Each plugin's main-thread milliseconds, by GUID, since the first frame: its patch methods and its own
    /// Update loops, exclusive of the plugins they called into.</summary>
    private static Dictionary<string, long> StartupShares()
    {
        var shares = new Dictionary<string, long>();
        for (int i = 0; i < startupPlugins.Count; i++)
            shares[startupPlugins[i]] = costs.Running(i) * 1000 / Stopwatch.Frequency;
        return shares;
    }

    public static string Reply(bool start) => perf.Reply(start);

    private void Update()
    {
        try
        {
            if (!perf.Active)
                return;
            if (perf.TakeRestart())
            {
                if (this.graph == null)
                    this.Instrument();
                this.frames.Reset();
                costs.Reset();
                this.collectionsAtStart = GC.CollectionCount(0);
            }
            if (this.pending.Count > 0)
            {
                this.WrapSome();
                // The window starts once everything is wrapped, so the wrapping frames do not skew it.
                if (this.pending.Count == 0)
                {
                    this.frames.Reset();
                    costs.Reset();
                    this.collectionsAtStart = GC.CollectionCount(0);
                }
                return;
            }
            this.frames.Add(Time.unscaledDeltaTime * 1000);
            costs.EndFrame();
            if (Time.unscaledTime >= this.nextSummary)
            {
                this.nextSummary = Time.unscaledTime + 1;
                var memory = new Memory(Profiler.GetMonoUsedSizeLong(), Profiler.GetMonoHeapSizeLong(), GC.CollectionCount(0) - this.collectionsAtStart);
                perf.Summary = PerfReport.Json(this.frames, costs, this.plugins, this.graph, memory, Stopwatch.Frequency);
            }
        }
        catch (Exception ex)
        {
            // A measuring failure must never reach the game.
            perf.Fail(ex.Message);
            this.enabled = false;
            this.log?.LogWarning($"In-game measuring is off for this launch: {ex.Message}");
        }
    }

    private void Instrument()
    {
        Dictionary<Assembly, string> assemblies = [];
        foreach (PluginInfo info in Chainloader.PluginInfos.Values)
        {
            // Compared as an object: a plugin whose GameObject a scene load destroyed is still its assembly's plugin.
            if ((object?)info.Instance is { } plugin && info.Metadata.GUID != Plugin.Guid)
                assemblies[plugin.GetType().Assembly] = info.Metadata.GUID;
        }
        this.graph = PatchGraph.Build(Edges(), HarmonyId, a => assemblies.TryGetValue(a, out string? guid) ? guid : null);
        List<(MethodInfo Method, string Plugin)> timed = [.. this.graph.Timed];
        foreach (KeyValuePair<Assembly, string> plugin in assemblies)
        {
            timed.AddRange(LoadableTypes.Collect(TypesOf(plugin.Key), UpdatesOf, out int skipped).Select(m => (m, plugin.Value)));
            if (skipped > 0)
                this.log?.LogWarning($"Measuring skips {skipped} types of {plugin.Value} that could not be loaded.");
        }

        this.plugins = [.. timed.Select(t => t.Plugin).Concat(this.graph.Patches.Keys).Distinct()];
        Dictionary<string, int> index = this.plugins.Select((guid, i) => (guid, i)).ToDictionary(p => p.guid, p => p.i);
        startupPlugins = this.plugins;
        costs = new CostTracker(this.plugins.Count);
        Dictionary<MethodBase, int> owners = [];
        foreach ((MethodInfo method, string plugin) in timed)
            owners.TryAdd(method, index[plugin]);
        pluginOf = owners;
        this.harmony = new Harmony(HarmonyId);
        this.enter = new HarmonyMethod(typeof(PerfHost).GetMethod(nameof(Enter), BindingFlags.Static | BindingFlags.NonPublic));
        this.exit = new HarmonyMethod(typeof(PerfHost).GetMethod(nameof(Exit), BindingFlags.Static | BindingFlags.NonPublic));
        foreach (MethodBase method in owners.Keys)
            this.pending.Enqueue(method);
    }

    private void WrapSome()
    {
        long start = Stopwatch.GetTimestamp();
        while (this.pending.Count > 0 && Elapsed(start) < SliceMs)
        {
            if (this.wrapTicks + (Stopwatch.GetTimestamp() - start) > CeilingMs * Stopwatch.Frequency / 1000)
            {
                this.log?.LogWarning($"Measuring stopped wrapping with {this.pending.Count} methods left: it ran past its time limit.");
                this.pending.Clear();
                break;
            }
            try
            {
                this.harmony!.Patch(this.pending.Dequeue(), prefix: this.enter, finalizer: this.exit);
                this.wrapped++;
            }
            catch (Exception)
            {
                this.failed++;
            }
        }
        this.wrapTicks += Stopwatch.GetTimestamp() - start;
        if (this.pending.Count == 0)
            this.log?.LogInfo($"Measuring {this.plugins.Count} plugins: {this.wrapped} methods timed, {this.failed} could not be wrapped.");
    }

    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

    private static IEnumerable<PatchEdge> Edges()
    {
        foreach (MethodBase target in Harmony.GetAllPatchedMethods().ToList())
        {
            HarmonyLib.Patches? info = Harmony.GetPatchInfo(target);
            if (info == null)
                continue;
            foreach (Patch p in info.Prefixes)
                yield return new PatchEdge(target, p.owner, p.PatchMethod);
            foreach (Patch p in info.Postfixes)
                yield return new PatchEdge(target, p.owner, p.PatchMethod);
            foreach (Patch p in info.Finalizers)
                yield return new PatchEdge(target, p.owner, p.PatchMethod);
            foreach (Patch p in info.Transpilers)
                yield return new PatchEdge(target, p.owner, p.PatchMethod, transpiler: true);
        }
    }

    private static Type?[] TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types;
        }
    }

    private static IEnumerable<MethodInfo> UpdatesOf(Type type)
    {
        if (type.ContainsGenericParameters || !typeof(MonoBehaviour).IsAssignableFrom(type))
            yield break;
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (string name in UpdateMethods)
        {
            if (type.GetMethod(name, declared, null, Type.EmptyTypes, null) is { IsAbstract: false } method)
                yield return method;
        }
    }

    private static void Enter(out long __state) =>
        __state = Thread.CurrentThread.ManagedThreadId == mainThread && costs.Enter() ? Stopwatch.GetTimestamp() : -1;

    private static void Exit(MethodBase __originalMethod, long __state)
    {
        if (__state >= 0)
            costs.Exit(pluginOf.TryGetValue(__originalMethod, out int plugin) ? plugin : -1, Stopwatch.GetTimestamp() - __state);
    }
}

/// <summary>Reflection over a plugin's types that tolerates the ones the game's runtime cannot load.</summary>
internal static class LoadableTypes
{
    /// <summary>The Update loops of each type, skipping (and counting) a type the runtime cannot load, as a mod's
    /// compatibility class for another mod that is not installed: one such type must not stop measuring.</summary>
    public static List<MethodInfo> Collect(IEnumerable<Type?> types, Func<Type, IEnumerable<MethodInfo>> updatesOf, out int skipped)
    {
        List<MethodInfo> methods = [];
        skipped = 0;
        foreach (Type? type in types)
        {
            if (type == null)
            {
                skipped++;
                continue;
            }
            try
            {
                methods.AddRange(updatesOf(type));
            }
            catch (Exception ex) when (ex is TypeLoadException or MissingMemberException or FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                skipped++;
            }
        }
        return methods;
    }
}
