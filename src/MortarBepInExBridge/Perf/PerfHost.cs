using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// In-game performance on a launch Mortar measures: frame times and memory from the start, and, from the first
/// <c>perf start</c>, each plugin's main-thread time per frame. That time is its Harmony prefixes, postfixes and
/// finalizers (each patch method is itself wrapped with a timer) and its own MonoBehaviours' Update, LateUpdate and
/// FixedUpdate. It runs on an object of its own that scene loads leave alone, since Lethal Company's first scene load
/// destroys the plugin's object. Unity is main-thread only, so the socket threads read the summary the main thread
/// rebuilds once a second and only flag a restart.
/// </summary>
internal sealed class PerfHost : MonoBehaviour
{
    private const string HarmonyId = Plugin.Guid + ".perf";
    private static readonly string[] UpdateMethods = ["Update", "LateUpdate", "FixedUpdate"];

    private static volatile bool active;
    private static volatile bool restart;
    private static volatile string summary = PerfReport.Unmeasured;
    private static int mainThread;
    private static CostTracker costs = new(0);
    private static Dictionary<MethodBase, int> pluginOf = [];

    private readonly FrameStats frames = new();
    private ManualLogSource? log;
    private List<string> plugins = [];
    private PatchGraph? graph;
    private int collectionsAtStart;
    private float nextSummary;

    public static void Start(ManualLogSource log)
    {
        var host = new GameObject("MortarBridgePerf") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        host.AddComponent<PerfHost>().log = log;
        mainThread = Thread.CurrentThread.ManagedThreadId;
        active = true;
    }

    /// <summary>The <c>perf</c> reply; start also restarts the window, timing plugins from the first start on.</summary>
    public static string Reply(bool start)
    {
        if (!active)
            return PerfReport.Unmeasured;
        if (start)
        {
            restart = true;
            return "{\"measured\":true}";
        }
        return summary;
    }

    private void Update()
    {
        try
        {
            if (restart)
            {
                restart = false;
                if (this.graph == null)
                    this.Instrument();
                this.frames.Reset();
                costs.Reset();
                this.collectionsAtStart = GC.CollectionCount(0);
            }
            this.frames.Add(Time.unscaledDeltaTime * 1000);
            costs.EndFrame();
            if (Time.unscaledTime >= this.nextSummary)
            {
                this.nextSummary = Time.unscaledTime + 1;
                var memory = new Memory(Profiler.GetMonoUsedSizeLong(), Profiler.GetMonoHeapSizeLong(), GC.CollectionCount(0) - this.collectionsAtStart);
                summary = PerfReport.Json(this.frames, costs, this.plugins, this.graph, memory, Stopwatch.Frequency);
            }
        }
        catch (Exception ex)
        {
            // A measuring failure must never reach the game.
            active = false;
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
            timed.AddRange(OwnUpdates(plugin.Key).Select(m => (m, plugin.Value)));

        this.plugins = [.. timed.Select(t => t.Plugin).Concat(this.graph.Patches.Keys).Distinct()];
        Dictionary<string, int> index = this.plugins.Select((guid, i) => (guid, i)).ToDictionary(p => p.guid, p => p.i);
        costs = new CostTracker(this.plugins.Count);
        Dictionary<MethodBase, int> owners = [];
        foreach ((MethodInfo method, string plugin) in timed)
            owners.TryAdd(method, index[plugin]);
        pluginOf = owners;
        var harmony = new Harmony(HarmonyId);
        var enter = new HarmonyMethod(typeof(PerfHost).GetMethod(nameof(Enter), BindingFlags.Static | BindingFlags.NonPublic));
        var exit = new HarmonyMethod(typeof(PerfHost).GetMethod(nameof(Exit), BindingFlags.Static | BindingFlags.NonPublic));
        int failed = 0;
        foreach (MethodBase method in owners.Keys)
        {
            try
            {
                harmony.Patch(method, prefix: enter, finalizer: exit);
            }
            catch (Exception)
            {
                failed++;
            }
        }
        this.log?.LogInfo($"Measuring {this.plugins.Count} plugins: {owners.Count - failed} methods timed, {failed} could not be wrapped.");
    }

    private static IEnumerable<PatchEdge> Edges()
    {
        foreach (MethodBase target in Harmony.GetAllPatchedMethods().ToList())
        {
            HarmonyLib.Patches? info = Harmony.GetPatchInfo(target);
            if (info == null)
                continue;
            foreach (Patch p in info.Prefixes)
                yield return new PatchEdge(target, p.owner, p.PatchMethod, PatchKind.Prefix);
            foreach (Patch p in info.Postfixes)
                yield return new PatchEdge(target, p.owner, p.PatchMethod, PatchKind.Postfix);
            foreach (Patch p in info.Finalizers)
                yield return new PatchEdge(target, p.owner, p.PatchMethod, PatchKind.Finalizer);
            foreach (Patch p in info.Transpilers)
                yield return new PatchEdge(target, p.owner, p.PatchMethod, PatchKind.Transpiler);
        }
    }

    private static IEnumerable<MethodInfo> OwnUpdates(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (Type? type in types)
        {
            if (type == null || type.ContainsGenericParameters || !typeof(MonoBehaviour).IsAssignableFrom(type))
                continue;
            foreach (string name in UpdateMethods)
            {
                if (type.GetMethod(name, declared, null, Type.EmptyTypes, null) is { IsAbstract: false } method)
                    yield return method;
            }
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
