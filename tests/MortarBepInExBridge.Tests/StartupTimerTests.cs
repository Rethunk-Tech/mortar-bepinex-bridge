using System.Text.Json;
using Xunit;

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
            string path = StartupTimer.Write(dir, new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc), "{\"schema\":1}");

            Assert.Equal("20261006T180000Z.json", Path.GetFileName(path));
            Assert.Equal("{\"schema\":1}", File.ReadAllText(path));
            Assert.Equal(10, Directory.GetFiles(dir, "*Z.json").Length);
            Assert.True(File.Exists(Path.Combine(dir, "20260101T000000Z.samples.json")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
