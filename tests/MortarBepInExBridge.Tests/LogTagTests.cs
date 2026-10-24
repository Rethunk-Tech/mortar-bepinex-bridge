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
        Assert.StartsWith($"{nameof(LogTagTests)}::{nameof(TheCallSiteIsTheFirstFrameOutsideTheLogger)}@", Log());

    // Stands in for the logging code, whose frames are skipped.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? Log() => LogTag.CallSite(new StackTrace(0, false), m => m.Name == nameof(Log));

    [Fact]
    public void AHiddenSourceDropsItsLinesForEveryObjectOrOne()
    {
        var hidden = LogTag.ParseHidden("A::B@1\t\r\nC::D@2\tEnv/Ship\nnot a source\n");
        Assert.True(LogTag.Hides(hidden, "A::B@1", null));
        Assert.True(LogTag.Hides(hidden, "A::B@1", "Any/Path"));
        Assert.True(LogTag.Hides(hidden, "C::D@2", "Env/Ship"));
        Assert.False(LogTag.Hides(hidden, "C::D@2", "Env/Other"));
        Assert.False(LogTag.Hides(hidden, "C::D@3", null));
        Assert.False(LogTag.Hides(LogTag.ParseHidden(null), "A::B@1", null));
    }
}
