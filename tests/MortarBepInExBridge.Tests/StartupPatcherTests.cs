extern alias Patcher;
using System.Reflection;
using Xunit;
using StartupPatcher = Patcher::MortarBepInExBridge.StartupPatcher;

namespace MortarBepInExBridge.Tests;

public class StartupPatcherTests
{
    // BepInEx 5's AssemblyPatcher binds these by name, case-insensitively and whatever their visibility, and calls
    // Initialize and Finish from the preloader, before Unity's internal calls exist.
    [Fact]
    public void BepInExFindsOnlyTheHooksThePatcherMeansToExpose()
    {
        string[] hooks = ["Initialize", "Finish", "Patch", "get_TargetDLLs"];
        string[] bound = [.. typeof(StartupPatcher)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Select(m => m.Name)
            .Where(n => hooks.Contains(n, StringComparer.InvariantCultureIgnoreCase))
            .Order(StringComparer.Ordinal)];
        Assert.Equal(["Initialize", "Patch", "get_TargetDLLs"], bound);
    }

    [Fact]
    public void ConsumeRequestLeavesAFileItCouldNotRead()
    {
        string dir = Path.Join(Path.GetTempPath(), "mortar-measure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Join(dir, ".measure-launch");
        try
        {
            File.WriteAllBytes(path, new byte[Files.MaxTextBytes + 1]);
            Assert.Null(StartupPatcher.ConsumeRequest(path, out string? error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TitleFromRequestKeepsTheFirstLineAndCapsIt()
    {
        Assert.Equal("MainMenu", StartupPatcher.TitleFromRequest("  MainMenu\nTiming is off\n"));
        Assert.Equal(new string('a', StartupPatcher.MaxTitleChars), StartupPatcher.TitleFromRequest(new string('a', StartupPatcher.MaxTitleChars + 8)));
        Assert.Equal("", StartupPatcher.TitleFromRequest("   "));
    }

    [Fact]
    public void ReadySignalFromRequestIsTheSecondLine()
    {
        Assert.Equal("BaseMainMenuScreen: OnEnter()", StartupPatcher.ReadySignalFromRequest("title\nBaseMainMenuScreen: OnEnter()\n"));
        Assert.Equal("", StartupPatcher.ReadySignalFromRequest("MainMenu"));
    }
}
