using System;
using System.Collections.Generic;
using MortarBepInExBridge.Overlay;

namespace MortarBepInExBridge.Games.Valheim;

internal sealed class ValheimHooks : IGameHooks
{
    public IOverlayProvider? Overlay(Func<string, Type?> find, Func<Type, object?> findObject) => new ValheimProvider(find);

    // Console.instance.TryRunCommand(line), the call mods such as Server Devcommands make.
    public IGameConsole? Console(Func<string, Type?> find) => new ReflectionConsole(() => find("Console"), "instance", "TryRunCommand");

    public IReadOnlyList<IntroStep> IntroSteps => [];

    public IReadOnlyList<GameVersionSource> VersionSources => [];
}
