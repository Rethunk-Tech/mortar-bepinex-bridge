using System;
using System.Globalization;
using System.Reflection;

namespace MortarBepInExBridge;

/// <summary>A game whose own version is not Application.version: the static instance that holds it, the field, and how
/// the game shows it.</summary>
internal sealed class GameVersionSource(string type, string instance, string field, string format)
{
    public string Type { get; } = type;
    public string Instance { get; } = instance;
    public string Field { get; } = field;
    public string Format { get; } = format;
}

/// <summary>
/// The version the game shows the player. Application.version is the Unity project's bundleVersion, which a game may
/// never set (Lethal Company's is 0.1 while its menu shows v81 from GameNetworkManager.gameVersionNum). Sources name
/// types as strings like <see cref="IntroSkip"/>, so a game holding none of them reports Application.version.
/// </summary>
internal static class GameVersion
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static readonly GameVersionSource[] Sources =
    [
        new("GameNetworkManager", "Instance", "gameVersionNum", "v{0}"),
    ];

    /// <summary>The version from the first source the game holds an instance of, else null. The instance appears only
    /// once the game's own start-up has made it, so the plugin asks again on each scene change.</summary>
    public static string? Read(Func<string, Type?> find)
    {
        foreach (GameVersionSource source in Sources)
        {
            Type? type = find(source.Type);
            if (type is null)
                continue;
            object? instance = type.GetProperty(source.Instance, Static)?.GetValue(null) ?? type.GetField(source.Instance, Static)?.GetValue(null);
            object? value = instance is null ? null : type.GetField(source.Field, Members)?.GetValue(instance);
            if (value is not null)
                return string.Format(CultureInfo.InvariantCulture, source.Format, value);
        }
        return null;
    }
}
