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

    [Fact]
    public void AGameWithoutASourceHasNoVersionOfItsOwn() =>
        Assert.Null(GameVersion.Read(_ => null));
}
