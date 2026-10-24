namespace User.MQTTPlugin
{
    internal enum ConnectionSettingsProblem
    {
        None,
        NoBroker,
        InvalidPort,
    }

    /// <summary>
    /// Everything needed to connect to a broker and subscribe, with the password decrypted.
    /// </summary>
    internal sealed class ConnectionSettings
    {
        public ConnectionSettings(string broker, int port, string username, string password, string topic, bool useTls)
        {
            Broker = broker;
            Port = port;
            Username = username;
            Password = password;
            Topic = topic;
            UseTls = useTls;
        }

        public string Broker { get; }
        public int Port { get; }
        public string Username { get; }
        public string Password { get; }
        public string Topic { get; }
        public bool UseTls { get; }

        /// <summary>
        /// Builds the connection settings from the saved settings, decrypting the password.
        /// Returns null and the reason when there is no broker or the port isn't valid.
        /// </summary>
        internal static ConnectionSettings FromSettings(MQTTPluginSettings settings, out ConnectionSettingsProblem problem)
        {
            if (!SettingsRules.HasBrokerConfigured(settings))
            {
                problem = ConnectionSettingsProblem.NoBroker;
                return null;
            }
            if (!SettingsRules.IsValidPort(settings.Port))
            {
                problem = ConnectionSettingsProblem.InvalidPort;
                return null;
            }
            problem = ConnectionSettingsProblem.None;
            return new ConnectionSettings(
                settings.Server,
                settings.Port,
                settings.Username ?? "",
                Crypt.UnprotectString(settings.EncryptedPassword),
                settings.Topic ?? "",
                settings.UseTls);
        }
    }
}
