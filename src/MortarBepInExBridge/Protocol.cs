using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MortarBepInExBridge;

/// <summary>One loaded BepInEx plugin. Location is its DLL's path below the plugins folder, with forward slashes, so a
/// client can tell which of two copies of one GUID and version BepInEx kept; "" when it lies elsewhere.</summary>
internal sealed class PluginRow(string guid, string name, string version, string location = "")
{
    public string Guid { get; } = guid;
    public string Name { get; } = name;
    public string Version { get; } = version;
    public string Location { get; } = location;

    /// <summary>path below pluginsDir with forward slashes, or "" for a path outside it.</summary>
    public static string Below(string pluginsDir, string path)
    {
        string root = pluginsDir.Replace('\\', '/').TrimEnd('/') + "/";
        string file = path.Replace('\\', '/');
        return file.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? file[root.Length..] : "";
    }
}

/// <summary>What the commands report. The plugin implements it over BepInEx and Unity so the protocol needs no game.</summary>
internal interface IGameView
{
    string GameVersion { get; }

    /// <summary>Whether GameVersion is the version the game itself shows, rather than Unity's Application.version.</summary>
    bool GameVersionIsGames { get; }

    string Scene { get; }
    IReadOnlyList<PluginRow> Plugins { get; }

    /// <summary>The in-game performance summary as JSON; start restarts its window first.</summary>
    string Perf(bool start);
}

/// <summary>The command protocol: Mortar sends <c>token\ncommand\n</c> and reads one reply line, <c>ok</c>, <c>ok {json}</c> or <c>error: message</c>.</summary>
internal static class Protocol
{
    public const int MaxCommandBytes = 4096;
    public const int MaxTokenBytes = 128;

    public static bool TokenMatches(string expected, string? provided)
    {
        return provided != null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }

    /// <summary>
    /// Run one command line. The game has no command console, so the commands are queries:
    /// <c>ping</c> answers <c>ok</c> (the same reply the SMAPI bridge gives a queued command); <c>status</c>, <c>plugins</c>,
    /// <c>perf</c> and <c>perf start</c> answer <c>ok</c> and JSON.
    /// </summary>
    public static string Handle(string line, IGameView game)
    {
        string name = line.Trim().ToLowerInvariant();
        if (name.Length == 0)
            return "error: empty command";

        switch (name)
        {
            case "ping":
                return "ok";
            case "status":
                return "ok {"
                    + "\"gameVersion\":" + Json.Quote(game.GameVersion)
                    + ",\"gameVersionSource\":" + Json.Quote(game.GameVersionIsGames ? "game" : "unity")
                    + ",\"scene\":" + Json.Quote(game.Scene)
                    + ",\"plugins\":" + PluginList(game.Plugins, withName: false) + "}";
            case "plugins":
                return "ok " + PluginList(game.Plugins, withName: true);
            case "perf":
                return "ok " + game.Perf(start: false);
            case "perf start":
                return "ok " + game.Perf(start: true);
            default:
                return "error: unknown command " + Json.Quote(name) + "; this game has no console, try ping, status, plugins, perf or perf start";
        }
    }

    private static string PluginList(IReadOnlyList<PluginRow> plugins, bool withName)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < plugins.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append("{\"guid\":").Append(Json.Quote(plugins[i].Guid));
            if (withName)
                sb.Append(",\"name\":").Append(Json.Quote(plugins[i].Name));
            sb.Append(",\"version\":").Append(Json.Quote(plugins[i].Version));
            if (plugins[i].Location != "")
                sb.Append(",\"location\":").Append(Json.Quote(plugins[i].Location));
            sb.Append('}');
        }
        return sb.Append(']').ToString();
    }
}
