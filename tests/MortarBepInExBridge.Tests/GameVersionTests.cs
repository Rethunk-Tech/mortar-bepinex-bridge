using Xunit;

namespace MortarBepInExBridge.Tests;

public class GameVersionTests
{
    private sealed class Manager
    {
        public static Manager? Instance { get; set; }

        public int version = 1;
    }

    // Its own type, because a start-up value once kept stays for that type and would leak into the other tests.
    private sealed class StartedManager
    {
        public static StartedManager? Instance { get; set; }

        public int version = 1;
    }

    private static readonly GameVersionSource[] Sources = [new("Manager", "Instance", "version", "v{0}", "Awake")];

    private static readonly GameVersionSource[] StartedSources = [new("StartedManager", "Instance", "version", "v{0}", "Awake")];

    private static Type? Find(string name) => name switch
    {
        "Manager" => typeof(Manager),
        "StartedManager" => typeof(StartedManager),
        _ => null,
    };

    [Fact]
    public void AGameReportsItsOwnVersionOnceItHasMadeTheInstance()
    {
        Manager.Instance = null;
        Assert.Null(GameVersion.Read(Find, Sources));
        Manager.Instance = new Manager { version = 81 };
        Assert.Equal("v81", GameVersion.Read(Find, Sources));
    }

    [Fact]
    public void AVersionAModChangesAfterStartUpReadsAsTheGameSetIt()
    {
        GameVersion.Watch(typeof(StartedManager), StartedSources[0]);
        var made = new StartedManager { version = 81 };
        GameVersion.BeforeStartUp(made);
        made.version += 9950;
        StartedManager.Instance = made;
        Assert.Equal("v81", GameVersion.Read(Find, StartedSources));
    }

    [Fact]
    public void AGameWithoutASourceHasNoVersionOfItsOwn() =>
        Assert.Null(GameVersion.Read(_ => null, []));
}
