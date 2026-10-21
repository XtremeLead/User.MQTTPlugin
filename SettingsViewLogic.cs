using System.Windows.Input;

namespace User.MQTTPlugin
{
    /// <summary>
    /// The decisions SettingsControlV2 makes, kept apart from the WPF controls so they can be tested.
    /// </summary>
    internal static class SettingsViewLogic
    {
        internal const string NoSettingsMessage = "Please provide MQTT settings.";
        internal const string ConnectionFailedMessage = "Connection failed. Please check your settings or broker availability.";
        internal const string ConnectionSuccessMessage = "Successfully connected. Happy simracing!";
        internal const string ConnectionTestSuccessMessage = "Successfully connected. Click Save to Save your settings and connect.";

        /// <summary>
        /// The status shown when the settings page opens, or null to keep the default text.
        /// </summary>
        internal static string InitialConnectionStatus(int configuredSettingsCount, bool clientExists, bool isConnected)
        {
            string status = null;
            if (configuredSettingsCount == 0)
            {
                status = NoSettingsMessage;
            }
            if (clientExists || configuredSettingsCount > 0)
            {
                status = isConnected ? ConnectionSuccessMessage : ConnectionFailedMessage;
            }
            return status;
        }

        /// <summary>
        /// Switches between the default ports 1883 and 8883 when TLS is turned on or off.
        /// </summary>
        internal static string PortAfterTlsChange(bool? useTls, string port)
        {
            if (useTls == true && port == "1883") return "8883";
            if (useTls == false && port == "8883") return "1883";
            return port;
        }

        internal static bool IsPortKey(Key key)
        {
            return (key >= Key.D0 && key <= Key.D9) || (key >= Key.NumPad0 && key <= Key.NumPad9);
        }

        /// <summary>
        /// The text of an action field: empty while it shows its placeholder, trimmed otherwise.
        /// </summary>
        internal static string RealText(string text, bool isPlaceholder)
        {
            return isPlaceholder ? "" : (text ?? "").Trim();
        }

        internal static bool IsCompleteAction(string name, string topic, string message)
        {
            return !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(topic) && !string.IsNullOrEmpty(message);
        }

        internal static string ActionsSavedMessage(int savedCount, int skippedCount)
        {
            return skippedCount == 0
                ? $"Saved {savedCount} action(s)."
                : $"Saved {savedCount} action(s); {skippedCount} incomplete row(s) skipped — fill in all three fields.";
        }
    }
}
