using System.Threading;
using Xunit;

namespace MortarBepInExBridge.Tests;

public class GameConsoleTests
{
    public sealed class FakeConsole
    {
        public static FakeConsole? instance;
        public static readonly List<string> Lines = [];

        public void TryRunCommand(string text, bool silentFail = false, bool skipAllowedCheck = false)
        {
            if (text == "throw")
                throw new InvalidOperationException("nope");
            Lines.Add($"{text}|{silentFail}|{skipAllowedCheck}");
        }
    }

    public sealed class NoMethod
    {
        public static NoMethod? instance = new();
    }

    [Fact]
    public void ReflectionConsoleCallsTheMethodWithItsDefaults()
    {
        FakeConsole.Lines.Clear();
        var console = new ReflectionConsole(() => typeof(FakeConsole), "instance", "TryRunCommand");
        FakeConsole.instance = null;
        Assert.Equal("the game's console is not open yet", console.Run("help"));
        FakeConsole.instance = new FakeConsole();
        Assert.Null(console.Run("Spawn Boar"));
        Assert.Equal(["Spawn Boar|False|False"], FakeConsole.Lines);
        Assert.Equal("nope", console.Run("throw"));
    }

    [Fact]
    public void ReflectionConsoleSaysWhatIsMissing()
    {
        Assert.Equal("this game's console is not loaded", new ReflectionConsole(() => null, "instance", "X").Run("a"));
        Assert.Equal(
            "the game's console has no TryRunCommand(string) method",
            new ReflectionConsole(() => typeof(NoMethod), "instance", "TryRunCommand").Run("a"));
    }

    [Fact]
    public void TheMainThreadRunsQueuedWorkAndTimesOutWithoutOne()
    {
        var queue = new MainThreadQueue();
        Assert.Equal("the game did not run the command in time", queue.Invoke(() => null, 20));
        queue.Drain();

        string? result = "unset";
        var caller = new Thread(() => result = queue.Invoke(() => "from main", 5000));
        caller.Start();
        while (result == "unset" && caller.IsAlive)
        {
            queue.Drain();
            Thread.Sleep(1);
        }
        caller.Join();
        Assert.Equal("from main", result);
    }
}
