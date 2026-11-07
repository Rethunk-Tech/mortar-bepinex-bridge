extern alias Patcher;
using Xunit;
using CodeBaseFix = Patcher::MortarBepInExBridge.CodeBaseFix;

namespace MortarBepInExBridge.Tests;

public class CodeBaseFixTests
{
    [Fact]
    public void ACodeBaseKeepsANonAsciiFolderAndEscapedOneEncodesIt()
    {
        string location = "/g/Mörtar Test ✓/Lethal Company/Lethal Company_Data/Managed/Assembly-CSharp.dll";
        Assert.Equal("file:///g/Mörtar Test ✓/Lethal Company/Lethal Company_Data/Managed/Assembly-CSharp.dll", CodeBaseFix.FromLocation(location, escaped: false));
        Assert.Equal("file:///g/M%C3%B6rtar%20Test%20%E2%9C%93/Lethal%20Company/Lethal%20Company_Data/Managed/Assembly-CSharp.dll", CodeBaseFix.FromLocation(location, escaped: true));
    }

    [Fact]
    public void AWineDrivePathBecomesAFileUri()
    {
        Assert.Equal("file:///Z:/g/a.dll", CodeBaseFix.FromLocation(@"Z:\g\a.dll", escaped: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/a.dll")]
    public void NoUsableLocationLeavesTheFailureAlone(string? location)
    {
        Assert.Null(CodeBaseFix.FromLocation(location, escaped: false));
    }
}
