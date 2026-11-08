using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MortarBepInExBridge;

/// <summary>One loaded config file and the call that makes BepInEx re-read it.</summary>
internal sealed class ConfigTarget(string path, Action reload)
{
    public string Path { get; } = path;
    public Action Reload { get; } = reload;
}

/// <summary>What a <c>config reload</c> did: files reloaded, and one message per file whose reload threw.</summary>
internal sealed class ConfigReloadResult(int reloaded, IReadOnlyList<string> failures)
{
    public int Reloaded { get; } = reloaded;
    public IReadOnlyList<string> Failures { get; } = failures;
}

internal static class ConfigReload
{
    /// <summary>Why a name is not a config file inside BepInEx's config folder, or null when it is one.</summary>
    public static string? Validate(string name)
    {
        if (name.Length == 0)
            return "config reload needs a config file name";
        if (name.StartsWith('/') || name.StartsWith('\\') || Path.IsPathRooted(name) || name.Length >= 2 && name[1] == ':')
            return "config reload needs a path below the config folder, not a rooted one";
        foreach (string part in name.Split('/', '\\'))
        {
            if (part == "..")
                return "config reload does not take .. segments";
        }
        if (!name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
            return "config reload only takes .cfg files";
        return null;
    }

    /// <summary>Whether a loaded ConfigFile's path is the named file below configDir; names are case-insensitive.</summary>
    public static bool Owns(string configDir, string name, string filePath) =>
        string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(Path.Join(configDir, name)), StringComparison.OrdinalIgnoreCase);

    /// <summary>Reloads every target that owns the file; a throwing reload is reported and the rest still run.</summary>
    public static ConfigReloadResult Run(string configDir, string name, IEnumerable<ConfigTarget> targets)
    {
        int reloaded = 0;
        var failures = new List<string>();
        foreach (ConfigTarget target in targets.Where(target => Owns(configDir, name, target.Path)))
        {
            try
            {
                target.Reload();
                reloaded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.Message.Replace('\n', ' ').Replace('\r', ' ')}");
            }
        }
        return new ConfigReloadResult(reloaded, failures);
    }

    public static string Reply(ConfigReloadResult result) =>
        result.Failures.Count == 0
            ? $"ok reloaded {result.Reloaded}"
            : $"ok reloaded {result.Reloaded}, failed {result.Failures.Count}: {string.Join("; ", result.Failures)}";
}
