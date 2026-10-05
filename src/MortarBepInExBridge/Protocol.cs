using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MortarBepInExBridge;

/// <summary>One loaded BepInEx plugin.</summary>
internal sealed class PluginRow(string guid, string name, string version)
{
    public string Guid { get; } = guid;
    public string Name { get; } = name;
    public string Version { get; } = version;
}

/// <summary>What the commands report. The plugin implements it over BepInEx and Unity so the protocol needs no game.</summary>
internal interface IGameView
{
    string GameVersion { get; }
    string Scene { get; }
    IReadOnlyList<PluginRow> Plugins { get; }
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
    /// <c>ping</c> answers <c>ok</c> (the same reply the SMAPI bridge gives a queued command), <c>status</c> and <c>plugins</c> answer <c>ok</c> and JSON.
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
                    + ",\"scene\":" + Json.Quote(game.Scene)
                    + ",\"plugins\":" + PluginList(game.Plugins, withName: false) + "}";
            case "plugins":
                return "ok " + PluginList(game.Plugins, withName: true);
            default:
                return "error: unknown command " + Json.Quote(name) + "; this game has no console, try ping, status or plugins";
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
            sb.Append(",\"version\":").Append(Json.Quote(plugins[i].Version)).Append('}');
        }
        return sb.Append(']').ToString();
    }
}
