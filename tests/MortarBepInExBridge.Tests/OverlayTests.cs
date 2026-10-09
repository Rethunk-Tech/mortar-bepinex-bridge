using MortarBepInExBridge.Overlay;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class OverlayTests
{
    [Fact]
    public void ConfigNeedsEnabledPortAndToken()
    {
        OverlayConfig? on = OverlayConfig.Parse("{\"OverlayEnabled\":true,\"OverlayPort\":8123,\"OverlayToken\":\"ab12\",\"StartupProfile\":false}");
        Assert.Equal(8123, on?.Port);
        Assert.Equal("ab12", on?.Token);
        Assert.Null(OverlayConfig.Parse("{\"OverlayEnabled\":false,\"OverlayPort\":8123,\"OverlayToken\":\"ab12\"}"));
        Assert.Null(OverlayConfig.Parse("{\"OverlayEnabled\":true,\"OverlayPort\":70000,\"OverlayToken\":\"ab12\"}"));
        Assert.Null(OverlayConfig.Parse("{\"OverlayEnabled\":true,\"OverlayPort\":8123,\"OverlayToken\":\"\"}"));
        Assert.Null(OverlayConfig.Parse(null));
    }

    [Fact]
    public void JsonLeavesOutNullsAndEscapes()
    {
        var values = new Dictionary<string, object?> { ["a"] = "x\"y", ["b"] = null, ["c"] = 3, ["d"] = true, ["e"] = new[] { "p", "q" } };
        Assert.Equal("{\"a\":\"x\\\"y\",\"c\":3,\"d\":true,\"e\":[\"p\",\"q\"]}", OverlayJson.Write(values));
    }

    private sealed class Level { public string PlanetName = "41 Experimentation"; }
    private sealed class Stats { public int daysSpent = 6; }
    private sealed class FakePlayer
    {
        public string playerUsername = "Zed";
        public bool isPlayerControlled;
        public bool isPlayerDead;
    }
    private sealed class FakeRound
    {
        public static FakeRound? Instance;
        public FakePlayer localPlayerController = new() { isPlayerControlled = true };
        public FakePlayer[] allPlayerScripts = [];
        public Level currentLevel = new();
        public Stats gameStats = new();
    }
    private sealed class FakeTime
    {
        public static FakeTime Instance = new();
        public int profitQuota = 400;
        public int quotaFulfilled = 150;
        public int daysUntilDeadline = 2;
    }
    private sealed class FakeTerminal { public int groupCredits = 90; }

    private static Type? Find(string name) => name switch
    {
        "StartOfRound" => typeof(FakeRound),
        "TimeOfDay" => typeof(FakeTime),
        "Terminal" => typeof(FakeTerminal),
        "Player" => typeof(FakeValheimPlayer),
        "EnvMan" => typeof(FakeEnv),
        "ZoneSystem" => typeof(FakeZone),
        _ => null,
    };

    [Fact]
    public void LethalCompanyReadsCrewMoonQuotaAndCredits()
    {
        FakeRound.Instance = new FakeRound
        {
            allPlayerScripts = [new() { isPlayerControlled = true }, new() { isPlayerDead = true }, new()],
        };
        IOverlayProvider provider = OverlayProviders.For("Lethal Company", Find, _ => new FakeTerminal())!;
        string json = OverlayProviders.Snapshot(provider, ex => throw ex);
        Assert.Equal(
            "{\"inGame\":true,\"game\":\"lethal-company\",\"playerName\":\"Zed\",\"moon\":\"Experimentation\",\"crewAlive\":1,\"crewTotal\":2,\"day\":6,\"quota\":400,\"quotaProgress\":150,\"daysLeft\":2,\"credits\":90}",
            json);
    }

    [Fact]
    public void LethalCompanyIsNotInGameWithoutARound()
    {
        FakeRound.Instance = null;
        IOverlayProvider provider = OverlayProviders.For("Lethal Company", Find, _ => null)!;
        Assert.Equal(OverlayServer.NotInGame, OverlayProviders.Snapshot(provider, ex => throw ex));
    }

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
        IOverlayProvider provider = OverlayProviders.For("Valheim", Find, _ => null)!;
        Assert.Equal(
            "{\"inGame\":true,\"game\":\"valheim\",\"playerName\":\"Ragna\",\"biome\":\"BlackForest\",\"day\":12,\"bossesDefeated\":[\"Eikthyr\",\"The Elder\"],\"bossCount\":2}",
            OverlayProviders.Snapshot(provider, ex => throw ex));
        FakeValheimPlayer.m_localPlayer = null;
        Assert.Equal(OverlayServer.NotInGame, OverlayProviders.Snapshot(provider, ex => throw ex));
    }

    [Fact]
    public void UnknownGameHasNoProvider() => Assert.Null(OverlayProviders.For("Stardew Valley", Find, _ => null));

    private sealed class Throws : IOverlayProvider
    {
        public string Game => "x";
        public Dictionary<string, object?>? Read() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void AProviderThatThrowsReadsAsNotInGame()
    {
        string? seen = null;
        Assert.Equal(OverlayServer.NotInGame, OverlayProviders.Snapshot(new Throws(), ex => seen = ex.Message));
        Assert.Equal("boom", seen);
    }

    [Theory]
    [InlineData("GET /state?token=t HTTP/1.1", null, 200)]
    [InlineData("GET /state HTTP/1.1", "Bearer t", 200)]
    [InlineData("GET /state?token=nope HTTP/1.1", null, 401)]
    [InlineData("GET /state HTTP/1.1", null, 401)]
    [InlineData("GET /other?token=t HTTP/1.1", null, 404)]
    [InlineData("POST /state?token=t HTTP/1.1", null, 405)]
    [InlineData("garbage", null, 400)]
    public void ServerChecksMethodPathAndToken(string request, string? auth, int status)
    {
        using var server = new OverlayServer(0, "t");
        server.Set("{\"inGame\":false}");
        var headers = new Dictionary<string, string>();
        if (auth != null)
            headers["Authorization"] = auth;
        string reply = server.Answer(request, headers);
        Assert.StartsWith($"HTTP/1.1 {status} ", reply);
        if (status == 200)
            Assert.EndsWith("{\"inGame\":false}", reply);
    }

    [Fact]
    public void EndlessHeadersAreCutOff()
    {
        using var server = new OverlayServer(0, "t");
        var sb = new System.Text.StringBuilder("GET /state HTTP/1.1\r\n");
        for (int i = 0; i < 100000; i++)
            sb.Append("X-").Append(i).Append(": v\r\n");
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(sb.ToString()));
        Assert.StartsWith("HTTP/1.1 431 ", server.Respond(stream));
        Assert.True(stream.Position < stream.Length / 10);
    }
}
