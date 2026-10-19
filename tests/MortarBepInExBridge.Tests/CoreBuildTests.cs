using Xunit;

namespace MortarBepInExBridge.Tests;

// Compiled only into the core build (no -p:Game): a game build has hooks by design.
public class CoreBuildTests
{
    [Fact]
    public void TheCoreBuildHasNoGameHooks() => Assert.Null(BuiltGame.Hooks);

    [Fact]
    public void TheCoreBuildReferencesNoGameTypes() =>
        Assert.DoesNotContain(typeof(BuiltGame).Assembly.GetTypes(), t => t.Namespace?.StartsWith("MortarBepInExBridge.Games") == true);
}
