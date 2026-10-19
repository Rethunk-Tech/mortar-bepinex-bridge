using Xunit;

namespace MortarBepInExBridge.Tests;

public class GameVersionTests
{
    private sealed class Manager
    {
        public static Manager? Instance { get; set; }

        public int version = 1;
    }

    private static readonly GameVersionSource[] Sources = [new("Manager", "Instance", "version", "v{0}", "Awake")];

    private static Type? Find(string name) => name == "Manager" ? typeof(Manager) : null;

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
        GameVersion.Watch(typeof(Manager), Sources[0]);
        var made = new Manager { version = 81 };
        GameVersion.BeforeStartUp(made);
        made.version += 9950;
        Manager.Instance = made;
        Assert.Equal("v81", GameVersion.Read(Find, Sources));
    }

    [Fact]
    public void AGameWithoutASourceHasNoVersionOfItsOwn() =>
        Assert.Null(GameVersion.Read(_ => null, []));
}
