using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using MortarBepInExBridge.Perf;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortarBepInExBridge;

[BepInPlugin(Guid, "Mortar BepInEx Bridge", Version)]
public sealed class Plugin : BaseUnityPlugin, IGameView
{
    public const string Guid = "Rethunk.MortarBepInExBridge";
    public const string Version = "0.7.0";
    private const string StateFileName = "mortar-bepinex-bridge.json";
    private const string HiddenLogSourcesFile = "startup/hidden-log-sources";

    // One DLL serves every game it has a row for, so the row is the one matching this game's Application.productName.
    private IGameHooks? Hooks;

    private BridgeServer? Server;
    // Its patches hold the game's own version as it was before mods changed it, so it is held for the whole process
    // rather than made and released around one hook call.
    private readonly Harmony VersionPatches = new(Guid + ".version");
    private string? StatePath;
    private volatile string GameVersionValue = "";
    private volatile bool GameVersionIsGames;
    // Unity objects are main-thread only, so the socket threads read this copy, which the scene event keeps current.
    private volatile string SceneValue = "";

    string IGameView.GameVersion => this.GameVersionValue;

    bool IGameView.GameVersionIsGames => this.GameVersionIsGames;

    string IGameView.Scene => this.SceneValue;

    IReadOnlyList<PluginRow> IGameView.Plugins => Loaded();

    IReadOnlyList<string> IGameView.Features => Protocol.Features;

    string IGameView.Perf(bool start) => PerfHost.Reply(start);

    bool IGameView.PerfStop() => PerfHost.Stop();

    string IGameView.ConfigSync() => ConfigSyncValues.Read(Probe.Find);

    private const int ConsoleWaitMs = 2000;
    private const int ConfigReloadWaitMs = 5000;
    private readonly MainThreadQueue Main = new();
    private IGameConsole? GameConsole;

    string? IGameView.Console(string line)
    {
        IGameConsole? console = this.GameConsole;
        return console == null
            ? $"{Application.productName} has no console Mortar can reach"
            : this.Main.Invoke(() => console.Run(line), ConsoleWaitMs);
    }

    ConfigReloadResult? IGameView.ConfigReload(string file)
    {
        ConfigReloadResult? result = null;
        string? failure = this.Main.Invoke(() =>
        {
            result = ConfigReload.Run(Paths.ConfigPath, file, LoadedConfigs());
            return null;
        }, ConfigReloadWaitMs);
        return failure == null ? result : null;
    }

    // A ConfigFile shared by several plugins appears once.
    private static List<ConfigTarget> LoadedConfigs()
    {
        var files = Chainloader.PluginInfos.Values
            .Select(info => info.Instance?.Config)
            .OfType<ConfigFile>()
            .Distinct()
            .ToList();
        // CoreConfig (BepInEx.cfg) is not public in the BepInEx.Core package this builds against.
        const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        if (typeof(ConfigFile).GetProperty("CoreConfig", any)?.GetValue(null) is ConfigFile core && !files.Contains(core))
            files.Add(core);
        return files.ConvertAll(file => new ConfigTarget(file.ConfigFilePath, file.Reload));
    }

    private void Awake()
    {
        this.Hooks = BuiltGame.For(Application.productName);
        LogSources.Install(ReadProfileFile(HiddenLogSourcesFile));
        GameVersion.Hook(this.VersionPatches, Probe.Find, Hooks?.VersionSources ?? []);
        this.ReadGameVersion();
        this.SceneValue = SceneManager.GetActiveScene().name;
        SceneManager.activeSceneChanged += (_, next) =>
        {
            this.SceneValue = next.name;
            this.ReadGameVersion();
        };
        // Unity's own null check: a destroyed plugin is not a null reference. The regress reads this line to learn
        // whether the loader kept the plugin alive across scene loads.
        SceneManager.sceneLoaded += (scene, _) =>
            this.Logger.LogInfo($"Bridge plugin alive after scene {scene.name}: {this != null}");
        // Only a real quit stops the server: in Lethal Company, with BepInEx's default HideManagerGameObject=false, the
        // first scene load destroys the manager object and this component with it, while the game runs on.
        Application.quitting += this.Shutdown;
        PerfHost.Start(this.Logger, MeasuredLaunch.Requested(Environment.GetEnvironmentVariable));
        IntroMode intro = IntroSkip.Requested(Environment.GetEnvironmentVariable, ReadProfileFile(IntroSkip.RequestFile));
        if (intro != IntroMode.Play)
        {
            this.Logger.LogInfo($"Mortar asked this launch to skip the intro ({intro}).");
            IntroRunner.Start(this.Logger, intro, Hooks?.IntroSteps ?? []);
        }

        byte[] raw = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(raw);
        string token = BitConverter.ToString(raw).Replace("-", "").ToLowerInvariant();

        this.GameConsole = Hooks?.Console(Probe.Find);
        ConsoleRunner.Start(this.Main, this.Logger);

        this.Server = new BridgeServer(token, line =>
        {
            this.Logger.LogDebug($"Command received: {line}");
            return Protocol.Handle(line, this);
        });
        // The config folder is the same for every way of launching the game, unlike the plugin's own folder, which a mod manager picks.
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
        this.Logger.LogInfo($"Listening on 127.0.0.1:{this.Server.Port}.");
    }

    private void ReadGameVersion()
    {
        string? own = GameVersion.Read(Probe.Find, Hooks?.VersionSources ?? []);
        this.GameVersionValue = own ?? Application.version;
        this.GameVersionIsGames = own != null;
    }

    // A request Mortar leaves in the profile folder that holds BepInEx's, which a Steam launch reaches where the environment does not.
    private string? ReadProfileFile(string relative)
    {
        string path = System.IO.Path.Join(System.IO.Path.GetDirectoryName(Paths.BepInExRootPath) ?? "", relative);
        string? text = Files.ReadText(path, out string? error);
        if (error != null)
            this.Logger.LogWarning($"Could not read {relative}: {error}");
        return text;
    }

    private void Shutdown()
    {
        this.Server?.Dispose();
        this.Server = null;
        if (this.StatePath != null)
        {
            if (Files.TryDelete(this.StatePath) is { } error)
                this.Logger.LogWarning($"Could not remove {StateFileName}: {error}");
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
