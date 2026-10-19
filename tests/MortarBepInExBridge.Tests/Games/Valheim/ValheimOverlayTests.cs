using MortarBepInExBridge.Overlay;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class ValheimOverlayTests
{
    private static Type? Find(string name) => name switch
    {
        "Player" => typeof(FakeValheimPlayer),
        "EnvMan" => typeof(FakeEnv),
        "ZoneSystem" => typeof(FakeZone),
        _ => null,
    };

    private sealed class FakeValheimPlayer
    {
        public static FakeValheimPlayer? m_localPlayer;
        public string GetPlayerName() => "Ragna";
        public Biome GetCurrentBiome() => Biome.BlackForest;
    }
    private enum Biome { BlackForest }
    private sealed class FakeEnv
    {
        public static FakeEnv instance = new();
        public int GetDay() => 12;
        public int GetDay(double _) => 99;
    }
    private sealed class FakeZone
    {
        public static FakeZone instance = new();
        public List<string> GetGlobalKeys() => ["defeated_gdking", "defeated_eikthyr", "something_else"];
    }

    [Fact]
    public void ValheimReadsBiomeDayAndBossesInOrder()
    {
        FakeValheimPlayer.m_localPlayer = new FakeValheimPlayer();
        IOverlayProvider provider = BuiltGame.Hooks!.Overlay(Find, _ => null)!;
        Assert.Equal(
            "{\"inGame\":true,\"game\":\"valheim\",\"playerName\":\"Ragna\",\"biome\":\"BlackForest\",\"day\":12,\"bossesDefeated\":[\"Eikthyr\",\"The Elder\"],\"bossCount\":2}",
            OverlayProviders.Snapshot(provider, ex => throw ex));
        FakeValheimPlayer.m_localPlayer = null;
        Assert.Equal(OverlayServer.NotInGame, OverlayProviders.Snapshot(provider, ex => throw ex));
    }
}
