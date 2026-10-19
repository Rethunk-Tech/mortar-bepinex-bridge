using Xunit;

namespace MortarBepInExBridge.Tests;

public class LethalCompanyVersionTests
{
    [Fact]
    public void TheMenuVersionComesFromGameNetworkManager()
    {
        GameVersionSource source = Assert.Single(BuiltGame.Hooks!.VersionSources);
        Assert.Equal(("GameNetworkManager", "Instance", "gameVersionNum", "v{0}"), (source.Type, source.Instance, source.Field, source.Format));
    }
}
