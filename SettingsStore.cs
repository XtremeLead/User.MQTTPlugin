using System.Collections.Generic;
using System.Configuration;
using SimHub.Plugins;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Where MQTTSettings reads and writes the settings (T3). Tests replace it with a fake.
    /// </summary>
    internal interface IPluginSettingsStore
    {
        MQTTPluginSettings Load();
        void Save(MQTTPluginSettings settings);
    }

    /// <summary>
    /// SimHub's own plugin storage (D1). SimHub only rewrites the file when its content changes,
    /// and keeps earlier versions in PluginsData\Common\_Backups.
    /// </summary>
    internal sealed class SimHubSettingsStore : IPluginSettingsStore
    {
        private const string SettingsName = "GeneralSettings";

        private readonly IPlugin plugin;

        public SimHubSettingsStore(IPlugin plugin)
        {
            this.plugin = plugin;
        }

        public MQTTPluginSettings Load() => plugin.ReadCommonSettings(SettingsName, () => new MQTTPluginSettings());

        public void Save(MQTTPluginSettings settings) => plugin.SaveCommonSettings(SettingsName, settings);
    }

    /// <summary>
    /// Reads the settings the plugin stored before F4, in Properties.Settings (the user.config of
    /// the current SimHub version). Only used for the one-time migration; never written.
    /// </summary>
    internal static class LegacySettings
    {
        internal static IDictionary<string, string> Load()
        {
            var settings = new Dictionary<string, string>();
            foreach (SettingsProperty property in Properties.Settings.Default.Properties)
            {
                settings[property.Name] = Properties.Settings.Default[property.Name]?.ToString() ?? "";
            }
            return settings;
        }
    }
}
