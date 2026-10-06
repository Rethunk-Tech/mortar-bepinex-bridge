using System;
using System.Collections;
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

    public static void Start(ManualLogSource log)
    {
        var host = new GameObject("MortarBridgeIntroSkip") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        host.AddComponent<IntroRunner>().log = log;
    }

    private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;

    private void OnDestroy() => SceneManager.sceneLoaded -= this.OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        foreach (IntroStep step in IntroSkip.For(scene.name))
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
