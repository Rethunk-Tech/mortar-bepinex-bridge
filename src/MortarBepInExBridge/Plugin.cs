using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using MortarBepInExBridge.Overlay;
using MortarBepInExBridge.Perf;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortarBepInExBridge;

[BepInPlugin(Guid, "Mortar BepInEx Bridge", Version)]
public sealed class Plugin : BaseUnityPlugin, IGameView
{
    public const string Guid = "Rethunk.MortarBepInExBridge";
    public const string Version = "0.2.0";
    private const string StateFileName = "mortar-bepinex-bridge.json";

    private BridgeServer? Server;
    private OverlayServer? Overlay;
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
        GameVersion.Hook(new Harmony(Guid + ".version"), IntroSkip.Find);
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
        IntroMode intro = IntroSkip.Requested(Environment.GetEnvironmentVariable, ReadProfileFile(IntroSkip.RequestFile));
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
        this.StartOverlay();
    }

    private void ReadGameVersion()
    {
        string? own = GameVersion.Read(IntroSkip.Find);
        this.GameVersionValue = own ?? Application.version;
        this.GameVersionIsGames = own != null;
    }

    private readonly Dictionary<Type, UnityEngine.Object> Found = [];

    // Looks a scene object up once and again only after Unity has destroyed it.
    private object? FindObject(Type type)
    {
        if (!this.Found.TryGetValue(type, out UnityEngine.Object? found) || !found)
        {
            found = UnityEngine.Object.FindObjectOfType(type);
            if (found)
                this.Found[type] = found;
            else
                this.Found.Remove(type);
        }
        return found ? found : null;
    }

    // Only when Mortar's stream overlay setting is on, and only for a game that has a provider.
    private void StartOverlay()
    {
        OverlayConfig? config = OverlayConfig.Parse(ReadProfileFile(OverlayConfig.File));
        if (config == null)
            return;
        IOverlayProvider? provider = OverlayProviders.For(Application.productName, IntroSkip.Find, this.FindObject);
        if (provider == null)
        {
            this.Logger.LogWarning($"Stream overlay is on, but there is no overlay for {Application.productName}.");
            return;
        }
        try
        {
            var server = new OverlayServer(config.Port, config.Token);
            server.Start();
            OverlayRunner.Start(server, provider, this.Logger);
            this.Overlay = server;
            this.Logger.LogInfo($"Stream overlay for {provider.Game} on 127.0.0.1:{config.Port}.");
        }
        catch (SocketException ex)
        {
            this.Logger.LogError($"Stream overlay is off: could not bind 127.0.0.1:{config.Port}: {ex.Message}");
        }
    }

    // A request Mortar leaves in the profile folder that holds BepInEx's, which a Steam launch reaches where the environment does not.
    private static string? ReadProfileFile(string relative)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Paths.BepInExRootPath) ?? "", relative);
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
        this.Overlay?.Dispose();
        this.Overlay = null;
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
            rows.Add(new PluginRow(info.Metadata.GUID, info.Metadata.Name, info.Metadata.Version.ToString(), PluginRow.Below(Paths.PluginPath, info.Location ?? "")));
        return rows;
    }
}
