using Xunit;

namespace MortarBepInExBridge.Tests;

public class GameVersionTests
{
    private sealed class GameNetworkManager
    {
        public static GameNetworkManager? Instance { get; set; }

        public int gameVersionNum = 1;
    }

    private static Type? Lethal(string name) => name == "GameNetworkManager" ? typeof(GameNetworkManager) : null;

    [Fact]
    public void LethalCompanyReportsTheVersionItsMenuShowsOnceTheGameHasMadeIt()
    {
        GameNetworkManager.Instance = null;
        Assert.Null(GameVersion.Read(Lethal));
        GameNetworkManager.Instance = new GameNetworkManager { gameVersionNum = 81 };
        Assert.Equal("v81", GameVersion.Read(Lethal));
    }

    private sealed class ModdedNetworkManager
    {
        public static ModdedNetworkManager? Instance { get; set; }

        public int gameVersionNum = 1;
    }

    [Fact]
    public void AVersionAModChangesAfterStartUpReadsAsTheGameSetIt()
    {
        GameVersion.Watch(typeof(ModdedNetworkManager), GameVersion.Sources[0]);
        var made = new ModdedNetworkManager { gameVersionNum = 81 };
        GameVersion.BeforeStartUp(made);
        made.gameVersionNum += 9950;
        ModdedNetworkManager.Instance = made;
        Assert.Equal("v81", GameVersion.Read(name => name == "GameNetworkManager" ? typeof(ModdedNetworkManager) : null));
    }

    [Fact]
    public void AGameWithoutASourceHasNoVersionOfItsOwn() =>
        Assert.Null(GameVersion.Read(_ => null));
}
