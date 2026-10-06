using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using MortarBepInExBridge.Perf;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortarBepInExBridge;

[BepInPlugin(Guid, "Mortar BepInEx Bridge", Version)]
public sealed class Plugin : BaseUnityPlugin, IGameView
{
    public const string Guid = "Rethunk.MortarBepInExBridge";
    public const string Version = "0.1.0";
    private const string StateFileName = "mortar-bepinex-bridge.json";

    private BridgeServer? Server;
    private string? StatePath;
    private volatile string GameVersionValue = "";
    private volatile bool GameVersionIsGames;
    // Unity objects are main-thread only, so the socket threads read this copy, which the scene event keeps current.
    private volatile string SceneValue = "";

    string IGameView.GameVersion => this.GameVersionValue;

    bool IGameView.GameVersionIsGames => this.GameVersionIsGames;

    string IGameView.Scene => this.SceneValue;

    IReadOnlyList<PluginRow> IGameView.Plugins => Loaded();

    string IGameView.Perf(bool start) => PerfHost.Reply(start);

    private void Awake()
    {
        this.ReadGameVersion();
        this.SceneValue = SceneManager.GetActiveScene().name;
        SceneManager.activeSceneChanged += (_, next) =>
        {
            this.SceneValue = next.name;
            this.ReadGameVersion();
        };
        // Only a real quit stops the server: in Lethal Company, with BepInEx's default HideManagerGameObject=false, the
        // first scene load destroys the manager object and this component with it, while the game runs on.
        Application.quitting += this.Shutdown;
        if (MeasuredLaunch.Requested(Environment.GetEnvironmentVariable))
            PerfHost.Start(this.Logger);
        IntroMode intro = IntroSkip.Requested(Environment.GetEnvironmentVariable, ReadIntroRequest());
        if (intro != IntroMode.Play)
        {
            this.Logger.LogInfo($"Mortar asked this launch to skip the intro ({intro}).");
            IntroRunner.Start(this, this.Logger, intro);
        }

        byte[] raw = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(raw);
        string token = BitConverter.ToString(raw).Replace("-", "").ToLowerInvariant();

        this.Server = new BridgeServer(token, line =>
        {
            this.Logger.LogDebug($"Command received: {line}");
            return Protocol.Handle(line, this);
        });
        this.Server.Start();

        // The config folder is the same for every way of launching the game, unlike the plugin's own folder, which a mod manager picks.
        this.StatePath = System.IO.Path.Combine(Paths.ConfigPath, StateFileName);
        Files.AtomicWrite(this.StatePath, $"{{\"port\":{this.Server.Port},\"token\":\"{token}\"}}");
        this.Logger.LogInfo($"Listening on 127.0.0.1:{this.Server.Port}.");
    }

    private void ReadGameVersion()
    {
        string? own = GameVersion.Read(IntroSkip.Find);
        this.GameVersionValue = own ?? Application.version;
        this.GameVersionIsGames = own != null;
    }

    private static string? ReadIntroRequest()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Paths.BepInExRootPath) ?? "", IntroSkip.RequestFile);
        try
        {
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    private void Shutdown()
    {
        this.Server?.Dispose();
        this.Server = null;
        if (this.StatePath != null)
        {
            try
            {
                System.IO.File.Delete(this.StatePath);
            }
            catch (System.IO.IOException)
            {
            }
            this.StatePath = null;
        }
    }

    private static IReadOnlyList<PluginRow> Loaded()
    {
        var rows = new List<PluginRow>();
        foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            rows.Add(new PluginRow(info.Metadata.GUID, info.Metadata.Name, info.Metadata.Version.ToString()));
        return rows;
    }
}
