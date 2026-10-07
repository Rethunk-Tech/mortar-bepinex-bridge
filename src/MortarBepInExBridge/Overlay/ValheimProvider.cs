using System;
using System.Collections.Generic;

namespace MortarBepInExBridge.Overlay;

/// <summary>Valheim: player, biome, in-game day and bosses defeated.</summary>
internal sealed class ValheimProvider(Func<string, Type?> find) : IOverlayProvider
{
    // The world's global keys, in the order the bosses are met.
    private static readonly (string Key, string Name)[] Bosses =
    [
        ("defeated_eikthyr", "Eikthyr"),
        ("defeated_gdking", "The Elder"),
        ("defeated_bonemass", "Bonemass"),
        ("defeated_dragon", "Moder"),
        ("defeated_goblinking", "Yagluth"),
        ("defeated_queen", "The Queen"),
        ("defeated_fader", "Fader"),
    ];

    public string Game => "valheim";

    public Dictionary<string, object?>? Read()
    {
        object? player = Probe.Static(find("Player"), "m_localPlayer");
        if (player == null)
            return null;

        var defeated = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        object? zone = Probe.Static(find("ZoneSystem"), "instance");
        foreach (object? key in Probe.Items(Probe.Call(zone, "GetGlobalKeys") ?? Probe.Member(zone, "m_globalKeys")) ?? Array.Empty<object>())
        {
            if (key != null)
                keys.Add(key.ToString() ?? "");
        }
        foreach ((string key, string name) in Bosses)
        {
            if (keys.Contains(key))
                defeated.Add(name);
        }

        return new Dictionary<string, object?>
        {
            ["playerName"] = Probe.Text(Probe.Call(player, "GetPlayerName")),
            ["biome"] = Probe.Text(Probe.Call(player, "GetCurrentBiome")),
            ["day"] = Probe.Int(Probe.Call(Probe.Static(find("EnvMan"), "instance"), "GetDay")),
            ["bossesDefeated"] = defeated,
            ["bossCount"] = defeated.Count,
        };
    }
}
