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
        Assert.Null(IntroSkip.Find("NoSuchGameType"));
        Assert.Equal(typeof(IntroSkip), IntroSkip.Find("MortarBepInExBridge.IntroSkip"));
        IntroSkip.Apply(new object(), Steps[0]);
    }
    [Fact]
    public void FindCachesAMissSoLaterScenesDoNotWalkTheDomainAgain()
    {
        Assert.Null(IntroSkip.Find("MortarBepInExBridge.NoSuchTypeInThisProcess"));
        object cache = typeof(IntroSkip).GetField("Types", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        Assert.True(((System.Collections.IDictionary)cache).Contains("MortarBepInExBridge.NoSuchTypeInThisProcess"));
    }
}
