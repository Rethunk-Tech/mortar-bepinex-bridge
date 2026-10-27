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
        if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return "";
        string relative = file[root.Length..];
        int depth = 0;
        foreach (string part in relative.Split('/'))
        {
            if (part.Length == 0 || part == ".")
                continue;
            if (part == "..")
            {
                if (--depth < 0)
                    return "";
                continue;
            }
            depth++;
        }
        return relative;
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

    /// <summary>Runs a line in the game's own console; null when the game took it, else why it did not.</summary>
    string? Console(string line);

    /// <summary>The host's values of the settings Mortar-ConfigSync shares, as a JSON array.</summary>
    string ConfigSync();
}

/// <summary>The command protocol: Mortar sends <c>token\ncommand\n</c> and reads one reply line, <c>ok</c>, <c>ok {json}</c> or <c>error: message</c>.</summary>
internal static class Protocol
{
    public const int MaxCommandBytes = 4096;
    public const int MaxTokenBytes = 128;

    public static bool TokenMatches(string expected, string? provided)
    {
        if (provided == null)
            return false;
        byte[] left = Encoding.UTF8.GetBytes(expected);
        byte[] right = Encoding.UTF8.GetBytes(provided);
        // Unity's Mono throws when the spans differ in length; net10 returns false.
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    /// <summary>
    /// Run one command line. Most commands are queries:
    /// <c>ping</c> answers <c>ok</c> (the same reply the SMAPI bridge gives a queued command); <c>status</c>, <c>plugins</c>,
    /// <c>perf</c>, <c>perf start</c> and <c>configsync</c> answer <c>ok</c> and JSON; <c>console &lt;line&gt;</c> runs the line in the game's console and answers <c>ok</c>.
    /// </summary>
    public static string Handle(string line, IGameView game)
    {
        string name = line.Trim().ToLowerInvariant();
        if (name.Length == 0)
            return "error: empty command";
        if (name == "console" || name.StartsWith("console ", StringComparison.Ordinal))
        {
            // The argument keeps its case: a game's console is case-sensitive about names and values.
            string text = line.Trim()["console".Length..].Trim();
            if (text.Length == 0)
                return "error: console needs a command to run";
            string? failure = game.Console(text);
            return failure == null ? "ok" : "error: " + failure.Replace('\n', ' ').Replace('\r', ' ');
        }

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
            case "configsync":
                try
                {
                    return "ok " + game.ConfigSync();
                }
                // The game thread can add a shared setting mid-read; the next query reads it whole.
                catch (InvalidOperationException ex)
                {
                    return "error: " + ex.Message;
                }
            default:
                return "error: unknown command " + Json.Quote(name) + "; try ping, status, plugins, perf, perf start, configsync or console <line>";
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
