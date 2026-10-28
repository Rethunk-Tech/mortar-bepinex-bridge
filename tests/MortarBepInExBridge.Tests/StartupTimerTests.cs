extern alias Patcher;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;
using PluginId = Patcher::MortarBepInExBridge.PluginId;
using StartupTimer = Patcher::MortarBepInExBridge.StartupTimer;

namespace MortarBepInExBridge.Tests;

public class StartupTimerTests
{
    private static JsonElement Run(string title, params (string Source, string Message, long Ms)[] lines)
    {
        var timer = new StartupTimer(1000, title);
        foreach ((string source, string message, long ms) in lines)
            timer.OnLog(source, message, ms);
        Assert.False(timer.OnScene("InitScene", 5000));
        Assert.True(timer.OnScene("MainMenu", 9000));
        string json = timer.Report(new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc), "5.4.21.0", "v72", text => text switch
        {
            "Alpha 1.0.0" => new PluginId("com.a.alpha", "Alpha", "1.0.0"),
            "Beta 2.0.0" => new PluginId("com.b.beta", "Beta", "2.0.0"),
            _ => null,
        });
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void AGameWithAReadySignalIsMeasuredUntilItFires()
    {
        var timer = new StartupTimer(1000, "title") { WaitForReady = true };
        Assert.False(timer.OnScene("title", 9000));
        Assert.True(timer.OnReady(12000));
        JsonElement r = JsonDocument.Parse(timer.Report(DateTime.UtcNow, "5.4.21.0", "v1", _ => null)).RootElement;
        Assert.Equal(9000, r.GetProperty("phases").GetProperty("titleScreen").GetInt64());
        Assert.Equal(12000, r.GetProperty("phases").GetProperty("ready").GetInt64());
        Assert.Equal(12000 - 1000, r.GetProperty("otherMs").GetInt64());
    }

    [Fact]
    public void ASignalThatNeverFiresEndsAtTheNextSceneAfterTheTitle()
    {
        var timer = new StartupTimer(1000, "title") { WaitForReady = true };
        Assert.False(timer.OnScene("title", 9000));
        Assert.True(timer.OnScene("other", 9500));
    }

    [Fact]
    public void EachPluginIsChargedUntilTheChainloadersNextPluginLine()
    {
        JsonElement r = Run("MainMenu",
            ("BepInEx", "Chainloader started", 3000),
            ("BepInEx", "Loading [Alpha 1.0.0]", 3100),
            ("Alpha", "Loading [assets]", 3150),
            ("BepInEx", "Loading [Beta 2.0.0]", 3400),
            ("BepInEx", "Error loading [Beta 2.0.0] : boom", 3450),
            ("BepInEx", "Loading [Gamma 3.0.0]", 3500),
            ("BepInEx", "Chainloader startup complete", 3800));

        JsonElement[] mods = [.. r.GetProperty("mods").EnumerateArray()];
        Assert.Equal(["com.a.alpha", "com.b.beta", "Gamma 3.0.0"], mods.Select(m => m.GetProperty("id").GetString()));
        Assert.Equal([300L, 50L, 300L], mods.Select(m => m.GetProperty("entryMs").GetInt64()));
        Assert.Equal("Beta", mods[1].GetProperty("name").GetString());
        JsonElement phases = r.GetProperty("phases");
        Assert.Equal(1000, phases.GetProperty("bridgeEntry").GetInt64());
        Assert.Equal(3000, phases.GetProperty("entryDone").GetInt64());
        Assert.Equal(3800, phases.GetProperty("gameLaunched").GetInt64());
        Assert.Equal(5000, phases.GetProperty("titleMenu").GetInt64());
        Assert.Equal(9000, phases.GetProperty("titleScreen").GetInt64());
        Assert.True(r.GetProperty("entryTimed").GetBoolean());
        Assert.Equal(0, r.GetProperty("entryMissed").GetInt32());
        Assert.Equal(9000 - 1000 - 650, r.GetProperty("otherMs").GetInt64());
        Assert.Equal("2026-10-06T18:00:00.000Z", r.GetProperty("processStart").GetString());
        Assert.Equal("5.4.21.0", r.GetProperty("loader").GetString());
    }

