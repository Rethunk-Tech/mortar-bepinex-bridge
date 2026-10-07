using System;

namespace MortarBepInExBridge.Perf;

/// <summary>
/// Per-frame costs as a histogram of geometric buckets (about 10% wide, from a microsecond), so a percentile costs one
/// scan of a fixed array. Values are milliseconds; samples never added count as zeros.
/// </summary>
internal sealed class Histogram
{
    private const double FirstMs = 0.001;
    private const double Ratio = 1.1;
    private readonly long[] buckets = new long[160];
    private readonly double logRatio = Math.Log(Ratio);
    private long added;

    public double Max { get; private set; }

    public void Reset()
    {
        Array.Clear(this.buckets, 0, this.buckets.Length);
        this.added = 0;
        this.Max = 0;
    }

    public void Add(double ms)
    {
        if (ms <= 0 || double.IsNaN(ms))
            return;
        int bucket = ms <= FirstMs ? 0 : (int)Math.Ceiling(Math.Log(ms / FirstMs) / this.logRatio);
        this.buckets[Math.Min(this.buckets.Length - 1, bucket)]++;
        this.added++;
        this.Max = Math.Max(this.Max, ms);
    }

    /// <summary>The value at fraction p of <paramref name="total"/> samples, as the upper edge of its bucket and never above the largest seen.</summary>
    public double Percentile(double p, long total)
    {
        if (total <= 0)
            return 0;
        long rank = (long)Math.Ceiling(p * total);
        long seen = total - this.added;
        if (rank <= seen)
            return 0;
        for (int i = 0; i < this.buckets.Length; i++)
        {
            seen += this.buckets[i];
            if (seen >= rank)
                return Math.Min(this.Max, FirstMs * Math.Pow(Ratio, i));
        }
        return this.Max;
    }
}
