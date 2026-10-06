using System;

namespace MortarBepInExBridge.Perf;

/// <summary>
/// Each plugin's exclusive main-thread time per frame. Timed calls nest (a plugin's Update calling a method another
/// plugin patched), so a call's own time is its total less the time of the timed calls inside it; otherwise the outer
/// plugin would be charged for the inner one. Times are Stopwatch ticks.
/// </summary>
internal sealed class CostTracker(int plugins)
{
    private const int MaxDepth = 64;
    private readonly long[] child = new long[MaxDepth + 1];
    private int depth;
    private readonly long[] frame = new long[plugins];
    private readonly long[] total = new long[plugins];
    private readonly long[] peak = new long[plugins];
    private readonly long[] calls = new long[plugins];

    /// <summary>A timed call starts; false when nesting is too deep to track, and the call is then not timed.</summary>
    public bool Enter()
    {
        if (this.depth >= MaxDepth)
            return false;
        this.depth++;
        this.child[this.depth] = 0;
        return true;
    }

    /// <summary>The timed call Enter started ends after elapsed ticks, charged to plugin; -1 charges no one.</summary>
    public void Exit(int plugin, long elapsed)
    {
        if (this.depth == 0)
            return;
        long own = Math.Max(0, elapsed - this.child[this.depth]);
        this.depth--;
        this.child[this.depth] += elapsed;
        if (plugin < 0)
            return;
        this.frame[plugin] += own;
        this.calls[plugin]++;
    }

    /// <summary>A frame ended: fold its times into the totals and peaks.</summary>
    public void EndFrame()
    {
        for (int i = 0; i < this.frame.Length; i++)
        {
            this.total[i] += this.frame[i];
            this.peak[i] = Math.Max(this.peak[i], this.frame[i]);
            this.frame[i] = 0;
        }
    }

    public void Reset()
    {
        Array.Clear(this.frame, 0, this.frame.Length);
        Array.Clear(this.total, 0, this.total.Length);
        Array.Clear(this.peak, 0, this.peak.Length);
        Array.Clear(this.calls, 0, this.calls.Length);
    }

    public long Total(int plugin) => this.total[plugin];

    public long Peak(int plugin) => this.peak[plugin];

    public long Calls(int plugin) => this.calls[plugin];
}
