using System.Xml.Linq;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class VersionTests
{
    [Fact]
    public void PluginVersionEqualsTheCsprojVersion()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Join(dir, "MortarBepInExBridge.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var csproj = XDocument.Load(Path.Join(dir, "src", "MortarBepInExBridge", "MortarBepInExBridge.csproj"));
        Assert.Equal(Plugin.Version, csproj.Descendants("Version").Single().Value);
    }
}
