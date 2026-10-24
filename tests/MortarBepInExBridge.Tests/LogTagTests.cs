using System.Diagnostics;
using System.Runtime.CompilerServices;
using MortarBepInExBridge;
using Xunit;

public class LogTagTests
{
    [Fact]
    public void TheTagNamesTheCallSiteAndTheObject()
    {
        Assert.Equal("", LogTag.Of(null, null));
        Assert.Equal("⟦src=Game.Round::Start@1a⟧ ", LogTag.Of("Game.Round::Start@1a", null));
        // A separator or closing mark inside a name cannot end the tag early.
        Assert.Equal("⟦src=A::B@0;obj=Env/Ship,1⟧ ", LogTag.Of("A::B@0", "Env/Ship;1⟧"));
    }

    [Fact]
    public void TheCallSiteIsTheFirstFrameOutsideTheLogger() =>
        Assert.Equal($"{nameof(LogTagTests)}::{nameof(TheCallSiteIsTheFirstFrameOutsideTheLogger)}", Log());

    // Stands in for the logging code, whose frames are skipped.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? Log() => LogTag.CallSite(new StackTrace(0, false), m => m.Name == nameof(Log));

    [Fact]
    public void AHiddenSourceDropsItsLinesByTextAndObject()
    {
        var hidden = LogTag.ParseHidden("A::B\tCould not build #\t\r\nC::D\t#\tEnv/Ship\nnot a source\n");
        Assert.True(LogTag.Hides(hidden, "A::B", () => "Could not build 3", null));
        Assert.False(LogTag.Hides(hidden, "A::B", () => "Built 3", null));
        Assert.True(LogTag.Hides(hidden, "C::D", () => "1", "Env/Ship"));
        Assert.False(LogTag.Hides(hidden, "C::D", () => "1", "Env/Other"));
        Assert.False(LogTag.Hides(hidden, "E::F", () => throw new InvalidOperationException("formatted a line no rule names"), null));
        Assert.Equal("[Dissonance:Recording] (#) Started", LogTag.Mask("[Dissonance:Recording] (16:34:55.653) Started\nmore"));
    }
}
