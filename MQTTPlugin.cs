using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Windows.Media;

namespace User.MQTTPlugin
{
    [PluginDescription("Plugin to publish and subscribe to a MQTT topic. Returns a JSON object.")]
    [PluginAuthor("XtremeLead")]
    [PluginName("MQTT plugin")]
    public class MQTTPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public MQTTPluginSettings Settings;

        public static MQTTPlugin Instance { get; private set; }

        /// <summary>
        /// Instance of the current plugin manager
        /// </summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>
        /// Gets the left menu icon. Icon must be 24x24 and compatible with black and white display.
        /// </summary>
        public ImageSource PictureIcon => this.ToIcon(Properties.Resources.sdkmenuicon);

        /// <summary>
        /// Gets a short plugin title to show in left menu. Return null if you want to use the title as defined in PluginName attribute.
        /// </summary>
        public string LeftMenuTitle => "MQTT plugin";

        // Written by the M2Mqtt receive thread, read by SimHub through the delegate attached in Init.
        private volatile string mqttMessage = "";

        public string MqttMessage
        {
            get
            {
                return mqttMessage;
            }
            set
            {
                mqttMessage = value;
            }
        }
        /// <summary>
        /// Called one time per game data update, contains all normalized game data,
        /// raw data are intentionnally "hidden" under a generic object type (A plugin SHOULD NOT USE IT)
        ///
        /// This method is on the critical path, it must execute as fast as possible and avoid throwing any error
        ///
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <param name="data">Current game data, including current and previous data frame.</param>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Define the value of our property (declared in init)
            if (data.GameRunning)
            {
                if (data.OldData != null && data.NewData != null)
                {

                }
            }
        }

        /// <summary>
        /// Called at plugin manager stop, close/dispose anything needed here !
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            MqttConnectionManager.Shared.Stop(this);
            MqttConnectionManager.Shared.MessageReceived -= OnMqttMessage;

            // Save settings
            this.SaveCommonSettings("GeneralSettings", Settings);

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Returns the settings control, return null if no settings control is required
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            const bool useRedesignedSettingsUI = true;
            return useRedesignedSettingsUI
                ? (System.Windows.Controls.Control)new SettingsControlV2(this)
                : new SettingsControl(this);
        }

        /// <summary>
        /// Called once after plugins startup
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("Starting plugin");

            Instance = this;

            // Load settings
            Settings = this.ReadCommonSettings<MQTTPluginSettings>("GeneralSettings", () => new MQTTPluginSettings());

            // Declare a property available in the property list, this gets evaluated "on demand" (when shown or used in formulas)
            this.AttachDelegate("CurrentDateTime", () => DateTime.Now);

            this.AttachDelegate("MQTTMessage", () => mqttMessage);

            // Connects in the background, so a slow or unreachable broker doesn't hold up SimHub (D5).
            MqttConnectionManager.Shared.MessageReceived += OnMqttMessage;
            MqttConnectionManager.Shared.Start(this);
        }

        internal void OnMqttMessage(string message)
        {
            MqttMessage = message;
        }

        public void AddAction(string name, string topic, string message)
        {
            this.AddAction(name, (a, b) =>
            {
                MqttConnectionManager.Shared.Publish(topic, message);
            });
        }


    }
}
