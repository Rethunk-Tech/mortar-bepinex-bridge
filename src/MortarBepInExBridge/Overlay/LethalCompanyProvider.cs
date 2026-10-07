using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MortarBepInExBridge.Overlay;

/// <summary>Lethal Company: player, moon, crew, day, quota and ship credits. <c>findObject</c> finds the terminal, which
/// exists only on the ship.</summary>
internal sealed class LethalCompanyProvider(Func<string, Type?> find, Func<Type, object?> findObject) : IOverlayProvider
{
    private static readonly Regex MoonNumber = new(@"^\d+\s+", RegexOptions.Compiled);

    public string Game => "lethal-company";

    public Dictionary<string, object?>? Read()
    {
        object? round = Probe.Static(find("StartOfRound"), "Instance");
        object? local = Probe.Member(round, "localPlayerController");
        if (round == null || local == null)
            return null;

        int total = 0;
        int alive = 0;
        foreach (object? player in Probe.Items(Probe.Member(round, "allPlayerScripts")) ?? Array.Empty<object>())
        {
            bool controlled = Probe.Bool(Probe.Member(player, "isPlayerControlled"));
            bool dead = Probe.Bool(Probe.Member(player, "isPlayerDead"));
            if (controlled || dead)
                total++;
            if (controlled && !dead)
                alive++;
        }

        object? time = Probe.Static(find("TimeOfDay"), "Instance");
        string? moon = Probe.Text(Probe.Member(Probe.Member(round, "currentLevel"), "PlanetName"));
        return new Dictionary<string, object?>
        {
            ["playerName"] = Probe.Text(Probe.Member(local, "playerUsername")),
            ["moon"] = moon == null ? null : MoonNumber.Replace(moon, ""),
            ["crewAlive"] = alive,
            ["crewTotal"] = total,
            ["day"] = Probe.Int(Probe.Member(Probe.Member(round, "gameStats"), "daysSpent")),
            ["quota"] = Probe.Int(Probe.Member(time, "profitQuota")),
            ["quotaProgress"] = Probe.Int(Probe.Member(time, "quotaFulfilled")),
            ["daysLeft"] = Probe.Int(Probe.Member(time, "daysUntilDeadline")),
            ["credits"] = Probe.Int(Probe.Member(find("Terminal") is { } terminal ? findObject(terminal) : null, "groupCredits")),
        };
    }
}
