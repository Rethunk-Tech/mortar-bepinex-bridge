using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MortarBepInExBridge;

/// <summary>How far a launch takes the game past its start-up on its own.</summary>
internal enum IntroMode
{
    /// <summary>The game's start-up as it is.</summary>
    Play,

    /// <summary>Boot animations and cold opens skipped; every start-up choice stays the player's.</summary>
    Animations,

    /// <summary>Also answers the start-up choices, so a launch nobody watches reaches the main menu.</summary>
    Menu,
}

/// <summary>One change to a game object when a scene loads: fields set, then a method called, Delay seconds in. A step
/// that answers a choice for the player runs only in <see cref="IntroMode.Menu"/>.</summary>
internal sealed class IntroStep(string scene, string type, (string Field, object Value)[] fields, string? method = null, object[]? args = null, float delay = 0, bool choice = false)
{
    public string Scene { get; } = scene;
    public bool Choice { get; } = choice;
    public string Type { get; } = type;
    public (string Field, object Value)[] Fields { get; } = fields;
    public string? Method { get; } = method;
    public object[]? Args { get; } = args;
    public float Delay { get; } = delay;
}

/// <summary>
/// Skips the game's intro when Mortar asks: the player's "Skip the intro" setting writes "intro" to RequestFile, and a
/// crash check writes "menu". MORTAR_SKIP_INTRO=1 in the game's environment, which Mortar's sandbox and regress runs set,
/// is "menu" too. The request is a file because a Steam launch starts the game from Steam's own process, which never
/// sees Mortar's environment. Without a request nothing changes. Steps name a game's scenes and types as strings, so
/// the plugin references no game assembly, and a game holding none of the named types is untouched.
/// </summary>
internal static class IntroSkip
{
    public const string EnvVar = "MORTAR_SKIP_INTRO";

    /// <summary>The request, relative to the profile folder that holds BepInEx's.</summary>
    public const string RequestFile = "startup/skip-intro";

    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly (string, object)[] NoBootUp = [("runBootUpScreen", false), ("playColdOpenCinematic", false), ("playColdOpenCinematic2", false)];

    // Lethal Company. InitSceneLaunchOptions waits on every start for a click on Online or LAN; LAN needs no Steam
    // session, and the delay lets IngamePlayerSettings finish loading the settings it saves on that choice. Both init
    // scenes then play a boot animation, and on some starts a cold-open cinematic, before MainMenu. sceneLoaded runs
    // after the scene's Awake and before its Start, so the flags Awake set are cleared before Start reads them.
    public static readonly IntroStep[] Steps =
    [
        new("InitSceneLaunchOptions", "PreInitSceneScript", [], "ChooseLaunchOption", [false], 2f, choice: true),
        new("InitScene", "InitializeGame", NoBootUp),
        new("InitSceneLANMode", "InitializeGame", NoBootUp),
    ];

    /// <summary>The mode asked for, from the environment and the request file's text (null when there is none).</summary>
    public static IntroMode Requested(Func<string, string?> env, string? request)
    {
        if (env(EnvVar) == "1")
            return IntroMode.Menu;
        return request?.Trim() switch
        {
            "menu" => IntroMode.Menu,
            "intro" => IntroMode.Animations,
            _ => IntroMode.Play,
        };
    }

    public static IEnumerable<IntroStep> For(string scene, IntroMode mode) =>
        Steps.Where(s => s.Scene == scene && mode != IntroMode.Play && (!s.Choice || mode == IntroMode.Menu));

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
