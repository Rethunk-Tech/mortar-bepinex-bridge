using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace MortarBepInExBridge.Tests;

// One DLL serves every game: its rows are all compiled in and picked at run time, so there is no core build to check
// hooks against.
public class CoreBuildTests
{
    [Fact]
    public void OneDllServesEachGameByItsProductName()
    {
        Assert.Equal("Lethal Company", BuiltGame.For("Lethal Company")?.ProductName);
        Assert.Equal("Valheim", BuiltGame.For("Valheim")?.ProductName);
        Assert.Null(BuiltGame.For("Stardew Valley"));
        Assert.Null(BuiltGame.For(null));
    }

    // Intro skip, console and the game version all read game members through Probe, so it has to stay in the plugin's
    // own namespace rather than move in with the feature that first needed it.
    [Fact]
    public void TheProbeHelperBelongsToNeitherOverlayNorIntroSkip()
    {
        Assert.Equal("MortarBepInExBridge", typeof(Probe).Namespace);
        Assert.Null(typeof(IntroSkip).GetMethod(nameof(Probe.Find)));
    }
}
