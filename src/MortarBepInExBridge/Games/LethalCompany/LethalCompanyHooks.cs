using System;
using System.Collections.Generic;
using MortarBepInExBridge.Overlay;

namespace MortarBepInExBridge.Games.LethalCompany;

internal sealed class LethalCompanyHooks : IGameHooks
{
    private static readonly (string, object)[] NoBootUp = [("runBootUpScreen", false), ("playColdOpenCinematic", false), ("playColdOpenCinematic2", false)];

    // InitSceneLaunchOptions waits on every start for a click on Online or LAN; LAN needs no Steam session, and the
    // delay lets IngamePlayerSettings finish loading the settings it saves on that choice. Both init scenes then play a
    // boot animation, and on some starts a cold-open cinematic, before MainMenu. sceneLoaded runs after the scene's
    // Awake and before its Start, so the flags Awake set are cleared before Start reads them.
    public static readonly IntroStep[] Steps =
    [
        new("InitSceneLaunchOptions", "PreInitSceneScript", [], "ChooseLaunchOption", [false], 2f, choice: true),
        new("InitScene", "InitializeGame", NoBootUp),
        new("InitSceneLANMode", "InitializeGame", NoBootUp),
    ];

    // The menu shows v81 from GameNetworkManager.gameVersionNum while Application.version is 0.1.
    public static readonly GameVersionSource[] Versions = [new("GameNetworkManager", "Instance", "gameVersionNum", "v{0}", "Awake")];

    public IOverlayProvider? Overlay(Func<string, Type?> find, Func<Type, object?> findObject) => new LethalCompanyProvider(find, findObject);

    public IGameConsole? Console(Func<string, Type?> find) => null;

    public IReadOnlyList<IntroStep> IntroSteps => Steps;

    public IReadOnlyList<GameVersionSource> VersionSources => Versions;
}
