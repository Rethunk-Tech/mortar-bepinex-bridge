using System.Diagnostics;
using MortarBepInExBridge.Perf;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class PerfReportTests
{
    [Fact]
    public void FpsStaysPositiveWhenFrameCountTimes1000ExceedsInt32()
    {
        var frames = new FrameStats();
        for (int i = 0; i < 3_000_000; i++)
            frames.Add(1.0);
        string json = PerfReport.Json(frames, new CostTracker(0), [], null, new Memory(0, 0, 0), Stopwatch.Frequency);
        Assert.Contains("\"fps\":1000", json);
        Assert.DoesNotContain("\"fps\":-", json);
    }
}
