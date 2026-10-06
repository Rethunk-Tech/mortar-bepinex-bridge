using Xunit;

namespace MortarBepInExBridge.Tests;

public class IntroSkipTests
{
    private sealed class InitializeGame
    {
        public bool runBootUpScreen = true;
        private bool playColdOpenCinematic = true;
        public bool playColdOpenCinematic2 = true;

        public bool ColdOpen => this.playColdOpenCinematic;
    }

    private sealed class PreInitSceneScript
    {
        public bool? Chose;

        public void ChooseLaunchOption(bool online) => this.Chose = online;
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    public void OnlyAnExplicitOneSkips(string? value, bool skips) =>
        Assert.Equal(skips, IntroSkip.Requested(name => name == IntroSkip.EnvVar ? value : "1"));

    [Fact]
    public void BootUpAndColdOpensAreClearedInBothInitScenes()
    {
        foreach (string scene in new[] { "InitScene", "InitSceneLANMode" })
        {
            var game = new InitializeGame();
            foreach (IntroStep step in IntroSkip.For(scene))
                IntroSkip.Apply(game, step);
            Assert.False(game.runBootUpScreen);
            Assert.False(game.ColdOpen);
            Assert.False(game.playColdOpenCinematic2);
        }
    }

    [Fact]
    public void TheLaunchOptionsSceneChoosesLanAfterSettingsLoad()
    {
        IntroStep step = Assert.Single(IntroSkip.For("InitSceneLaunchOptions"));
        var menu = new PreInitSceneScript();
        IntroSkip.Apply(menu, step);
        Assert.False(menu.Chose);
        Assert.True(step.Delay > 0.5f);
    }

    [Fact]
    public void OtherScenesAndAbsentTypesAreLeftAlone()
    {
        Assert.Empty(IntroSkip.For("MainMenu"));
        Assert.Null(IntroSkip.Find("PreInitSceneScript"));
        Assert.Equal(typeof(IntroSkip), IntroSkip.Find("MortarBepInExBridge.IntroSkip"));
        var unrelated = new object();
        IntroSkip.Apply(unrelated, IntroSkip.Steps[0]);
    }
}
