extern alias Patcher;
using Xunit;
using PatchTimer = Patcher::MortarBepInExBridge.PatchTimer;

namespace MortarBepInExBridge.Tests;

public class PatchTimerTests
{
    [Fact]
    public void ThePatchCallIsTheFirstFrameOutsideHarmonyAndTheRuntime()
    {
        string?[] frames = ["MortarBepInExBridge.Patcher", "0Harmony", "MonoMod.Utils", "mscorlib", "System.Core", "Alpha.Plugin", "BepInEx"];
        Assert.Equal("Alpha.Plugin", PatchTimer.Caller(frames));
        Assert.Null(PatchTimer.Caller(["0Harmony", null, "UnityEngine.CoreModule"]));
    }

    [Fact]
    public void TicksAddUpPerAssemblyAndAnUnknownCallerIsDropped()
    {
        var timer = new PatchTimer();
        timer.Charge("Alpha", 500);
        timer.Charge("Alpha", 1500);
        timer.Charge(null, 9000);
        Assert.Equal(new Dictionary<string, long> { ["Alpha"] = 2000 }, timer.Ms(1000));
    }
}