    [Fact]
    public void APluginTheChainloaderNeverFinishedIsMissedNotCharged()
    {
        JsonElement r = Run("MainMenu", ("BepInEx", "Loading [Alpha 1.0.0]", 3100));
        Assert.Empty(r.GetProperty("mods").EnumerateArray());
        Assert.Equal(1, r.GetProperty("entryMissed").GetInt32());
    }

    [Fact]
    public void PatchAndSharedTimeJoinTheirPluginsRowAndTheRestIsSampledOther()
    {
        var timer = new StartupTimer(1000, "MainMenu");
        timer.OnLog("BepInEx", "Loading [Alpha 1.0.0]", 3100);
        timer.OnLog("BepInEx", "Loading [Beta 2.0.0]", 3400);
        timer.OnLog("BepInEx", "Chainloader startup complete", 3800);
        Assert.True(timer.OnScene("MainMenu", 9000));
        string json = timer.Report(new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc), "5.4.21.0", "v72",
            text => text == "Alpha 1.0.0" ? new PluginId("com.a.alpha", "Alpha", "1.0.0") : new PluginId("com.b.beta", "Beta", "2.0.0"),
            new Dictionary<string, long> { ["com.a.alpha"] = 120 },
            new Dictionary<string, long> { ["com.b.beta"] = 700, ["com.a.alpha"] = 0 });
        JsonElement r = JsonDocument.Parse(json).RootElement;

        JsonElement[] mods = [.. r.GetProperty("mods").EnumerateArray()];
        Assert.Equal(120, mods[0].GetProperty("patchMs").GetInt64());
        Assert.False(mods[0].TryGetProperty("sampleMs", out _));
        Assert.False(mods[1].TryGetProperty("patchMs", out _));
        Assert.Equal(700, mods[1].GetProperty("sampleMs").GetInt64());
        Assert.Equal(9000 - 3800 - 700, r.GetProperty("sampledOtherMs").GetInt64());
    }

    [Fact]
    public void AnUnsampledLaunchReportsNoSampledShare()
    {
        JsonElement r = Run("MainMenu", ("BepInEx", "Loading [Alpha 1.0.0]", 3100));
        Assert.False(r.TryGetProperty("sampledOtherMs", out _));
    }

    [Fact]
    public void WithoutATitleSceneTheFirstSceneEndsStartup()
    {
        var timer = new StartupTimer(1000, "");
        Assert.True(timer.OnScene("start", 4000));
    }

    [Fact]
    public void WriteKeepsTheNewestReportsAndEverySampleFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mortar-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            for (int i = 0; i < 12; i++)
                File.WriteAllText(Path.Combine(dir, $"20260101T0000{i:00}Z.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "20260101T000000Z.samples.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "overlay.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "zzzz.json"), "{}");
            string path = StartupTimer.Write(dir, new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc), "{\"schema\":1}");

            Assert.Equal("20261006T180000Z.json", Path.GetFileName(path));
            Assert.Equal("{\"schema\":1}", File.ReadAllText(path));
            Assert.Equal(10, Directory.GetFiles(dir, "*Z.json").Length);
            Assert.True(File.Exists(Path.Combine(dir, "20260101T000000Z.samples.json")));
            Assert.True(File.Exists(Path.Combine(dir, "overlay.json")));
            Assert.True(File.Exists(Path.Combine(dir, "zzzz.json")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void WriteReplacesAnExistingReportAndRestrictsIt()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mortar-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            DateTime start = new(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc);
            string path = StartupTimer.Write(dir, start, "old");
            Assert.Equal(path, StartupTimer.Write(dir, start, "new"));
            Assert.Equal("new", File.ReadAllText(path));
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
