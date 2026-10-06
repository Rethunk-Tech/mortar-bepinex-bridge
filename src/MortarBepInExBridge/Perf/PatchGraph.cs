using System;
using System.Collections.Generic;
using System.Reflection;

namespace MortarBepInExBridge.Perf;

internal enum PatchKind
{
    Prefix,
    Postfix,
    Finalizer,
    Transpiler,
}

/// <summary>One Harmony patch on a game method: who applied it and the method it runs.</summary>
internal readonly struct PatchEdge(MethodBase target, string owner, MethodInfo patch, PatchKind kind)
{
    public MethodBase Target { get; } = target;
    public string Owner { get; } = owner;
    public MethodInfo Patch { get; } = patch;
    public PatchKind Kind { get; } = kind;
}

/// <summary>
/// Which plugin each Harmony patch belongs to, and so which patch methods to time for it. A patch belongs to the plugin
/// whose assembly declares its method, since a Harmony ID is only the GUID by convention; otherwise it is listed under
/// the Harmony ID. A transpiler rewrites the game method's body, so its cost cannot be told from the method's own and
/// it is only counted.
/// </summary>
internal sealed class PatchGraph
{
    public List<(MethodInfo Method, string Plugin)> Timed { get; } = [];
    public Dictionary<string, int> Patches { get; } = [];
    public Dictionary<string, int> Transpilers { get; } = [];
    /// <summary>Harmony ID to the game methods it patches, "Type.FullName::Method", for Mortar's same-job hints.</summary>
    public SortedDictionary<string, SortedSet<string>> Owners { get; } = new(StringComparer.Ordinal);

    public static PatchGraph Build(IEnumerable<PatchEdge> edges, string self, Func<Assembly, string?> pluginOf)
    {
        var graph = new PatchGraph();
        var seen = new HashSet<MethodInfo>();
        foreach (PatchEdge e in edges)
        {
            if (e.Owner == self)
                continue;
            string plugin = pluginOf(e.Patch.DeclaringType?.Assembly ?? e.Patch.Module.Assembly) ?? e.Owner;
            if (!graph.Owners.TryGetValue(e.Owner, out SortedSet<string>? targets))
                graph.Owners[e.Owner] = targets = new SortedSet<string>(StringComparer.Ordinal);
            targets.Add($"{e.Target.DeclaringType?.FullName}::{e.Target.Name}");
            graph.Patches[plugin] = graph.Patches.TryGetValue(plugin, out int n) ? n + 1 : 1;
            if (e.Kind == PatchKind.Transpiler)
            {
                graph.Transpilers[plugin] = graph.Transpilers.TryGetValue(plugin, out int t) ? t + 1 : 1;
                continue;
            }
            // A generic patch method has no single body to wrap.
            if (!e.Patch.ContainsGenericParameters && seen.Add(e.Patch))
                graph.Timed.Add((e.Patch, plugin));
        }
        return graph;
    }
}
