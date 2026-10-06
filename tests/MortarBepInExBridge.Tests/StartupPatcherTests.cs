using System.Reflection;
using Xunit;

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
}
