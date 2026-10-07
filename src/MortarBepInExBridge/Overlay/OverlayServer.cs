using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MortarBepInExBridge.Overlay;

/// <summary>Loopback-only HTTP: <c>GET /state</c> answers the last snapshot as JSON, behind a token in an
/// <c>Authorization: Bearer</c> header or a <c>token</c> query parameter. It reads nothing from the game and has no path
/// to the command channel; the poll loop hands it finished JSON.</summary>
internal sealed class OverlayServer(int port, string token) : IDisposable
{
    private const int MaxLineBytes = 4096;
    private const int ClientTimeoutMs = 5000;
    public const string NotInGame = "{\"inGame\":false}";

    private readonly TcpListener Listener = new(IPAddress.Loopback, port);
    private readonly string Token = token;
    private volatile string Snapshot = NotInGame;
    private volatile bool Stopped;

    public int Port => ((IPEndPoint)this.Listener.LocalEndpoint).Port;

    public void Set(string json) => this.Snapshot = json;

    public void Start()
    {
        this.Listener.Start();
        new Thread(this.AcceptLoop) { IsBackground = true, Name = "MortarBepInExBridgeOverlay" }.Start();
    }

    public void Dispose()
    {
        this.Stopped = true;
        this.Listener.Stop();
    }

    private void AcceptLoop()
    {
        while (!this.Stopped)
        {
            TcpClient client;
            try
            {
                client = this.Listener.AcceptTcpClient();
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            ThreadPool.QueueUserWorkItem(_ => this.Serve(client));
        }
    }

    private void Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = ClientTimeoutMs;
                client.SendTimeout = ClientTimeoutMs;
                NetworkStream stream = client.GetStream();
                byte[] reply = Encoding.UTF8.GetBytes(this.Respond(stream));
                stream.Write(reply, 0, reply.Length);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }

    private string Respond(Stream stream)
    {
        string? request = BridgeServer.ReadLine(stream, MaxLineBytes);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while (request != null && (line = BridgeServer.ReadLine(stream, MaxLineBytes)) is { Length: > 0 })
        {
            int colon = line.IndexOf(':');
            if (colon > 0)
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return this.Answer(request, headers);
    }

    internal string Answer(string? request, IReadOnlyDictionary<string, string> headers)
    {
        string[] parts = (request ?? "").Split([' '], 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return Response(400, "{\"error\":\"bad request\"}");
        if (parts[0] != "GET")
            return Response(405, "{\"error\":\"method not allowed\"}");
        string target = parts[1];
        int query = target.IndexOf('?');
        if ((query < 0 ? target : target[..query]) != "/state")
            return Response(404, "{\"error\":\"not found\"}");
        if (!Protocol.TokenMatches(this.Token, TokenOf(query < 0 ? "" : target[(query + 1)..], headers)))
            return Response(401, "{\"error\":\"unauthorized\"}", "WWW-Authenticate: Bearer\r\n");
        return Response(200, this.Snapshot);
    }

    private static string? TokenOf(string query, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue("Authorization", out string? auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..].Trim();
        foreach (string pair in query.Split(['&'], StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0 && Uri.UnescapeDataString(pair[..eq]) == "token")
                return Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
        }
        return null;
    }

    private static string Response(int status, string body, string extraHeaders = "")
    {
        string reason = status switch { 200 => "OK", 400 => "Bad Request", 401 => "Unauthorized", 404 => "Not Found", 405 => "Method Not Allowed", _ => "Error" };
        // Allow-Origin: the page OBS opens is a file:// URL, which fetches this loopback address cross-origin.
        return $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *\r\n{extraHeaders}Connection: close\r\n\r\n{body}";
    }
}
