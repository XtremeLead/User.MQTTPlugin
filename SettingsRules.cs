using System.Collections.Generic;
using System.Linq;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Checks on the settings as returned by MQTTSettings.LoadSettings().
    /// </summary>
    internal static class SettingsRules
    {
        /// <summary>
        /// Counts the settings whose name contains "mqtt" and whose value isn't empty.
        /// </summary>
        internal static int CountConfiguredMqttSettings(IDictionary<string, string> settings)
        {
            return settings.Count(kvp => kvp.Key.IndexOf("mqtt") > -1 && kvp.Value != "");
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
