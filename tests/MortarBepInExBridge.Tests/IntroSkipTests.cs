using Xunit;

namespace MortarBepInExBridge.Tests;

public class IntroSkipTests
{
    [Theory]
    [InlineData("1", null, "Menu")]
    [InlineData("1", "intro", "Menu")]
    [InlineData(null, "menu\n", "Menu")]
    [InlineData(null, "intro", "Animations")]
    [InlineData(null, null, "Play")]
    [InlineData("0", "", "Play")]
    [InlineData("true", "yes", "Play")]
    public void TheEnvironmentOrTheRequestFileAsksForAMode(string? env, string? request, string mode) =>
        Assert.Equal(mode, IntroSkip.Requested(name => name == IntroSkip.EnvVar ? env : "1", request).ToString());

    private static readonly IntroStep[] Steps =
    [
        new("Boot", "Splash", [("play", false)]),
        new("Boot", "Gate", [], "Choose", [false], 2f, choice: true),
    ];

    [Fact]
    public void AnimationsAreSkippedForThePlayerButChoicesAreAnsweredOnlyInMenuMode()
    {
        Assert.Equal(["Splash"], IntroSkip.For(Steps, "Boot", IntroMode.Animations).Select(s => s.Type));
        Assert.Equal(["Splash", "Gate"], IntroSkip.For(Steps, "Boot", IntroMode.Menu).Select(s => s.Type));
        Assert.Empty(IntroSkip.For(Steps, "Boot", IntroMode.Play));
        Assert.Empty(IntroSkip.For(Steps, "Elsewhere", IntroMode.Menu));
        Assert.Empty(IntroSkip.For([], "Boot", IntroMode.Menu));
    }

    [Fact]
    public void AbsentTypesAndUnrelatedObjectsAreLeftAlone()
    {
        Assert.Null(Probe.Find("NoSuchGameType"));
        Assert.Equal(typeof(IntroSkip), Probe.Find("MortarBepInExBridge.IntroSkip"));
        IntroSkip.Apply(new object(), Steps[0]);
    }
    [Fact]
    public void PluginLogsAliveAfterEachSceneForBisect()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Join(dir.FullName, "MortarBepInExBridge.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        string plugin = File.ReadAllText(Path.Join(dir.FullName, "src/MortarBepInExBridge/Plugin.cs"));
        string intro = File.ReadAllText(Path.Join(dir.FullName, "src/MortarBepInExBridge/IntroRunner.cs"));
        Assert.Contains("Bridge plugin alive after scene {scene.name}", plugin);
        Assert.DoesNotContain("Bridge plugin alive after scene {scene.name}", intro);
    }

    [Fact]
    public void SceneStepsFindObjectsWithoutTheObsoleteUnityLookup()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Join(dir.FullName, "MortarBepInExBridge.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        string intro = File.ReadAllText(Path.Join(dir.FullName, "src/MortarBepInExBridge/IntroRunner.cs"));
        Assert.Contains("FindObjectsByType(type)", intro);
        Assert.DoesNotContain("FindObjectsOfType", intro);
        Assert.DoesNotContain("FindObjectsSortMode", intro);
    }

    [Fact]
    public void FindCachesAMissSoLaterScenesDoNotWalkTheDomainAgain()
    {
        Assert.Null(Probe.Find("MortarBepInExBridge.NoSuchTypeInThisProcess"));
        object cache = typeof(Probe).GetField("Types", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        Assert.True(((System.Collections.IDictionary)cache).Contains("MortarBepInExBridge.NoSuchTypeInThisProcess"));
    }
}
