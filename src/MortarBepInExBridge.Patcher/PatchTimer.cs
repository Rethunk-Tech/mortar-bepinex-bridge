using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace MortarBepInExBridge;

/// <summary>
/// Time spent applying Harmony patches, charged to the assembly that asked for them. Patching nests (PatchAll calls
/// the class processor, which calls the patch processor), so only the outermost call on a thread is timed.
/// </summary>
internal sealed class PatchTimer
{
    private static readonly string[] SkippedPrefixes = ["0Harmony", "MonoMod", "Mono.", "mscorlib", "System", "netstandard", "BepInEx", "UnityEngine", "MortarBepInExBridge"];

    private readonly Dictionary<string, long> ticks = [];

    /// <summary>The assembly a patch call came from: the first frame outside Harmony, the runtime and the loader.</summary>
    internal static string? Caller(IEnumerable<string?> frameAssemblies) =>
        frameAssemblies.FirstOrDefault(a => !string.IsNullOrEmpty(a) && !SkippedPrefixes.Any(p => a!.StartsWith(p, StringComparison.Ordinal)));

    internal static string? Caller(StackTrace trace) =>
        Caller(trace.GetFrames()?.Select(f => f.GetMethod()?.DeclaringType?.Assembly.GetName().Name) ?? []);

    public void Charge(string? assembly, long elapsedTicks)
    {
        if (assembly == null)
            return;
        lock (this.ticks)
            this.ticks[assembly] = this.ticks.TryGetValue(assembly, out long t) ? t + elapsedTicks : elapsedTicks;
    }

    /// <summary>Milliseconds per assembly name.</summary>
    public Dictionary<string, long> Ms(long frequency)
    {
        lock (this.ticks)
            return this.ticks.ToDictionary(p => p.Key, p => p.Value * 1000 / frequency);
    }
}

/// <summary>
/// Wraps the Harmony entry points a plugin patches through, so each plugin's patching time is charged to its assembly.
/// Harmony is bound by reflection: the patcher project cannot reference HarmonyX without pulling a second Mono.Cecil
/// beside the one the patcher's signature binds to. Only installed on a launch Mortar measures.
/// </summary>
internal static class PatchTiming
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly PatchTimer Timer = new();
    [ThreadStatic] private static int depth;
    [ThreadStatic] private static string? caller;

    /// <summary>False when Harmony is not the HarmonyX this binds to; the launch is then timed without patches.</summary>
    public static bool Install()
    {
        Assembly? asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony");
        Type? harmony = asm?.GetType("HarmonyLib.Harmony");
        Type? method = asm?.GetType("HarmonyLib.HarmonyMethod");
        if (asm == null || harmony == null || method == null)
            return false;
        object instance = Activator.CreateInstance(harmony, "Rethunk.MortarBepInExBridge.startup")!;
        MethodInfo patch = harmony.GetMethods().First(m => m.Name == "Patch" && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(MethodBase));
        object begin = Activator.CreateInstance(method, typeof(PatchTiming).GetMethod(nameof(Begin), Any))!;
        object end = Activator.CreateInstance(method, typeof(PatchTiming).GetMethod(nameof(End), Any))!;
        ParameterInfo[] parameters = patch.GetParameters();
        int prefix = Array.FindIndex(parameters, p => p.Name == "prefix");
        int finalizer = Array.FindIndex(parameters, p => p.Name == "finalizer");
        if (prefix < 0 || finalizer < 0)
            return false;
        MethodBase?[] targets =
        [
            harmony.GetMethod("Patch", Any, null, [.. parameters.Select(p => p.ParameterType)], null),
            asm.GetType("HarmonyLib.PatchClassProcessor")?.GetMethod("Patch", Any, null, Type.EmptyTypes, null),
            asm.GetType("HarmonyLib.PatchProcessor")?.GetMethod("Patch", Any, null, Type.EmptyTypes, null),
        ];
        foreach (MethodBase? target in targets)
        {
            if (target == null)
                continue;
            object?[] args = new object?[parameters.Length];
            args[0] = target;
            args[prefix] = begin;
            args[finalizer] = end;
            patch.Invoke(instance, args);
        }
        return true;
    }

    /// <summary>Milliseconds each assembly spent applying patches so far.</summary>
    public static Dictionary<string, long> Ms() => Timer.Ms(Stopwatch.Frequency);

    private static void Begin(out long __state)
    {
        if (depth++ > 0)
        {
            __state = -1;
            return;
        }
        caller = null;
        try
        {
            caller = PatchTimer.Caller(new StackTrace(1, false));
        }
        catch (Exception)
        {
            // A failed attribution costs the row, never the patch.
        }
        __state = Stopwatch.GetTimestamp();
    }

    private static void End(long __state)
    {
        depth--;
        if (__state >= 0)
            Timer.Charge(caller, Stopwatch.GetTimestamp() - __state);
    }
}
