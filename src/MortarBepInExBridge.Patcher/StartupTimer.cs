using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MortarBepInExBridge;

/// <summary>A plugin as the chainloader's "Loading [Name Version]" line names it, resolved to its GUID.</summary>
internal readonly struct PluginId(string guid, string name, string version)
{
    public string Guid { get; } = guid;
    public string Name { get; } = name;
    public string Version { get; } = version;
}

/// <summary>
/// Turns the chainloader's log lines and the game's scene loads into Mortar's startup report. Every time is milliseconds
/// from process start. A plugin's time is the gap from its "Loading" line to the chainloader's next line about a plugin,
/// which covers its constructor and Awake; whatever it does later, in Start, coroutines or scene hooks, falls in the
/// phase from the first scene to the title scene.
/// </summary>
internal sealed class StartupTimer(long preloaderMs, string titleScene)
{
    public const string Source = "BepInEx";
    public const string Started = "Chainloader started";
    public const string Complete = "Chainloader startup complete";
    private const string Loading = "Loading [";
    private const string ErrorLoading = "Error loading [";

    private const int Kept = 10;

    private readonly List<(string Plugin, long Start, long End)> loads = [];
    private long chainloaderStart;
    private long chainloaderDone;
    private long firstScene;
    private long title;

    /// <summary>One line the chainloader logged at ms. Lines from any other source are some plugin's own.</summary>
    public void OnLog(string source, string message, long ms)
    {
        if (source != Source)
            return;
        if (message == Started)
            this.chainloaderStart = ms;
        else if (message == Complete)
        {
            this.EndLoad(ms);
            this.chainloaderDone = ms;
        }
        else if (message.StartsWith(Loading, StringComparison.Ordinal) && message.EndsWith("]", StringComparison.Ordinal))
        {
            this.EndLoad(ms);
            this.loads.Add((message.Substring(Loading.Length, message.Length - Loading.Length - 1), ms, -1));
        }
        else if (message.StartsWith(ErrorLoading, StringComparison.Ordinal))
            this.EndLoad(ms);
    }

    private void EndLoad(long ms)
    {
        int last = this.loads.Count - 1;
        if (last >= 0 && this.loads[last].End < 0)
            this.loads[last] = (this.loads[last].Plugin, this.loads[last].Start, ms);
    }

    /// <summary>A scene finished loading at ms; true once the title scene (or, for a game without one named, the first
    /// scene) has, when the report is due.</summary>
    public bool OnScene(string name, long ms)
    {
        if (this.firstScene == 0)
            this.firstScene = ms;
        if (titleScene == "" || name == titleScene)
            this.title = ms;
        return this.title > 0;
    }

    /// <summary>The report as Mortar's StartupReport reads it. resolve maps "Name Version" to the plugin, null for one
    /// the chainloader no longer lists, which is then reported under that text.</summary>
    public string Report(DateTime processStart, string loader, string game, Func<string, PluginId?> resolve)
    {
        var mods = new StringBuilder();
        long charged = 0;
        foreach ((string plugin, long start, long end) in this.loads)
        {
            if (end < 0)
                continue;
            PluginId id = resolve(plugin) ?? new PluginId(plugin, plugin, "");
            charged += end - start;
            if (mods.Length > 0)
                mods.Append(',');
            mods.Append("{\"id\":").Append(Json.Quote(id.Guid))
                .Append(",\"name\":").Append(Json.Quote(id.Name))
                .Append(",\"version\":").Append(Json.Quote(id.Version))
                .Append(",\"entryMs\":").Append(end - start).Append('}');
        }
        long other = Math.Max(0, this.title - preloaderMs - charged);
        return new StringBuilder("{\"schema\":1")
            .Append(",\"loader\":").Append(Json.Quote(loader))
            .Append(",\"game\":").Append(Json.Quote(game))
            .Append(",\"processStart\":").Append(Json.Quote(processStart.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)))
            .Append(",\"phases\":{\"bridgeEntry\":").Append(preloaderMs)
            .Append(",\"entryDone\":").Append(this.chainloaderStart)
            .Append(",\"gameLaunched\":").Append(this.chainloaderDone)
            .Append(",\"titleMenu\":").Append(this.firstScene)
            .Append(",\"titleScreen\":").Append(this.title)
            .Append("},\"entryTimed\":true,\"entryMissed\":").Append(this.loads.Count(l => l.End < 0))
            .Append(",\"mods\":[").Append(mods).Append("],\"otherMs\":").Append(other).Append('}')
            .ToString();
    }

    /// <summary>Writes the report named for the process start, keeping the newest few; Mortar's sample files stay.</summary>
    internal static string Write(string dir, DateTime processStart, string report)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, processStart.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + ".json");
        string temp = path + ".tmp";
        File.WriteAllText(temp, report);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
        foreach (string old in Directory.GetFiles(dir, "*.json").Where(f => !f.EndsWith(".samples.json", StringComparison.Ordinal)).OrderByDescending(f => f, StringComparer.Ordinal).Skip(Kept))
            File.Delete(old);
        return path;
    }
}
