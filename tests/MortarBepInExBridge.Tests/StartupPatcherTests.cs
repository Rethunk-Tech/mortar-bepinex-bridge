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
