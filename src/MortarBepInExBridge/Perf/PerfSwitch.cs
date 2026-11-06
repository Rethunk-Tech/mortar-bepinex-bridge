namespace MortarBepInExBridge.Perf;

/// <summary>Whether in-game measuring is on: the socket threads flip it and the main thread reads it each frame, so it
/// holds no Unity state.</summary>
internal sealed class PerfSwitch
{
    private volatile bool active;
    private volatile string? failed;
    private volatile bool restart;
    private volatile bool stopped;
    private volatile string summary = PerfReport.Unmeasured;

    public bool Active => this.active;

    /// <summary>True between <see cref="Stop"/> and the next <see cref="Start"/>: the main thread then adds no frames.</summary>
    public bool Stopped => this.stopped;

    public string Summary
    {
        set => this.summary = value;
    }

    public void Start()
    {
        this.stopped = false;
        this.active = true;
        this.restart = true;
    }

    /// <summary>Freezes the window and its last summary; false when measuring never started. Harmony stays patched, since
    /// unpatching while a patched method runs is unsafe.</summary>
    public bool Stop()
    {
        if (!this.active)
            return false;
        this.stopped = true;
        return true;
    }

    /// <summary>The <c>perf</c> reply; start turns measuring on, or restarts the window when it already is.</summary>
    public string Reply(bool start)
    {
        if (this.failed != null)
            return "{\"measured\":false,\"reason\":" + Json.Quote(this.failed) + "}";
        if (start)
            this.Start();
        if (!this.active)
            return PerfReport.Unmeasured;
        return start ? "{\"measured\":true}" : this.summary;
    }

    /// <summary>True once per <see cref="Start"/>; only the main thread takes it.</summary>
    public bool TakeRestart()
    {
        if (!this.restart)
            return false;
        this.restart = false;
        return true;
    }

    /// <summary>A measuring failure turns it off for the rest of the launch, and the reply says why.</summary>
    public void Fail(string reason)
    {
        this.failed = reason;
        this.active = false;
    }
}
