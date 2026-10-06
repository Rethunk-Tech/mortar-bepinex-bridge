using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace MortarBepInExBridge;

/// <summary>A game whose own version is not Application.version: the static instance that holds it, the field, how the
/// game shows it, and the instance's first Unity message, before which no plugin's patch has run on it.</summary>
internal sealed class GameVersionSource(string type, string instance, string field, string format, string startUp)
{
    public string Type { get; } = type;
    public string Instance { get; } = instance;
    public string Field { get; } = field;
    public string Format { get; } = format;
    public string StartUp { get; } = startUp;
}

/// <summary>
/// The version the game shows the player. Application.version is the Unity project's bundleVersion, which a game may
/// never set (Lethal Company's is 0.1 while its menu shows v81 from GameNetworkManager.gameVersionNum). Sources name
/// types as strings like <see cref="IntroSkip"/>, so a game holding none of them reports Application.version.
/// Mods change the field to keep modded lobbies apart (MoreCompany's Awake postfix adds 9950, so v81 reads v10031), so
/// the value is taken in a prefix on the start-up message: Unity has deserialized the field by then, and every prefix
/// runs before any postfix whatever the plugins' load order.
/// </summary>
internal static class GameVersion
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static readonly GameVersionSource[] Sources =
    [
        new("GameNetworkManager", "Instance", "gameVersionNum", "v{0}", "Awake"),
    ];

    private static readonly ConcurrentDictionary<Type, GameVersionSource> Watched = new();
    private static readonly ConcurrentDictionary<Type, object> AtStartUp = new();

    /// <summary>Patches each source the game holds to keep its field's value from before mods touch it; the count
    /// patched.</summary>
    public static int Hook(Harmony harmony, Func<string, Type?> find)
    {
        var prefix = new HarmonyMethod(typeof(GameVersion).GetMethod(nameof(BeforeStartUp), BindingFlags.Static | BindingFlags.NonPublic)) { priority = Priority.First };
        int hooked = 0;
        foreach (GameVersionSource source in Sources)
        {
            Type? type = find(source.Type);
            MethodInfo? startUp = type?.GetMethod(source.StartUp, Members | BindingFlags.DeclaredOnly);
            if (type is null || startUp is null)
                continue;
            Watch(type, source);
            harmony.Patch(startUp, prefix: prefix);
            hooked++;
        }
        return hooked;
    }

    internal static void Watch(Type type, GameVersionSource source) => Watched[type] = source;

    internal static void BeforeStartUp(object __instance)
    {
        Type type = __instance.GetType();
        if (AtStartUp.ContainsKey(type) || !Watched.TryGetValue(type, out GameVersionSource? source))
            return;
        object? value = type.GetField(source.Field, Members)?.GetValue(__instance);
        if (value is not null)
            AtStartUp.TryAdd(type, value);
    }

    /// <summary>The version from the first source the game holds an instance of, else null: as it was at start-up when
    /// <see cref="Hook"/> saw that, else as it is now. The instance appears only once the game's own start-up has made
    /// it, so the plugin asks again on each scene change.</summary>
    public static string? Read(Func<string, Type?> find)
    {
        foreach (GameVersionSource source in Sources)
        {
            Type? type = find(source.Type);
            if (type is null)
                continue;
            if (AtStartUp.TryGetValue(type, out object? own))
                return string.Format(CultureInfo.InvariantCulture, source.Format, own);
            object? instance = type.GetProperty(source.Instance, Static)?.GetValue(null) ?? type.GetField(source.Instance, Static)?.GetValue(null);
            object? value = instance is null ? null : type.GetField(source.Field, Members)?.GetValue(instance);
            if (value is not null)
                return string.Format(CultureInfo.InvariantCulture, source.Format, value);
        }
        return null;
    }
}
