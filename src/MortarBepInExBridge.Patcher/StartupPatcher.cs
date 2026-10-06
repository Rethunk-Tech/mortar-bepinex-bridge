using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortarBepInExBridge;

/// <summary>
/// BepInEx preloader patcher that times each plugin's load on a launch Mortar asked to measure. It patches no
/// assembly: a patcher is the only code that runs before the chainloader, so its log listener sees every plugin's
/// "Loading" line, including those of plugins that load before the bridge plugin. An unmeasured launch costs one
/// missing-file check.
/// </summary>
public static class StartupPatcher
{
    /// <summary>Mortar writes it in the profile's startup folder for a measured launch, holding the game's title
    /// scene name; the launch consumes it.</summary>
    internal const string MeasureFile = ".measure-launch";

    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Mortar Startup");
    private static StartupTimer? timer;
    private static Listener? listener;
    private static DateTime processStart;
    private static string startupDir = "";

    public static IEnumerable<string> TargetDLLs { get; } = [];

    public static void Patch(AssemblyDefinition _)
    {
    }

    public static void Initialize()
    {
        try
        {
            // BepInEx's root is the profile's BepInEx folder; the profile folder above it holds Mortar's startup folder.
            startupDir = Path.Combine(Path.GetDirectoryName(Paths.BepInExRootPath) ?? "", "startup");
            string request = Path.Combine(startupDir, MeasureFile);
            if (!File.Exists(request))
                return;
            string title = File.ReadAllText(request).Trim();
            File.Delete(request);
            processStart = ProcessStart();
            timer = new StartupTimer(Now(), title);
            listener = new Listener();
            BepInEx.Logging.Logger.Listeners.Add(listener);
            Log.LogInfo($"Timing this launch's plugins until scene {(title == "" ? "(first)" : title)}.");
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    private static DateTime ProcessStart()
    {
        try
        {
            return Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return DateTime.UtcNow;
        }
    }

    private static long Now() => (long)(DateTime.UtcNow - processStart).TotalMilliseconds;

    private static void OnLog(LogEventArgs e)
    {
        if (timer == null)
            return;
        try
        {
            string message = e.Data?.ToString() ?? "";
            timer.OnLog(e.Source.SourceName, message, Now());
            if (e.Source.SourceName == StartupTimer.Source && message == StartupTimer.Started)
                HookScenes();
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    // Kept out of Initialize so UnityEngine is first touched once the chainloader runs, on Unity's main thread.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HookScenes() => SceneManager.sceneLoaded += OnSceneLoaded;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        try
        {
            if (timer != null && timer.OnScene(scene.name, Now()))
                Finish();
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    private static void Finish()
    {
        StartupTimer done = timer!;
        Stop();
        Dictionary<string, PluginId> plugins = Chainloader.PluginInfos.Values
            .GroupBy(p => p.ToString())
            .ToDictionary(g => g.Key, g => new PluginId(g.First().Metadata.GUID, g.First().Metadata.Name, g.First().Metadata.Version.ToString()));
        string loader = typeof(Chainloader).Assembly.GetName().Version?.ToString() ?? "";
        string report = done.Report(processStart, loader, Application.version, text => plugins.TryGetValue(text, out PluginId id) ? id : null);
        string path = StartupTimer.Write(startupDir, processStart, report);
        Log.LogInfo($"Startup report written to {path}.");
    }

    // Called from a scene load, never from inside a log event, while BepInEx is walking its listeners.
    private static void Stop()
    {
        timer = null;
        if (listener != null)
            BepInEx.Logging.Logger.Listeners.Remove(listener);
        listener = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // A timing failure must never reach the game. The hooks stay but do nothing: one may be running from inside
    // BepInEx's walk over its listeners, which removing it would break.
    private static void Abandon(Exception ex)
    {
        timer = null;
        Log.LogWarning($"Startup timing is off for this launch: {ex.Message}");
    }

    private sealed class Listener : ILogListener
    {
        public void LogEvent(object sender, LogEventArgs eventArgs) => OnLog(eventArgs);

        public void Dispose()
        {
        }
    }
}
