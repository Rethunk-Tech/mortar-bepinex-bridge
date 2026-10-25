extern alias Patcher;
using System.Reflection;
using System.Text.Json;
using MortarBepInExBridge.Perf;
using Xunit;
using StartupPatcher = Patcher::MortarBepInExBridge.StartupPatcher;

namespace MortarBepInExBridge.Tests;

public class PerfTests
{
    // Stand-ins for the game's methods and for plugins' patch methods.
    private static void GameUpdate()
    {
    }

    private static void GameDraw()
    {
    }

    private static void PrefixA()
    {
    }

    private static void PostfixA()
    {
    }

    private static IEnumerable<object> TranspilerB(IEnumerable<object> code) => code;

    private static void PrefixMine()
    {
    }

    private static MethodInfo M(string name) => typeof(PerfTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void PatchesAreTimedForThePluginWhoseAssemblyDeclaresThem()
    {
        PatchEdge[] edges =
        [
            new(M(nameof(GameUpdate)), "com.a", M(nameof(PrefixA))),
            new(M(nameof(GameUpdate)), "com.a", M(nameof(PostfixA))),
            new(M(nameof(GameDraw)), "com.a", M(nameof(PrefixA))),
            new(M(nameof(GameDraw)), "harmony.b", M(nameof(TranspilerB)), transpiler: true),
            new(M(nameof(GameDraw)), "bridge.perf", M(nameof(PrefixMine))),
        ];
        // Only com.a's patches come from a plugin assembly Mortar knows; harmony.b's is listed under its Harmony ID.
        PatchGraph graph = PatchGraph.Build(edges, "bridge.perf", a => a == typeof(PerfTests).Assembly ? "com.a" : null);

        Assert.Equal([(M(nameof(PrefixA)), "com.a"), (M(nameof(PostfixA)), "com.a")], graph.Timed);
        Assert.Equal(4, graph.Patches["com.a"]);
        Assert.Equal(1, graph.Transpilers["com.a"]);
        Assert.Equal(["harmony.b", "com.a"], graph.Owners.Keys.OrderByDescending(k => k));
        Assert.Equal([$"{typeof(PerfTests).FullName}::GameDraw", $"{typeof(PerfTests).FullName}::GameUpdate"], graph.Owners["com.a"]);
        Assert.False(graph.Owners.ContainsKey("bridge.perf"));
    }

    [Fact]
    public void NestedCallsChargeEachPluginOnlyItsOwnTime()
    {
        var costs = new CostTracker(2);
        Assert.True(costs.Enter());   // plugin 0's Update
        Assert.True(costs.Enter());   // calls a method plugin 1 patched
        costs.Exit(1, 30);
        costs.Exit(0, 100);
        costs.EndFrame();
        Assert.True(costs.Enter());
        costs.Exit(0, 40);
        costs.EndFrame();

        Assert.Equal(110, costs.Total(0));
        Assert.Equal(70, costs.Peak(0));
        Assert.Equal(2, costs.Calls(0));
        Assert.Equal(30, costs.Total(1));
    }

    [Fact]
    public void EnterStopsTimingPastTheNestingCap()
    {
        var costs = new CostTracker(1);
        for (int i = 0; i < 64; i++)
            Assert.True(costs.Enter());
        Assert.False(costs.Enter());
        costs.Exit(0, 10);
        costs.EndFrame();
        Assert.Equal(10, costs.Total(0));
        Assert.Equal(1, costs.Calls(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void HistogramIgnoresNonPositiveSamples(double ms)
    {
        var h = new Histogram();
        h.Add(ms);
        h.Add(5);
        Assert.Equal(0, h.Percentile(0.5, 2));
        Assert.Equal(5, h.Max);
    }

    [Fact]
    public void FramePercentilesComeFromTheHistogram()
    {
        var frames = new FrameStats();
        for (int i = 0; i < 98; i++)
            frames.Add(10);
        frames.Add(40);
        frames.Add(2500);

        Assert.Equal(100, frames.Frames);
        Assert.Equal(10.1, frames.Percentile(0.5), 3);
        Assert.Equal(40.1, frames.Percentile(0.99), 3);
        Assert.Equal(2500, frames.Percentile(1));
        Assert.Equal(2500, frames.MaxMs);
    }

    [Fact]
    public void TheReportCarriesFramesMemoryPluginsAndPatchOwners()
    {
        var frames = new FrameStats();
        frames.Add(20);
        frames.Add(20);
        var costs = new CostTracker(1, 0.001);
        costs.Enter();
        costs.Exit(0, 3_000);
        costs.EndFrame();
        PatchGraph graph = PatchGraph.Build([new(M(nameof(GameUpdate)), "com.a", M(nameof(PrefixA)))], "x", _ => "com.a");

        JsonElement r = JsonDocument.Parse(PerfReport.Json(frames, costs, ["com.a"], graph, new Memory(1024, 2048, 3), 1_000_000)).RootElement;

        Assert.True(r.GetProperty("measured").GetBoolean());
        Assert.Equal(50, r.GetProperty("fps").GetDouble());
        Assert.Equal(20, r.GetProperty("frameMs").GetProperty("p95").GetDouble());
        Assert.Equal(1024, r.GetProperty("monoUsedBytes").GetInt64());
        Assert.Equal(3, r.GetProperty("gcCollections").GetInt32());
        JsonElement plugin = r.GetProperty("plugins")[0];
        Assert.Equal("com.a", plugin.GetProperty("guid").GetString());
        Assert.Equal(1.5, plugin.GetProperty("msPerFrame").GetDouble());
        Assert.Equal(3, plugin.GetProperty("peakMs").GetDouble());
        Assert.Equal(0.5, plugin.GetProperty("callsPerFrame").GetDouble());
        Assert.Equal(3, plugin.GetProperty("p95Ms").GetDouble(), 1);
        Assert.Equal(0.075, plugin.GetProperty("share").GetDouble(), 4);
        JsonElement baseline = r.GetProperty("baseline");
        Assert.Equal(20, baseline.GetProperty("frameMs").GetDouble());
        Assert.Equal(1.5, baseline.GetProperty("modsMs").GetDouble());
        Assert.Equal(18.5, baseline.GetProperty("withoutModsMs").GetDouble());
        Assert.Equal(1, plugin.GetProperty("patches").GetInt32());
        Assert.Equal(1, r.GetProperty("patchOwners").GetProperty("com.a").GetArrayLength());
    }

    [Fact]
    public void PerPluginP95CountsEveryFrameAndZeroForARarePlugin()
    {
        var costs = new CostTracker(2, 0.001);
        for (int i = 0; i < 100; i++)
        {
            if (costs.Enter())
                costs.Exit(0, i < 10 ? 50_000 : 1_000);
            if (i == 0 && costs.Enter())
                costs.Exit(1, 30_000);
            costs.EndFrame();
        }

        Assert.InRange(costs.P95Ms(0, 100), 45, 50);
        Assert.Equal(0, costs.P95Ms(1, 100));
        costs.Reset();
        Assert.Equal(0, costs.P95Ms(0, 100));
    }

    [Fact]
    public void PerfStartTurnsMeasuringOnForALaunchThatWasNotMeasured()
    {
        var perf = new PerfSwitch();
        Assert.Equal(PerfReport.Unmeasured, perf.Reply(false));
        Assert.False(perf.Active);
        Assert.False(perf.TakeRestart());

        Assert.Equal("{\"measured\":true}", perf.Reply(true));
        Assert.True(perf.Active);
        Assert.True(perf.TakeRestart());
        Assert.False(perf.TakeRestart());
        perf.Summary = "{\"measured\":true,\"frames\":1}";
        Assert.Equal("{\"measured\":true,\"frames\":1}", perf.Reply(false));
    }

    [Fact]
    public void AFailedMeasureStaysOffThroughALaterStart()
    {
        var perf = new PerfSwitch();
        perf.Start();
        perf.Fail("Could not load type \"X\"");
        JsonElement r = JsonDocument.Parse(perf.Reply(true)).RootElement;
        Assert.False(r.GetProperty("measured").GetBoolean());
        Assert.Equal("Could not load type \"X\"", r.GetProperty("reason").GetString());
        Assert.False(perf.Active);
    }

    private sealed class Timed
    {
        private void Update()
        {
        }
    }

    private sealed class Unloadable
    {
        private void Update()
        {
        }
    }

    [Fact]
    public void ATypeThatCannotLoadIsSkippedAndTheRestAreStillTimed()
    {
        // Mono throws this from a mod's compatibility class whose field type lives in a mod that is not installed.
        static IEnumerable<MethodInfo> UpdatesOf(Type t)
        {
            if (t == typeof(Unloadable))
                throw new TypeLoadException("Could not load type of field 'Dawn.Compatibility.SoundAPICompat+<>c:<>9'");
            if (t.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic) is { } update)
                yield return update;
        }

        List<MethodInfo> timed = LoadableTypes.Collect([typeof(Unloadable), null, typeof(Timed), typeof(PerfTests)], UpdatesOf, out int skipped);

        Assert.Equal([typeof(Timed).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!], timed);
        Assert.Equal(2, skipped);
    }

    [Fact]
    public void ThePatcherAndThePluginAgreeOnTheMeasuredSignal()
    {
        Assert.Equal(MeasuredLaunch.EnvVar, StartupPatcher.MeasuredEnvVar);
        Assert.True(MeasuredLaunch.Requested(name => name == MeasuredLaunch.EnvVar ? "1" : null));
        Assert.False(MeasuredLaunch.Requested(_ => null));
    }
}
