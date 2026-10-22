using System.Collections.Generic;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Checks on the settings as returned by MQTTSettings.LoadSettings().
    /// </summary>
    internal static class SettingsRules
    {
        /// <summary>
        /// Whether the user has provided MQTT settings: a broker is the one setting that has
        /// no usable default.
        /// </summary>
        internal static bool HasBrokerConfigured(IDictionary<string, string> settings)
        {
            return settings.TryGetValue("mqttserver", out string server) && !string.IsNullOrEmpty(server);
        }

        internal static bool IsTlsEnabled(IDictionary<string, string> settings)
        {
            return settings.TryGetValue("mqtttls", out string tlsValue) && tlsValue == "True";
        }

        internal static bool TryParsePort(string text, out int port)
        {
            return int.TryParse(text, out port);
        }
    }
}
