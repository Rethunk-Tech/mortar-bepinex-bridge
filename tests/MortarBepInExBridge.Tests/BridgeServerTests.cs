using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class BridgeServerTests
{
    private static string Exchange(BridgeServer server, string payload)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, server.Port);
        client.ReceiveTimeout = 5000;
        var stream = client.GetStream();
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        stream.Write(bytes, 0, bytes.Length);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadLine() ?? "";
    }

    private static BridgeServer Started(Func<string, string> handle)
    {
        var server = new BridgeServer("secret", handle);
        server.Start();
        return server;
    }

    [Fact]
    public void RightTokenReachesTheHandler()
    {
        using var server = Started(line => "ok " + line);
        Assert.Equal("ok ping", Exchange(server, "secret\nping\n"));
    }

    [Fact]
    public void WrongTokenIsRefusedBeforeTheCommandIsRead()
    {
        bool called = false;
        using var server = Started(_ =>
        {
            called = true;
            return "ok";
        });
        Assert.Equal("error: unauthorized", Exchange(server, "wrong\nping\n"));
        Assert.False(called);
    }

    [Fact]
    public void OversizedCommandIsRefused()
    {
        using var server = Started(_ => "ok");
        string reply = Exchange(server, "secret\n" + new string('x', Protocol.MaxCommandBytes + 10) + "\n");
        Assert.Equal("error: missing or oversized command", reply);
    }
}
