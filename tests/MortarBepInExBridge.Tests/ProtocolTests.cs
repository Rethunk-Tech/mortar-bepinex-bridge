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
        public IReadOnlyList<PluginRow> Plugins { get; } = [new("a.b", "A \"quoted\"", "1.2.3"), new("c.d", "C", "0.1.0")];

        public string Perf(bool start) => start ? "{\"started\":1}" : "{\"frames\":2}";
    }

    [Theory]
    [InlineData("ping", "ok")]
    [InlineData(" PING ", "ok")]
    [InlineData("status", "ok {\"gameVersion\":\"v62\",\"gameVersionSource\":\"game\",\"scene\":\"SampleSceneRelay\",\"plugins\":[{\"guid\":\"a.b\",\"version\":\"1.2.3\"},{\"guid\":\"c.d\",\"version\":\"0.1.0\"}]}")]
    [InlineData("plugins", "ok [{\"guid\":\"a.b\",\"name\":\"A \\\"quoted\\\"\",\"version\":\"1.2.3\"},{\"guid\":\"c.d\",\"name\":\"C\",\"version\":\"0.1.0\"}]")]
    [InlineData("perf", "ok {\"frames\":2}")]
    [InlineData("PERF START", "ok {\"started\":1}")]
    [InlineData("", "error: empty command")]
    public void Commands(string line, string reply) => Assert.Equal(reply, Protocol.Handle(line, new Game()));

    [Fact]
    public void UnknownCommandNamesTheOnesThatExist()
    {
        string reply = Protocol.Handle("give money", new Game());
        Assert.StartsWith("error: unknown command \"give money\"", reply);
        Assert.Contains("ping, status, plugins, perf or perf start", reply);
    }

    [Fact]
    public void TokenAndLineFraming()
    {
        Assert.True(Protocol.TokenMatches("abc", "abc"));
        Assert.False(Protocol.TokenMatches("abc", "abd"));
        Assert.False(Protocol.TokenMatches("abc", null));
        Assert.Equal("ping", BridgeServer.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("ping\r\n")), 16));
        Assert.Null(BridgeServer.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("pingping\n")), 4));
        Assert.Null(BridgeServer.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("no newline")), 64));
    }
}
