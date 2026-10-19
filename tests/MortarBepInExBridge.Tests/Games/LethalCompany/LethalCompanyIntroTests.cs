using Xunit;

namespace MortarBepInExBridge.Tests;

public class LethalCompanyIntroTests
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

    private static IEnumerable<IntroStep> For(string scene, IntroMode mode) => IntroSkip.For(BuiltGame.Hooks!.IntroSteps, scene, mode);

    [Fact]
    public void ThePlayersSettingSkipsAnimationsButLeavesTheLaunchChoiceAlone()
    {
        Assert.Empty(For("InitSceneLaunchOptions", IntroMode.Animations));
        Assert.Single(For("InitScene", IntroMode.Animations));
        Assert.Empty(For("InitScene", IntroMode.Play));
        Assert.Empty(For("MainMenu", IntroMode.Menu));
    }

    [Fact]
    public void BootUpAndColdOpensAreClearedInBothInitScenes()
    {
        foreach (string scene in new[] { "InitScene", "InitSceneLANMode" })
        {
            var game = new InitializeGame();
            foreach (IntroStep step in For(scene, IntroMode.Menu))
                IntroSkip.Apply(game, step);
            Assert.False(game.runBootUpScreen);
            Assert.False(game.ColdOpen);
            Assert.False(game.playColdOpenCinematic2);
        }
    }

    [Fact]
    public void TheLaunchOptionsSceneChoosesLanAfterSettingsLoad()
    {
        IntroStep step = Assert.Single(For("InitSceneLaunchOptions", IntroMode.Menu));
        var menu = new PreInitSceneScript();
        IntroSkip.Apply(menu, step);
        Assert.False(menu.Chose);
        Assert.True(step.Delay > 0.5f);
    }
}
