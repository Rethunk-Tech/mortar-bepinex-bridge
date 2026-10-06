using System;

namespace MortarBepInExBridge.Perf;

/// <summary>
/// Frame times since the last reset, kept as a histogram of 0.1 ms buckets so a percentile costs one scan of a fixed
/// array instead of a sort of every frame, which would itself show up in the frames being measured.
/// </summary>
internal sealed class FrameStats
{
    private const double BucketMs = 0.1;
    // Frames slower than a second all land in the last bucket; max keeps their real value.
    private readonly int[] buckets = new int[10_000];
    private long frames;
    private double totalMs;
    private double maxMs;

    public long Frames => this.frames;

    public double TotalMs => this.totalMs;

    public void Reset()
    {
        Array.Clear(this.buckets, 0, this.buckets.Length);
        this.frames = 0;
        this.totalMs = 0;
        this.maxMs = 0;
    }

    public void Add(double ms)
    {
        if (ms < 0 || double.IsNaN(ms))
            return;
        this.buckets[Math.Min(this.buckets.Length - 1, (int)(ms / BucketMs))]++;
        this.frames++;
        this.totalMs += ms;
        this.maxMs = Math.Max(this.maxMs, ms);
    }

    public double AverageMs => this.frames == 0 ? 0 : this.totalMs / this.frames;

    public double MaxMs => this.maxMs;

    /// <summary>The frame time at fraction p (0.95 for the 95th percentile), as the upper edge of its bucket.</summary>
    public double Percentile(double p)
    {
        if (this.frames == 0)
            return 0;
        long rank = (long)Math.Ceiling(p * this.frames);
        long seen = 0;
        for (int i = 0; i < this.buckets.Length; i++)
        {
            seen += this.buckets[i];
            if (seen >= rank)
                return i == this.buckets.Length - 1 ? this.maxMs : Math.Min(this.maxMs, (i + 1) * BucketMs);
        }
        return this.maxMs;
    }
}
