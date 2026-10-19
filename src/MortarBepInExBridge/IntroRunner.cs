using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortarBepInExBridge;

/// <summary>
/// Runs the intro-skip steps as scenes load. It lives on an object of its own that scene loads and asset unloads leave
/// alone, because the plugin's own object is not always: a game or loader that destroys it would otherwise leave
/// sceneLoaded calling StartCoroutine on a dead component, and the intro would stay up.
/// </summary>
internal sealed class IntroRunner : MonoBehaviour
{
    private ManualLogSource? log;
    private Plugin? plugin;
    private static readonly TimeSpan SplashWait = TimeSpan.FromSeconds(30);

    private IntroMode mode;
    private IReadOnlyList<IntroStep> steps = [];
    private volatile bool sceneLoaded;
    private volatile int splashAsks;
    private volatile string? splashError;
    private bool splashAsked;

    public static void Start(Plugin plugin, ManualLogSource log, IntroMode mode, IReadOnlyList<IntroStep> steps)
    {
        var host = new GameObject("MortarBridgeIntroSkip") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        var runner = host.AddComponent<IntroRunner>();
        runner.plugin = plugin;
        runner.log = log;
        runner.mode = mode;
        runner.steps = steps;
        Action? stop = IntroSkip.SplashStop(IntroSkip.Find);
        if (stop != null)
        {
            runner.splashAsked = true;
            new Thread(() => runner.StopSplash(stop)) { IsBackground = true, Name = "MortarBridgeSplash" }.Start();
        }
    }

    // The engine draws its splash screen while the first scene loads and runs no script frame until that scene is in,
    // so nothing on the main thread can stop it. The engine's stop call is the one Unity call made off the main thread
    // here: it is a static request the engine accepts from any thread, it does nothing until the splash is up, and so
    // it is repeated until the first scene has loaded or SplashWait has passed.
    private void StopSplash(Action stop)
    {
        try
        {
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (!this.sceneLoaded && waited.Elapsed < SplashWait)
            {
                stop();
                this.splashAsks++;
                Thread.Sleep(10);
            }
        }
        catch (Exception e)
        {
            this.splashError = (e.InnerException ?? e).GetType().Name;
        }
    }

    private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;

    private void OnDestroy() => SceneManager.sceneLoaded -= this.OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        // Unity's own null check: a destroyed plugin is not a null reference. The regress reads this line to learn
        // whether the loader kept the plugin alive across scene loads.
        this.log?.LogInfo($"Bridge plugin alive after scene {scene.name}: {this.plugin != null}");
        if (this.splashAsked && !this.sceneLoaded)
            this.log?.LogInfo($"Intro skip: asked the engine to stop its splash screen {this.splashAsks} times before scene {scene.name} (error: {this.splashError ?? "none"}).");
        this.sceneLoaded = true;
        foreach (IntroStep step in IntroSkip.For(this.steps, scene.name, this.mode))
            this.StartCoroutine(this.RunStep(step));
    }

    // A step without a delay runs before the coroutine first yields, inside sceneLoaded, so ahead of the scene's Start.
    private IEnumerator RunStep(IntroStep step)
    {
        if (step.Delay > 0)
            yield return new WaitForSeconds(step.Delay);
        Type? type = IntroSkip.Find(step.Type);
        if (type == null)
            yield break;
        foreach (UnityEngine.Object target in FindObjectsOfType(type))
        {
            IntroSkip.Apply(target, step);
            this.log?.LogInfo($"Intro skip: {step.Type} in {step.Scene}.");
        }
    }
}
