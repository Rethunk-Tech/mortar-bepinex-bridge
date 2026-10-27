using Xunit;

namespace ConfigSync
{
    // The members of Mortar-ConfigSync 0.1.0 that the bridge reads, with the same names and kinds.
    internal interface ISyncedEntry;

    internal sealed class SyncedConfig(string id)
    {
        internal static readonly Dictionary<string, SyncedConfig> all = [];
        internal readonly string Id = id;
        internal readonly Dictionary<string, ISyncedEntry> Entries = [];
    }

    internal sealed class SyncedEntry<T>(string section, string key, string? hostText, bool fromHost) : ISyncedEntry
    {
        private readonly string? hostText = hostText;
        public ConfigEntry<T> Local { get; } = new(new(section, key));
        public bool FromHost { get; private set; } = fromHost;
    }

    // BepInEx's ConfigEntry<T>, down to the definition the bridge reads.
    internal sealed class ConfigEntry<T>(ConfigDefinition definition)
    {
        public ConfigDefinition Definition { get; } = definition;
    }

    internal sealed class ConfigDefinition(string section, string key)
    {
        public string Section { get; } = section;
        public string Key { get; } = key;
    }
}

namespace MortarBepInExBridge.Tests
{
    public class ConfigSyncValuesTests
    {
        [Fact]
        public void ListsTheValuesTheHostSent()
        {
            var config = new ConfigSync.SyncedConfig("tech.rethunk.mortar.Dropship");
            config.Entries["General\nDistance"] = new ConfigSync.SyncedEntry<int>("General", "Distance", "60", true);
            config.Entries["General\nMarker"] = new ConfigSync.SyncedEntry<bool>("General", "Marker", "false", false);
            config.Entries["General\nColour"] = new ConfigSync.SyncedEntry<string>("General", "Colour", null, false);
            ConfigSync.SyncedConfig.all[config.Id] = config;
            try
            {
                Assert.Equal(
                    "[{\"mod\":\"tech.rethunk.mortar.Dropship\",\"section\":\"General\",\"key\":\"Distance\",\"value\":\"60\",\"inUse\":true},"
                    + "{\"mod\":\"tech.rethunk.mortar.Dropship\",\"section\":\"General\",\"key\":\"Marker\",\"value\":\"false\",\"inUse\":false}]",
                    ConfigSyncValues.Read(name => name == "ConfigSync.SyncedConfig" ? typeof(ConfigSync.SyncedConfig) : null));
            }
            finally
            {
                ConfigSync.SyncedConfig.all.Clear();
            }
        }

        [Fact]
        public void WithoutConfigSyncThereAreNone() => Assert.Equal("[]", ConfigSyncValues.Read(_ => null));
    }
}
