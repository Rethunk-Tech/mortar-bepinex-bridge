using Xunit;

namespace MortarBepInExBridge.Tests;

public class FilesTests
{
    [Fact]
    public void ReadTextReturnsNullWhenTheFileIsMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), "mortar-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Null(Files.ReadText(path, out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void ReadTextReportsWhenAPathExistsButIsNotReadable()
    {
        if (OperatingSystem.IsWindows())
            return;
        string dir = Path.Combine(Path.GetTempPath(), "mortar-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "unreadable");
        File.WriteAllText(path, "x");
        File.SetUnixFileMode(path, (UnixFileMode)0);
        try
        {
            Assert.Null(Files.ReadText(path, out string? error));
            Assert.False(string.IsNullOrEmpty(error));
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Directory.Delete(dir, recursive: true);
        }
    }
}
