using System;
using System.Collections.Generic;
using MortarBepInExBridge.Overlay;

namespace MortarBepInExBridge;

/// <summary>What one game adds to the core plugin. The core build has none; a game build (<c>-p:Game=LethalCompany</c>)
/// compiles <c>Games/&lt;Game&gt;/</c> and registers its hooks through <see cref="BuiltGame"/>. Everything names the
/// game's types as strings, so no build references a game assembly.</summary>
internal interface IGameHooks
{
    /// <summary>The overlay values of the game, or null when it has none. <c>findObject</c> finds the one live object of a Unity type.</summary>
    IOverlayProvider? Overlay(Func<string, Type?> find, Func<Type, object?> findObject);

    /// <summary>The game's own command console, or null.</summary>
    IGameConsole? Console(Func<string, Type?> find);

    /// <summary>The scene changes that skip the game's intro.</summary>
    IReadOnlyList<IntroStep> IntroSteps { get; }

    /// <summary>Where the game keeps its own version, when that is not Application.version.</summary>
    IReadOnlyList<GameVersionSource> VersionSources { get; }
}

/// <summary>The game this build was made for. A game build's <c>Games/&lt;Game&gt;/</c> folder implements <see cref="Register"/>.</summary>
internal static partial class BuiltGame
{
    public static IGameHooks? Hooks { get; } = Create();

    private static IGameHooks? Create()
    {
        IGameHooks? hooks = null;
        Register(ref hooks);
        return hooks;
    }

    static partial void Register(ref IGameHooks? hooks);
}
