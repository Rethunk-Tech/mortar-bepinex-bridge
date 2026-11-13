using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    internal const int MaxClients = 8;
    internal const int UnauthTimeoutMs = 250;
    internal const int TokenDeadlineMs = 1000;

    private readonly TcpListener Listener = new(IPAddress.Loopback, 0);
    private readonly string Token = token;
    private readonly Func<string, string> HandleLine = handle;
    private int Active;
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
            if (!TryAdmit(ref this.Active, client))
                continue;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    this.Serve(client);
                }
                finally
                {
                    Interlocked.Decrement(ref this.Active);
                }
            });
        }
    }

    internal static bool TryAdmit(ref int active, TcpClient client)
    {
        if (Interlocked.Increment(ref active) <= MaxClients)
            return true;
        Interlocked.Decrement(ref active);
        client.Close();
        return false;
    }

    private void Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = UnauthTimeoutMs;
                client.SendTimeout = ClientTimeoutMs;
                var stream = client.GetStream();
                byte[] reply = Encoding.UTF8.GetBytes(this.Process(client, stream) + "\n");
                stream.Write(reply, 0, reply.Length);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // The client left before its reply. A connection carries one command, so there is nothing to answer and
                // nothing to keep; the accept loop takes the next one.
            }
        }
    }

    private string Process(TcpClient client, Stream stream)
    {
        if (!Protocol.TokenMatches(this.Token, ReadLine(stream, Protocol.MaxTokenBytes, TokenDeadlineMs)))
            return "error: unauthorized";
        client.ReceiveTimeout = ClientTimeoutMs;
        string? line = ReadLine(stream, Protocol.MaxCommandBytes, ClientTimeoutMs);
        if (line == null)
            return "error: missing or oversized command";
        try
        {
            return this.HandleLine(line);
        }
        catch (Exception ex)
        {
            // An exception escaping a pool thread ends the game process; a failed query only fails its own reply.
            return "error: " + ex.Message.Replace('\n', ' ').Replace('\r', ' ');
        }
    }

    /// <summary>Read one newline-terminated line of at most <paramref name="max"/> bytes within <paramref name="deadlineMs"/>; null when the peer stops early, sends more or trickles bytes past the deadline.</summary>
    internal static string? ReadLine(Stream stream, int max, int deadlineMs)
    {
        var bytes = new List<byte>();
        var clock = Stopwatch.StartNew();
        int b;
        while ((b = stream.ReadByte()) >= 0)
        {
            if (clock.ElapsedMilliseconds > deadlineMs)
                return null;
            if (b == '\n')
                return Encoding.UTF8.GetString([.. bytes]).TrimEnd('\r');
            if (bytes.Count >= max)
                return null;
            bytes.Add((byte)b);
        }
        return null;
    }
}
