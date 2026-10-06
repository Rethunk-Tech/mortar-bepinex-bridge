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
        Assert.Equal("0.1", GameVersion.Read(Lethal, "0.1"));
        GameNetworkManager.Instance = new GameNetworkManager { gameVersionNum = 81 };
        Assert.Equal("v81", GameVersion.Read(Lethal, "0.1"));
    }

    [Fact]
    public void AGameWithoutASourceReportsApplicationVersion() =>
        Assert.Equal("0.220.5", GameVersion.Read(_ => null, "0.220.5"));
}
