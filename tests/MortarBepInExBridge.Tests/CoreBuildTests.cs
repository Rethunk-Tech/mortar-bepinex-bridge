using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace MortarBepInExBridge.Tests;

// Compiled only into the core build (no -p:Game): a game build has hooks by design.
public class CoreBuildTests
{
    [Fact]
    public void TheCoreBuildHasNoGameHooks() => Assert.Null(BuiltGame.Hooks);

    // Read from metadata: loading the types needs UnityEngine and BepInEx, which the plugin references but never ships.
    [Fact]
    public void TheCoreBuildReferencesNoGameTypes()
    {
        using var file = File.OpenRead(typeof(BuiltGame).Assembly.Location);
        using var pe = new PEReader(file);
        MetadataReader md = pe.GetMetadataReader();
        Assert.DoesNotContain(md.TypeDefinitions, h => md.GetString(md.GetTypeDefinition(h).Namespace).StartsWith("MortarBepInExBridge.Games", StringComparison.Ordinal));
    }
}
