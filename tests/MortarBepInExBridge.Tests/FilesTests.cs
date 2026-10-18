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

    [Fact]
    public void ReadTextRejectsAFileLargerThanTheCap()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mortar-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "huge");
        try
        {
            File.WriteAllBytes(path, new byte[Files.MaxTextBytes + 1]);
            Assert.Null(Files.ReadText(path, out string? error));
            Assert.Equal($"file exceeds {Files.MaxTextBytes} bytes", error);
            File.WriteAllBytes(path, new byte[Files.MaxTextBytes]);
            Assert.NotNull(Files.ReadText(path, out error));
            Assert.Null(error);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AtomicWriteRemovesTheTempFileWhenReplaceFails()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mortar-atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "target");
        Directory.CreateDirectory(path);
        try
        {
            Assert.ThrowsAny<IOException>(() => Files.AtomicWrite(path, "x"));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryDeleteTreatsAMissingPathAsSuccess()
    {
        string path = Path.Combine(Path.GetTempPath(), "mortar-gone-" + Guid.NewGuid().ToString("N"));
        Assert.Null(Files.TryDelete(path));
    }

    [Fact]
    public void TryDeleteReportsWhenTheFileCannotBeRemoved()
    {
        if (OperatingSystem.IsWindows())
            return;
        string dir = Path.Combine(Path.GetTempPath(), "mortar-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "state");
        File.WriteAllText(path, "x");
        File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.False(string.IsNullOrEmpty(Files.TryDelete(path)));
        }
        finally
        {
            File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(dir, recursive: true);
        }
    }
}
