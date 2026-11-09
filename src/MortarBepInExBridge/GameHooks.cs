using System;
using System.Collections.Generic;
using System.Linq;
using MortarBepInExBridge.Overlay;

namespace MortarBepInExBridge;

/// <summary>What one game adds to the plugin. Everything names the game's types as strings, so the DLL references no
/// game assembly, and one DLL ships every row in <c>Games/&lt;Game&gt;/</c>.</summary>
internal interface IGameHooks
{
    /// <summary>The Unity <c>Application.productName</c> of the game these hooks serve.</summary>
    string ProductName { get; }

    /// <summary>The overlay values of the game, or null when it has none. <c>findObject</c> finds the one live object of a Unity type.</summary>
    IOverlayProvider? Overlay(Func<string, Type?> find, Func<Type, object?> findObject);

    /// <summary>The game's own command console, or null.</summary>
    IGameConsole? Console(Func<string, Type?> find);

    /// <summary>The scene changes that skip the game's intro.</summary>
    IReadOnlyList<IntroStep> IntroSteps { get; }

    /// <summary>Where the game keeps its own version, when that is not Application.version.</summary>
    IReadOnlyList<GameVersionSource> VersionSources { get; }
}

/// <summary>Names the row a <c>Games/&lt;Game&gt;/</c> folder contributes. The attribute is declared in that game's own
/// folder, so a new game is a folder and no registry in the core to edit.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class GameRowAttribute(Type hooks) : Attribute
{
    public Type Hooks { get; } = hooks;
}

/// <summary>The games this DLL serves, each a <c>Games/&lt;Game&gt;/</c> row. <see cref="For"/> picks one by
/// <c>Application.productName</c> at start-up, so the DLL runs in every game it has a row for and in any other
/// BepInEx 5 Mono game without one.</summary>
internal static class BuiltGame
{
    // Read from the assembly's attributes rather than by scanning its types: scanning loads every type in the plugin,
    // including the ones that need UnityEngine and BepInEx, which a test run holds no copy of.
    public static IReadOnlyList<IGameHooks> All { get; } =
    [
        .. typeof(BuiltGame).Assembly.GetCustomAttributes(typeof(GameRowAttribute), false)
            .Cast<GameRowAttribute>()
            .Select(row => (IGameHooks)Activator.CreateInstance(row.Hooks)!)
            .OrderBy(row => row.ProductName, StringComparer.Ordinal),
    ];

    /// <summary>The row for that product name, or null in a game this DLL has no row for.</summary>
    public static IGameHooks? For(string? productName) =>
        productName == null ? null : All.FirstOrDefault(row => string.Equals(row.ProductName, productName, StringComparison.Ordinal));
}
