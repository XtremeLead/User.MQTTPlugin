using System.Collections.Generic;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Checks on the settings.
    /// </summary>
    internal static class SettingsRules
    {
        /// <summary>
        /// Whether the user has provided MQTT settings: a broker is the one setting that has
        /// no usable default.
        /// </summary>
        internal static bool HasBrokerConfigured(MQTTPluginSettings settings)
        {
            return !string.IsNullOrEmpty(settings.Server);
        }

        /// <summary>The TLS flag as stored in user.config before F4 (the text "True").</summary>
        internal static bool IsTlsEnabled(IDictionary<string, string> legacySettings)
        {
            return legacySettings.TryGetValue("mqtttls", out string tlsValue) && tlsValue == "True";
        }

        internal static bool IsValidPort(int port)
        {
            return port >= 1 && port <= 65535;
        }

        /// <summary>Parses a port number, accepting only 1 to 65535.</summary>
        internal static bool TryParsePort(string text, out int port)
        {
            if (int.TryParse(text, out port) && IsValidPort(port))
            {
                return true;
            }
            port = 0;
            return false;
        }
    }
}
