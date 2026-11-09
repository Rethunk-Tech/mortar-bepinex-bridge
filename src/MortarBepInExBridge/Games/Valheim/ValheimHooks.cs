using System;
using System.Collections.Generic;

namespace MortarBepInExBridge.Games.Valheim;

internal sealed class ValheimHooks : IGameHooks
{
    public string ProductName => "Valheim";

    // Console.instance.TryRunCommand(line), the call mods such as Server Devcommands make.
    public IGameConsole? Console(Func<string, Type?> find) => new ReflectionConsole(() => find("Console"), "instance", "TryRunCommand");

    public IReadOnlyList<IntroStep> IntroSteps => [];

    public IReadOnlyList<GameVersionSource> VersionSources => [];
}
