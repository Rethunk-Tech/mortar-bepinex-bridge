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
        string dir = Path.Combine(Path.GetTempPath(), "mortar-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(Files.ReadText(dir, out string? error));
            Assert.False(string.IsNullOrEmpty(error));
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
