using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace MortarBepInExBridge;

// No UnityEngine here: its IL2CPP wrappers are generated from the game at first run, so a game-agnostic build cannot
// compile against them. The scene, game version, console and perf stay empty; plugins and ping are the whole query.
[BepInPlugin(Guid, "Mortar BepInEx Bridge", Version)]
public sealed class Plugin : BasePlugin, IGameView
{
    public const string Guid = "Rethunk.MortarBepInExBridge";
    public const string Version = "0.6.0";
    private const string StateFileName = "mortar-bepinex-bridge.json";

    private BridgeServer? Server;
    private string? StatePath;

    string IGameView.GameVersion => "";

    bool IGameView.GameVersionIsGames => false;

    string IGameView.Scene => "";

    IReadOnlyList<PluginRow> IGameView.Plugins => Loaded();

    string IGameView.Perf(bool start) => "{}";

    bool IGameView.PerfStop() => false;

    string? IGameView.Console(string line) => "this game has no console Mortar can reach";

    string IGameView.ConfigSync() => "[]";

    ConfigReloadResult? IGameView.ConfigReload(string name) => null;

    public override void Load()
    {
        byte[] raw = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(raw);
        string token = BitConverter.ToString(raw).Replace("-", "").ToLowerInvariant();

        this.Server = new BridgeServer(token, line => Protocol.Handle(line, this));
        this.StatePath = System.IO.Path.Join(Paths.ConfigPath, StateFileName);
        this.Server.Start();
        try
        {
            Files.AtomicWrite(this.StatePath, $"{{\"port\":{this.Server.Port},\"token\":\"{token}\"}}");
        }
        catch
        {
            this.Server.Dispose();
            this.Server = null;
            throw;
        }
        // The IL2CPP chainloader gives a plugin no quit event, so the state file is removed when the process exits.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => this.Shutdown();
        this.Log.LogInfo($"Listening on 127.0.0.1:{this.Server.Port}.");
    }

    private void Shutdown()
    {
        this.Server?.Dispose();
        this.Server = null;
        if (this.StatePath != null)
        {
            if (Files.TryDelete(this.StatePath) is { } error)
                this.Log.LogWarning($"Could not remove {StateFileName}: {error}");
            this.StatePath = null;
        }
    }

    private static IReadOnlyList<PluginRow> Loaded()
    {
        var rows = new List<PluginRow>();
        foreach (var info in IL2CPPChainloader.Instance.Plugins.Values)
            rows.Add(new PluginRow(info.Metadata.GUID, info.Metadata.Name, info.Metadata.Version.ToString(), PluginRow.Below(Paths.PluginPath, info.Location ?? "")));
        return rows;
    }
}
