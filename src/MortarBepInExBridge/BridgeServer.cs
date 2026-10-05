using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MortarBepInExBridge;

/// <summary>Loopback-only TCP server: one connection per command, the token line first, then the command line.</summary>
internal sealed class BridgeServer(string token, Func<string, string> handle) : IDisposable
{
    private const int ClientTimeoutMs = 5000;

    private readonly TcpListener Listener = new(IPAddress.Loopback, 0);
    private readonly string Token = token;
    private readonly Func<string, string> HandleLine = handle;
    private volatile bool Stopped;

    public int Port => ((IPEndPoint)this.Listener.LocalEndpoint).Port;

    public void Start()
    {
        this.Listener.Start();
        new Thread(this.AcceptLoop) { IsBackground = true, Name = "MortarBepInExBridge" }.Start();
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
                var stream = client.GetStream();
                byte[] reply = Encoding.UTF8.GetBytes(this.Process(stream) + "\n");
                stream.Write(reply, 0, reply.Length);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }

    private string Process(Stream stream)
    {
        if (!Protocol.TokenMatches(this.Token, ReadLine(stream, Protocol.MaxTokenBytes)))
            return "error: unauthorized";
        string? line = ReadLine(stream, Protocol.MaxCommandBytes);
        return line == null ? "error: missing or oversized command" : this.HandleLine(line);
    }

    /// <summary>Read one newline-terminated line of at most <paramref name="max"/> bytes; null when the peer stops early or sends more.</summary>
    internal static string? ReadLine(Stream stream, int max)
    {
        var bytes = new List<byte>();
        int b;
        while ((b = stream.ReadByte()) >= 0)
        {
            if (b == '\n')
                return Encoding.UTF8.GetString([.. bytes]).TrimEnd('\r');
            if (bytes.Count >= max)
                return null;
            bytes.Add((byte)b);
        }
        return null;
    }
}
