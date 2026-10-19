using System.IO;
using System.Text;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class ProtocolTests
{
    private sealed class Game : IGameView
    {
        public string GameVersion => "v62";
        public bool GameVersionIsGames => true;
        public string Scene => "SampleSceneRelay";
        public IReadOnlyList<PluginRow> Plugins { get; } = [new("a.b", "A \"quoted\"", "1.2.3"), new("c.d", "C", "0.1.0", "Pack-C/c.dll")];

        public string Perf(bool start) => start ? "{\"started\":1}" : "{\"frames\":2}";

        public List<string> Ran { get; } = [];

        public string? Console(string line)
        {
            this.Ran.Add(line);
            return line.StartsWith("bad") ? "unknown command\nsecond line" : null;
        }
    }

    [Theory]
    [InlineData("ping", "ok")]
    [InlineData(" PING ", "ok")]
    [InlineData("status", "ok {\"gameVersion\":\"v62\",\"gameVersionSource\":\"game\",\"scene\":\"SampleSceneRelay\",\"plugins\":[{\"guid\":\"a.b\",\"version\":\"1.2.3\"},{\"guid\":\"c.d\",\"version\":\"0.1.0\",\"location\":\"Pack-C/c.dll\"}]}")]
    [InlineData("plugins", "ok [{\"guid\":\"a.b\",\"name\":\"A \\\"quoted\\\"\",\"version\":\"1.2.3\"},{\"guid\":\"c.d\",\"name\":\"C\",\"version\":\"0.1.0\",\"location\":\"Pack-C/c.dll\"}]")]
    [InlineData("perf", "ok {\"frames\":2}")]
    [InlineData("PERF START", "ok {\"started\":1}")]
    [InlineData("", "error: empty command")]
    public void Commands(string line, string reply) => Assert.Equal(reply, Protocol.Handle(line, new Game()));

    [Theory]
    [InlineData(@"Z:\p\BepInEx\plugins", @"Z:\p\BepInEx\plugins\Pack-A\Sub\a.dll", "Pack-A/Sub/a.dll")]
    [InlineData("/p/BepInEx/plugins/", "/p/BepInEx/plugins/Pack-A/a.dll", "Pack-A/a.dll")]
    [InlineData("/p/BepInEx/plugins", "/p/BepInEx/core/x.dll", "")]
    [InlineData("/p/BepInEx/plugins", "", "")]
    [InlineData("/p/BepInEx/plugins", "/p/BepInEx/plugins/foo/../../secret.dll", "")]
    [InlineData("/p/BepInEx/plugins", "/p/BepInEx/plugins/Pack/../Pack/a.dll", "Pack/../Pack/a.dll")]
    public void LocationIsBelowThePluginsFolder(string plugins, string path, string below) =>
        Assert.Equal(below, PluginRow.Below(plugins, path));

    [Fact]
    public void StatusNamesUnityWhenTheGameHasNoVersionOfItsOwn()
    {
        var game = new UnityGame();
        Assert.Equal(
            "ok {\"gameVersion\":\"2022.3.9\",\"gameVersionSource\":\"unity\",\"scene\":\"MainMenu\",\"plugins\":[]}",
            Protocol.Handle("status", game));
    }

    private sealed class UnityGame : IGameView
    {
        public string GameVersion => "2022.3.9";
        public bool GameVersionIsGames => false;
        public string Scene => "MainMenu";
        public IReadOnlyList<PluginRow> Plugins { get; } = [];

        public string Perf(bool start) => "{}";

        public string? Console(string line) => null;
    }

    [Fact]
    public void UnknownCommandNamesTheOnesThatExist()
    {
        string reply = Protocol.Handle("give money", new Game());
        Assert.StartsWith("error: unknown command \"give money\"", reply);
        Assert.Contains("ping, status, plugins, perf, perf start or console <line>", reply);
    }

    [Fact]
    public void ConsoleRunsTheLineWithItsCaseAndAnswersOneLine()
    {
        var game = new Game();
        Assert.Equal("ok", Protocol.Handle("  Console   Spawn Boar 1 ", game));
        Assert.Equal("ok", Protocol.Handle("CONSOLE help", game));
        Assert.Equal(["Spawn Boar 1", "help"], game.Ran);
        Assert.Equal("error: unknown command second line", Protocol.Handle("console bad", game));
        Assert.StartsWith("error: console needs", Protocol.Handle("console", game));
        Assert.StartsWith("error: console needs", Protocol.Handle("console   ", game));
    }

    [Fact]
    public void TokenAndLineFraming()
    {
        Assert.True(Protocol.TokenMatches("abc", "abc"));
        Assert.False(Protocol.TokenMatches("abc", "abd"));
        Assert.False(Protocol.TokenMatches("abc", null));
        Assert.False(Protocol.TokenMatches("abc", "ab"));
        Assert.False(Protocol.TokenMatches("abc", "abcd"));
        Assert.False(Protocol.TokenMatches("abc", ""));
        Assert.Equal("ping", BridgeServer.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("ping\r\n")), 16));
        Assert.Null(BridgeServer.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("pingping\n")), 4));
        Assert.Null(BridgeServer.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("no newline")), 64));
    }
}
