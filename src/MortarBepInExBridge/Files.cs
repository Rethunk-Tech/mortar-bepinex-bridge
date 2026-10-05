using System.IO;
using System.Runtime.InteropServices;

namespace MortarBepInExBridge;

internal static class Files
{
    // The temp file is restricted while still empty, so the token is never readable by others.
    internal static void AtomicWrite(string path, string contents)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, "");
        Restrict(temp);
        File.WriteAllText(temp, contents);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }

    // A Windows build has no mode bits to set, which includes the same build running under Proton.
    private static void Restrict(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && chmod(path, 0x180) != 0)
            throw new IOException($"Could not restrict permissions on {path}.");
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, uint mode);
}
