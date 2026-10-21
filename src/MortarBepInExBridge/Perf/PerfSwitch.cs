namespace MortarBepInExBridge.Perf;

/// <summary>Whether in-game measuring is on: the socket threads flip it and the main thread reads it each frame, so it
/// holds no Unity state.</summary>
internal sealed class PerfSwitch
{
    private volatile bool active;
    private volatile bool failed;
    private volatile bool restart;
    private volatile string summary = PerfReport.Unmeasured;

    public bool Active => this.active;

    public string Summary
    {
        set => this.summary = value;
    }

    public void Start()
    {
        this.active = true;
        this.restart = true;
    }

    /// <summary>The <c>perf</c> reply; start turns measuring on, or restarts the window when it already is.</summary>
    public string Reply(bool start)
    {
        if (start && !this.failed)
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

    /// <summary>A measuring failure turns it off for the rest of the launch.</summary>
    public void Fail()
    {
        this.failed = true;
        this.active = false;
    }
}
