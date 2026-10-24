using System.Collections.Generic;
using System.Linq;

namespace User.MQTTPlugin
{
    /// <summary>
    /// All plugin settings (A2). SimHub stores this as JSON through ReadCommonSettings and
    /// SaveCommonSettings, in PluginsData\Common\MQTTPlugin.GeneralSettings.json in the SimHub
    /// folder, which survives SimHub updates (B5). Treat an instance as read-only once it is
    /// MQTTSettings.Current; MQTTSettings saves changes on a copy.
    /// </summary>
    public class MQTTPluginSettings
    {
        /// <summary>The version that has the settings migrated from user.config (D1).</summary>
        internal const int CurrentVersion = 1;

        internal const int DefaultPort = 1883;

        /// <summary>0 for a file that predates F4, so the user.config settings still need migrating.</summary>
        public int SettingsVersion { get; set; }

        public string Server { get; set; } = "";

        /// <summary>The broker port; 0 when the migrated value wasn't a valid port.</summary>
        public int Port { get; set; } = DefaultPort;

        public string Username { get; set; } = "";

        /// <summary>The password, encrypted with Crypt.ProtectString.</summary>
        public string EncryptedPassword { get; set; } = "";

        public string Topic { get; set; } = "";

        public bool UseTls { get; set; }

        public List<MqttActionSetting> Actions { get; set; } = new List<MqttActionSetting>();

        internal MQTTPluginSettings Clone()
        {
            return new MQTTPluginSettings
            {
                SettingsVersion = SettingsVersion,
                Server = Server,
                Port = Port,
                Username = Username,
                EncryptedPassword = EncryptedPassword,
                Topic = Topic,
                UseTls = UseTls,
                Actions = (Actions ?? new List<MqttActionSetting>()).Select(a => a.Clone()).ToList(),
            };
        }
    }

    /// <summary>
    /// A custom action: SimHub shows it as "MQTTPlugin.&lt;Name&gt;", and it publishes Message to Topic.
    /// </summary>
    public class MqttActionSetting
    {
        public MqttActionSetting()
        {
        }

        public MqttActionSetting(string name, string topic, string message)
        {
            Name = name;
            Topic = topic;
            Message = message;
        }

        public string Name { get; set; } = "";
        public string Topic { get; set; } = "";
        public string Message { get; set; } = "";

        internal MqttActionSetting Clone() => new MqttActionSetting(Name, Topic, Message);
    }
}
