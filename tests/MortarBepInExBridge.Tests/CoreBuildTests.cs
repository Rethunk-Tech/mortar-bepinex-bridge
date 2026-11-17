using System.Reflection;
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

    // The stream overlay left the companion for its own mod; a namespace left behind would ship its socket server again,
    // and the 0.5.x companion and that mod must never both bind the overlay port.
    [Fact]
    public void TheDllHasNoOverlayNamespace()
    {
        using var file = File.OpenRead(typeof(BuiltGame).Assembly.Location);
        using var pe = new PEReader(file);
        MetadataReader md = pe.GetMetadataReader();
        Assert.DoesNotContain(md.TypeDefinitions, h => md.GetString(md.GetTypeDefinition(h).Namespace).StartsWith("MortarBepInExBridge.Overlay", StringComparison.Ordinal));
    }

    // The plugin compiles against UnityEngine.Modules 6000 while the games it serves are older (Lethal Company is on
    // 2022.3), so a Unity 6-only member compiles here and throws MissingMethodException in the game. 2022.3 has only
    // Object.FindObjectsByType(Type, FindObjectsSortMode); the single-argument overload arrived in Unity 6.
    [Fact]
    public void TheDllCallsNoFindObjectsByTypeOverloadTheGamesLack()
    {
        using var file = File.OpenRead(typeof(BuiltGame).Assembly.Location);
        using var pe = new PEReader(file);
        MetadataReader md = pe.GetMetadataReader();
        List<MemberReference> calls = [.. md.MemberReferences
            .Select(md.GetMemberReference)
            .Where(m => m.Parent.Kind == HandleKind.TypeReference)
            .Where(m =>
            {
                TypeReference type = md.GetTypeReference((TypeReferenceHandle)m.Parent);
                return md.GetString(type.Namespace) == "UnityEngine" && md.GetString(type.Name) == "Object" && md.GetString(m.Name) == "FindObjectsByType";
            })];
        Assert.NotEmpty(calls);
        Assert.DoesNotContain(calls, m => ArgumentCount(md, m) == 1);
    }

    private static int ArgumentCount(MetadataReader md, MemberReference call)
    {
        BlobReader blob = md.GetBlobReader(call.Signature);
        SignatureHeader header = blob.ReadSignatureHeader();
        if (header.IsGeneric)
            blob.ReadCompressedInteger();
        return blob.ReadCompressedInteger();
    }

    // Intro skip, console and the game version all read game members through Probe, so it has to stay in the plugin's
    // own namespace rather than move in with the feature that first needed it.
    [Fact]
    public void TheProbeHelperBelongsToNeitherOverlayNorIntroSkip()
    {
        Assert.Equal("MortarBepInExBridge", typeof(Probe).Namespace);
        Assert.Null(typeof(IntroSkip).GetMethod(nameof(Probe.Find)));
        const BindingFlags declared = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Assert.Null(typeof(Probe).GetMethod("Member", declared));
        Assert.Null(typeof(Probe).GetMethod("Call", declared));
        Assert.Null(typeof(Probe).GetMethod("Int", declared));
    }
}
