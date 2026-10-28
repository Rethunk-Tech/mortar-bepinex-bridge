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
    /// <summary>Tells the bridge plugin, in this process only, that the launch is measured, so it measures in game too.</summary>
    /// <summary>The AppDomain slot where the bridge plugin leaves a Func returning each plugin's in-game milliseconds by GUID.</summary>
    internal const string SharesKey = "MortarBepInExBridge.StartupShares";
    internal const string MeasuredEnvVar = "MORTAR_MEASURED_LAUNCH";

    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Mortar Startup");
    private static StartupTimer? timer;
    private static Listener? listener;
    private static DateTime processStart;
    private static string startupDir = "";
    private static string readySignal = "";

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
            string? text = Files.ReadText(request, out string? error);
            if (text == null && error == null)
                return;
            File.Delete(request);
            if (error != null)
            {
                Log.LogWarning($"Could not read {MeasureFile}: {error}");
                return;
            }
            string title = TitleFromRequest(text!);
            readySignal = ReadySignalFromRequest(text!);
            Environment.SetEnvironmentVariable(MeasuredEnvVar, "1");
            processStart = ProcessStart();
            timer = new StartupTimer(Now(), title);
            if (!PatchTiming.Install())
                Log.LogWarning("Patch timing is off for this launch: this Harmony is not the one the bridge binds to.");
            listener = new Listener();
            BepInEx.Logging.Logger.Listeners.Add(listener);
            Log.LogInfo($"Timing this launch's plugins until scene {(title == "" ? "(first)" : title)}.");
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    internal const int MaxTitleChars = 256;

    /// <summary>The title scene Mortar asked for: the first line, trimmed, capped so a request file cannot fill logs
    /// or wait on an unbounded name.</summary>
    internal static string TitleFromRequest(string text)
    {
        string title = text.Trim();
        int nl = title.IndexOfAny(['\r', '\n']);
        if (nl >= 0)
            title = title[..nl].Trim();
        return title.Length > MaxTitleChars ? title[..MaxTitleChars] : title;
    }

    /// <summary>The game's ready signal Mortar asked for: the request's second line, the text of the Unity log message
    /// the game prints when its own loading is done; empty when there is none.</summary>
    internal static string ReadySignalFromRequest(string text)
    {
        string[] lines = text.Split('\n');
        string signal = lines.Length > 1 ? lines[1].Trim() : "";
        return signal.Length > MaxTitleChars ? signal[..MaxTitleChars] : signal;
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
            {
                HookScenes();
                HookReady();
            }
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    // Kept out of Initialize so UnityEngine is first touched once the chainloader runs, on Unity's main thread.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HookScenes() => SceneManager.sceneLoaded += OnSceneLoaded;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HookReady()
    {
        if (readySignal != "")
        {
            Application.logMessageReceived += OnUnityLog;
            timer!.WaitForReady = true;
        }
    }

    // The non-threaded event runs on the main thread, where the report may be written.
    private static void OnUnityLog(string message, string _, LogType __)
    {
        if (message.Trim() == readySignal)
            OnReady();
    }

    private static void OnReady()
    {
        try
        {
            if (timer != null && timer.OnReady(Now()))
                WriteReport();
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        try
        {
            if (timer != null && timer.OnScene(scene.name, Now()))
                WriteReport();
        }
        catch (Exception ex)
        {
            Abandon(ex);
        }
    }

    // Never named Finish: BepInEx's preloader binds any static Finish() as this patcher's finalizer and runs it before
    // Unity has registered its internal calls, so touching Application there broke every Unity call for the session.
    private static void WriteReport()
    {
        StartupTimer done = timer!;
        Stop();
        Dictionary<string, PluginId> plugins = Chainloader.PluginInfos.Values
            .GroupBy(p => p.ToString())
            .ToDictionary(g => g.Key, g => new PluginId(g.First().Metadata.GUID, g.First().Metadata.Name, g.First().Metadata.Version.ToString()));
        string loader = typeof(Chainloader).Assembly.GetName().Version?.ToString() ?? "";
        Dictionary<string, string> byAssembly = [];
        foreach (PluginInfo info in Chainloader.PluginInfos.Values)
        {
            if ((object?)info.Instance is { } instance)
                byAssembly.TryAdd(instance.GetType().Assembly.GetName().Name, info.Metadata.GUID);
        }
        Dictionary<string, long> patches = ByPlugin(PatchTiming.Ms(), byAssembly);
        var shares = AppDomain.CurrentDomain.GetData(SharesKey) as Func<Dictionary<string, long>>;
        string report = done.Report(processStart, loader, Application.version, text => plugins.TryGetValue(text, out PluginId id) ? id : null, patches, shares?.Invoke());
        string path = StartupTimer.Write(startupDir, processStart, report);
        Log.LogInfo($"Startup report written to {path}.");
    }

    internal static Dictionary<string, long> ByPlugin(Dictionary<string, long> byAssembly, Dictionary<string, string> guidOf)
    {
        var sums = new Dictionary<string, long>();
        foreach (KeyValuePair<string, long> p in byAssembly)
        {
            if (guidOf.TryGetValue(p.Key, out string? guid))
                sums[guid] = sums.TryGetValue(guid, out long n) ? n + p.Value : p.Value;
        }
        return sums;
    }

    // Called from a scene load, never from inside a log event, while BepInEx is walking its listeners.
    private static void Stop()
    {
        timer = null;
        if (listener != null)
            BepInEx.Logging.Logger.Listeners.Remove(listener);
        listener = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.logMessageReceived -= OnUnityLog;
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
