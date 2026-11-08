using Xunit;

namespace MortarBepInExBridge.Tests;

public class ConfigReloadTests
{
    [Theory]
    [InlineData("a.cfg")]
    [InlineData("Sub/thing.CFG")]
    [InlineData("Sub\\thing.cfg")]
    [InlineData("my mod.cfg")]
    public void AcceptsConfigFilesBelowTheFolder(string name) => Assert.Null(ConfigReload.Validate(name));

    [Theory]
    [InlineData("")]
    [InlineData("/etc/a.cfg")]
    [InlineData("\\a.cfg")]
    [InlineData("C:/a.cfg")]
    [InlineData("../a.cfg")]
    [InlineData("Sub/../../a.cfg")]
    [InlineData("Sub\\..\\a.cfg")]
    [InlineData("a.json")]
    [InlineData("cfg")]
    [InlineData("a.cfg.bak")]
    public void RefusesAnythingElse(string name) => Assert.NotNull(ConfigReload.Validate(name));

    [Theory]
    [InlineData("/g/BepInEx/config", "a.cfg", "/g/BepInEx/config/a.cfg", true)]
    [InlineData("/g/BepInEx/config/", "Sub/a.cfg", "/g/BepInEx/config/Sub/A.CFG", true)]
    [InlineData("/g/BepInEx/config", "Sub/a.cfg", "/g/BepInEx/config/./Sub/../Sub/a.cfg", true)]
    [InlineData("/g/BepInEx/config", "a.cfg", "/g/BepInEx/config/Sub/a.cfg", false)]
    [InlineData("/g/BepInEx/config", "a.cfg", "/g/BepInEx/config/b.cfg", false)]
    public void MatchesByFullPathIgnoringCase(string dir, string name, string loaded, bool owns) =>
        Assert.Equal(owns, ConfigReload.Owns(dir, name, loaded));

    [Fact]
    public void ReloadsOwnersOnlyAndReportsAFailureWithoutStoppingTheRest()
    {
        var reloaded = new List<string>();
        ConfigTarget Ok(string path) => new(path, () => reloaded.Add(path));
        var targets = new[]
        {
            Ok("/g/c/other.cfg"),
            Ok("/g/c/mod.cfg"),
            new ConfigTarget("/g/c/MOD.cfg", () => throw new InvalidOperationException("bad\nvalue")),
            Ok("/g/c/mod.cfg"),
        };
        ConfigReloadResult result = ConfigReload.Run("/g/c", "mod.cfg", targets);
        Assert.Equal(2, result.Reloaded);
        Assert.Equal(["mod.cfg: bad value"], result.Failures);
        Assert.Equal(["/g/c/mod.cfg", "/g/c/mod.cfg"], reloaded);
        Assert.Equal("ok reloaded 2, failed 1: mod.cfg: bad value", ConfigReload.Reply(result));
    }

    [Fact]
    public void NoOwnerIsNotAnError() =>
        Assert.Equal("ok reloaded 0", ConfigReload.Reply(ConfigReload.Run("/g/c", "mod.cfg", [])));
}
