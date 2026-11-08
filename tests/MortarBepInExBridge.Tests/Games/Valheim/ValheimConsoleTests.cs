using Xunit;

namespace MortarBepInExBridge.Tests;

public class ValheimConsoleTests
{
    private sealed class Console
    {
        public static readonly Console instance = new();
        public string? Ran;

        public void TryRunCommand(string text, bool silentFail = false, bool skipAllowedCheck = false) => Ran = text;
    }

    [Fact]
    public void TheConsoleIsValheimsInstanceTryRunCommand()
    {
        IGameConsole console = BuiltGame.Hooks!.Console(name => name == "Console" ? typeof(Console) : null)!;
        Assert.Null(console.Run("Spawn Boar"));
        Assert.Equal("Spawn Boar", Console.instance.Ran);
    }
}
