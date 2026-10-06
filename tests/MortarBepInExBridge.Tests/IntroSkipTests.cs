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
    [InlineData("1", null, "Menu")]
    [InlineData("1", "intro", "Menu")]
    [InlineData(null, "menu\n", "Menu")]
    [InlineData(null, "intro", "Animations")]
    [InlineData(null, null, "Play")]
    [InlineData("0", "", "Play")]
    [InlineData("true", "yes", "Play")]
    public void TheEnvironmentOrTheRequestFileAsksForAMode(string? env, string? request, string mode) =>
        Assert.Equal(mode, IntroSkip.Requested(name => name == IntroSkip.EnvVar ? env : "1", request).ToString());

    [Fact]
    public void ThePlayersSettingSkipsAnimationsButLeavesTheLaunchChoiceAlone()
    {
        Assert.Empty(IntroSkip.For("InitSceneLaunchOptions", IntroMode.Animations));
        Assert.Single(IntroSkip.For("InitScene", IntroMode.Animations));
        Assert.Empty(IntroSkip.For("InitScene", IntroMode.Play));
    }

    [Fact]
    public void BootUpAndColdOpensAreClearedInBothInitScenes()
    {
        foreach (string scene in new[] { "InitScene", "InitSceneLANMode" })
        {
            var game = new InitializeGame();
            foreach (IntroStep step in IntroSkip.For(scene, IntroMode.Menu))
                IntroSkip.Apply(game, step);
            Assert.False(game.runBootUpScreen);
            Assert.False(game.ColdOpen);
            Assert.False(game.playColdOpenCinematic2);
        }
    }

    [Fact]
    public void TheLaunchOptionsSceneChoosesLanAfterSettingsLoad()
    {
        IntroStep step = Assert.Single(IntroSkip.For("InitSceneLaunchOptions", IntroMode.Menu));
        var menu = new PreInitSceneScript();
        IntroSkip.Apply(menu, step);
        Assert.False(menu.Chose);
        Assert.True(step.Delay > 0.5f);
    }

    [Fact]
    public void OtherScenesAndAbsentTypesAreLeftAlone()
    {
        Assert.Empty(IntroSkip.For("MainMenu", IntroMode.Menu));
        Assert.Null(IntroSkip.Find("PreInitSceneScript"));
        Assert.Equal(typeof(IntroSkip), IntroSkip.Find("MortarBepInExBridge.IntroSkip"));
        var unrelated = new object();
        IntroSkip.Apply(unrelated, IntroSkip.Steps[0]);
    }
}
