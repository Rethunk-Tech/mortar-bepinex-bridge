using System;
using BepInEx.Logging;
using UnityEngine;

namespace MortarBepInExBridge.Overlay;

/// <summary>Reads the provider on Unity's main thread twice a second and hands the server the finished JSON. It lives on
/// an object of its own, as <see cref="IntroRunner"/> does, because the plugin's object is not always kept alive.</summary>
internal sealed class OverlayRunner : MonoBehaviour
{
    private const float IntervalSeconds = 0.5f;

    private OverlayServer? server;
    private IOverlayProvider? provider;
    private ManualLogSource? log;
    private float next;
    private string lastError = "";

    public static void Start(OverlayServer server, IOverlayProvider provider, ManualLogSource log)
    {
        var host = new GameObject("MortarBridgeOverlay") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        var runner = host.AddComponent<OverlayRunner>();
        runner.server = server;
        runner.provider = provider;
        runner.log = log;
    }

    private void Update()
    {
        if (Time.unscaledTime < this.next || this.server == null || this.provider == null)
            return;
        this.next = Time.unscaledTime + IntervalSeconds;
        this.server.Set(OverlayProviders.Snapshot(this.provider, ex =>
        {
            if (ex.Message != this.lastError)
                this.log?.LogWarning($"Overlay read failed: {ex.Message}");
            this.lastError = ex.Message;
        }));
    }
}
