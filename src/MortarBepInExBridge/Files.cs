using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MortarBepInExBridge;

internal static class Files
{
    internal const int MaxTextBytes = 65536;

    /// <summary>The file's text, or null when it is missing. <paramref name="error"/> is set only when the file
    /// exists and cannot be read.</summary>
    internal static string? ReadText(string path, out string? error)
    {
        error = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxTextBytes)
            {
                error = $"file exceeds {MaxTextBytes} bytes";
                return null;
            }
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }

    // The temp file is restricted while still empty, so the token is never readable by others.
    internal static void AtomicWrite(string path, string contents)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, "");
        try
        {
            Restrict(temp);
            File.WriteAllText(temp, contents);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            throw;
        }
    }

    /// <summary>Null when the path is gone or was removed; otherwise the failure.</summary>
    internal static string? TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return null;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
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
