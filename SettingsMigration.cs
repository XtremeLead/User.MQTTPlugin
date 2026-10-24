using System;
using System.Collections.Generic;
using System.Linq;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Turns the settings from before F4 (text values in user.config) into the typed model (D1).
    /// </summary>
    internal static class SettingsMigration
    {
        /// <summary>
        /// Builds the settings from <paramref name="legacy"/> (LegacySettings.Load() output). The
        /// password stays encrypted as it was. Without previous settings this gives the defaults.
        /// </summary>
        internal static MQTTPluginSettings FromLegacy(IDictionary<string, string> legacy, IPluginLog log)
        {
            var settings = new MQTTPluginSettings
            {
                SettingsVersion = MQTTPluginSettings.CurrentVersion,
                Server = Get(legacy, "mqttserver"),
                Port = MigratePort(Get(legacy, "mqttport")),
                Username = Get(legacy, "mqttuser"),
                EncryptedPassword = Get(legacy, "mqttpass"),
                Topic = Get(legacy, "mqtttopic"),
                UseTls = SettingsRules.IsTlsEnabled(legacy),
                Actions = MigrateActions(Get(legacy, "mqttactions"), log),
            };

            if (settings.Server == "" && settings.Actions.Count == 0)
            {
                log.Info("No previous MQTT settings found; starting with defaults.");
            }
            else
            {
                log.Info($"Migrated MQTT settings from user.config: broker '{settings.Server}', {settings.Actions.Count} action(s).");
            }
            return settings;
        }

        /// <summary>An empty port becomes the default; text that isn't a valid port becomes 0.</summary>
        private static int MigratePort(string text)
        {
            if (text == "") return MQTTPluginSettings.DefaultPort;
            return SettingsRules.TryParsePort(text, out int port) ? port : 0;
        }

        private static List<MqttActionSetting> MigrateActions(string xml, IPluginLog log)
        {
            if (xml == "") return new List<MqttActionSetting>();
            try
            {
                return ActionsSerializer.Deserialize(xml)
                    .Select(a => new MqttActionSetting(a.Key, a.Value.Item1, a.Value.Item2))
                    .ToList();
            }
            catch (Exception e)
            {
                log.Error($"Could not migrate the saved MQTT actions: {e.Message}");
                return new List<MqttActionSetting>();
            }
        }

        private static string Get(IDictionary<string, string> legacy, string name)
        {
            return legacy.TryGetValue(name, out string value) && value != null ? value : "";
        }
    }
}
