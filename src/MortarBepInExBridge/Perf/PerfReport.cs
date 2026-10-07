using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MortarBepInExBridge.Perf;

/// <summary>Whether Mortar asked to measure this launch.</summary>
internal static class MeasuredLaunch
{
    /// <summary>Set in the game's process by the bridge's preloader patcher when Mortar asked to measure the launch.</summary>
    public const string EnvVar = "MORTAR_MEASURED_LAUNCH";

    public static bool Requested(Func<string, string?> env) => env(EnvVar) == "1";
}

/// <summary>Memory as the game reports it at the moment of the summary, in bytes, and garbage collections in the window.</summary>
internal readonly struct Memory(long monoUsed, long monoHeap, int collections)
{
    public long MonoUsed { get; } = monoUsed;
    public long MonoHeap { get; } = monoHeap;
    public int Collections { get; } = collections;
}

/// <summary>The <c>perf</c> reply: frame times, memory and each plugin's main-thread cost per frame over the window.</summary>
internal static class PerfReport
{
    public const string Unmeasured = "{\"measured\":false}";

    public static string Json(FrameStats frames, CostTracker costs, IReadOnlyList<string> plugins, PatchGraph? graph, Memory memory, long tickFrequency)
    {
        double perFrame = frames.Frames == 0 ? 0 : 1.0 / frames.Frames;
        double tickMs = 1000.0 / tickFrequency;
        var sb = new StringBuilder("{\"measured\":true,\"instrumented\":").Append(graph != null ? "true" : "false")
            .Append(",\"seconds\":").Append(Num(frames.TotalMs / 1000))
            .Append(",\"frames\":").Append(frames.Frames)
            .Append(",\"fps\":").Append(Num(frames.TotalMs > 0 ? frames.Frames * 1000 / frames.TotalMs : 0))
            .Append(",\"frameMs\":{\"avg\":").Append(Num(frames.AverageMs))
            .Append(",\"p50\":").Append(Num(frames.Percentile(0.5)))
            .Append(",\"p95\":").Append(Num(frames.Percentile(0.95)))
            .Append(",\"p99\":").Append(Num(frames.Percentile(0.99)))
            .Append(",\"max\":").Append(Num(frames.MaxMs))
            .Append("},\"monoUsedBytes\":").Append(memory.MonoUsed)
            .Append(",\"monoHeapBytes\":").Append(memory.MonoHeap)
            .Append(",\"gcCollections\":").Append(memory.Collections)
            .Append(",\"baseline\":{\"frameMs\":").Append(Num(frames.AverageMs))
            .Append(",\"modsMs\":").Append(Num(ModsTicks(costs, plugins.Count) * tickMs * perFrame))
            .Append(",\"withoutModsMs\":").Append(Num(Math.Max(0, frames.AverageMs - ModsTicks(costs, plugins.Count) * tickMs * perFrame)))
            .Append('}')
            .Append(",\"plugins\":[");
        for (int i = 0; i < plugins.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append("{\"guid\":").Append(MortarBepInExBridge.Json.Quote(plugins[i]))
                .Append(",\"msPerFrame\":").Append(Num(costs.Total(i) * tickMs * perFrame))
                .Append(",\"p95Ms\":").Append(Num(costs.P95Ms(i, frames.Frames)))
                .Append(",\"peakMs\":").Append(Num(costs.Peak(i) * tickMs))
                .Append(",\"share\":").Append(Num(frames.AverageMs > 0 ? costs.Total(i) * tickMs * perFrame / frames.AverageMs : 0, 4))
                .Append(",\"callsPerFrame\":").Append(Num(costs.Calls(i) * perFrame))
                .Append(",\"patches\":").Append(Count(graph?.Patches, plugins[i]))
                .Append(",\"transpilers\":").Append(Count(graph?.Transpilers, plugins[i])).Append('}');
        }
        sb.Append("],\"patchOwners\":{");
        bool first = true;
        foreach (KeyValuePair<string, SortedSet<string>> owner in graph?.Owners ?? [])
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append(MortarBepInExBridge.Json.Quote(owner.Key)).Append(":[")
                .Append(string.Join(",", owner.Value.Select(MortarBepInExBridge.Json.Quote))).Append(']');
        }
        return sb.Append("}}").ToString();
    }

    private static long ModsTicks(CostTracker costs, int plugins)
    {
        long ticks = 0;
        for (int i = 0; i < plugins; i++)
            ticks += costs.Total(i);
        return ticks;
    }

    private static int Count(Dictionary<string, int>? counts, string plugin) =>
        counts != null && counts.TryGetValue(plugin, out int n) ? n : 0;

    private static string Num(double value, int digits = 3) => Math.Round(value, digits).ToString("0.####", CultureInfo.InvariantCulture);
}
