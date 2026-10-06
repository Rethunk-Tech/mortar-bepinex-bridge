using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MortarBepInExBridge;

/// <summary>One change to a game object when a scene loads: fields set, then a method called, Delay seconds in.</summary>
internal sealed class IntroStep(string scene, string type, (string Field, object Value)[] fields, string? method = null, object[]? args = null, float delay = 0)
{
    public string Scene { get; } = scene;
    public string Type { get; } = type;
    public (string Field, object Value)[] Fields { get; } = fields;
    public string? Method { get; } = method;
    public object[]? Args { get; } = args;
    public float Delay { get; } = delay;
}

/// <summary>
/// Takes a test launch straight to the main menu. Only Mortar's sandbox and regress runs set MORTAR_SKIP_INTRO=1 in the
/// game's environment, so a player's own launch never skips anything. Steps name a game's scenes and types as strings,
/// so the plugin references no game assembly, and a game holding none of the named types is untouched.
/// </summary>
internal static class IntroSkip
{
    public const string EnvVar = "MORTAR_SKIP_INTRO";

    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly (string, object)[] NoBootUp = [("runBootUpScreen", false), ("playColdOpenCinematic", false), ("playColdOpenCinematic2", false)];

    // Lethal Company. InitSceneLaunchOptions waits on every start for a click on Online or LAN; LAN needs no Steam
    // session, and the delay lets IngamePlayerSettings finish loading the settings it saves on that choice. Both init
    // scenes then play a boot animation, and on some starts a cold-open cinematic, before MainMenu. sceneLoaded runs
    // after the scene's Awake and before its Start, so the flags Awake set are cleared before Start reads them.
    public static readonly IntroStep[] Steps =
    [
        new("InitSceneLaunchOptions", "PreInitSceneScript", [], "ChooseLaunchOption", [false], 2f),
        new("InitScene", "InitializeGame", NoBootUp),
        new("InitSceneLANMode", "InitializeGame", NoBootUp),
    ];

    public static bool Requested(Func<string, string?> env) => env(EnvVar) == "1";

    public static IEnumerable<IntroStep> For(string scene) => Steps.Where(s => s.Scene == scene);

    /// <summary>The loaded type of that full name, or null in a game that has none.</summary>
    public static Type? Find(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);

    public static void Apply(object target, IntroStep step)
    {
        Type type = target.GetType();
        foreach ((string field, object value) in step.Fields)
            type.GetField(field, Members)?.SetValue(target, value);
        if (step.Method != null)
            type.GetMethod(step.Method, Members)?.Invoke(target, step.Args);
    }
}
