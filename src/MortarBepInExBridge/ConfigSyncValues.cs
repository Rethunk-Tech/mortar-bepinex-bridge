using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace MortarBepInExBridge;

/// <summary>
/// The values a host sent through Mortar-ConfigSync, read by reflection so the bridge has no dependency on it. Its
/// fields are internal, so a ConfigSync whose shape changed reads as no values rather than failing.
/// </summary>
internal static class ConfigSyncValues
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>A JSON array of {mod, section, key, value, inUse}, one per shared setting the host has sent a value for.</summary>
    public static string Read(Func<string, Type?> find)
    {
        var sb = new StringBuilder("[");
        if (find("ConfigSync.SyncedConfig")?.GetField("all", Fields)?.GetValue(null) is IDictionary configs)
        {
            foreach (object config in configs.Values)
            {
                Type type = config.GetType();
                if (type.GetField("Id", Fields)?.GetValue(config) is not string mod || type.GetField("Entries", Fields)?.GetValue(config) is not IDictionary entries)
                    continue;
                foreach (object entry in entries.Values)
                {
                    Type entryType = entry.GetType();
                    if (entryType.GetField("hostText", Fields)?.GetValue(entry) is not string host
                        || entryType.GetProperty("FromHost", Fields)?.GetValue(entry) is not bool inUse
                        || entryType.GetProperty("Local", Fields)?.GetValue(entry) is not { } local
                        || local.GetType().GetProperty("Definition")?.GetValue(local) is not { } definition
                        || definition.GetType().GetProperty("Section")?.GetValue(definition) is not string section
                        || definition.GetType().GetProperty("Key")?.GetValue(definition) is not string key)
                        continue;
                    if (sb.Length > 1)
                        sb.Append(',');
                    sb.Append("{\"mod\":").Append(Json.Quote(mod))
                        .Append(",\"section\":").Append(Json.Quote(section))
                        .Append(",\"key\":").Append(Json.Quote(key))
                        .Append(",\"value\":").Append(Json.Quote(host))
                        .Append(",\"inUse\":").Append(inUse ? "true" : "false").Append('}');
                }
            }
        }
        return sb.Append(']').ToString();
    }
}
