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

        private string mqttMessage = "";

        public string MqttMessage
        {
            get
            {
                return mqttMessage;
            }
            set
            {

                //mqttMessage = timestamp + ": " + value;
                mqttMessage = value;
                this.AttachDelegate("MQTTMessage", () => mqttMessage);
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
            // Save settings
            this.SaveCommonSettings("GeneralSettings", Settings);
            if (MQTTClient.CLIENT != null)
            {
                MQTTClient.CLIENT.Disconnect();
            }
        }

        /// <summary>
        /// Returns the settings control, return null if no settings control is required
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(this);
        }

        /// <summary>
        /// Called once after plugins startup
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("Starting plugin");

            if ((bool)Properties.Settings.Default["lastConnectionSuccessful"])
            {
                MQTTClient.ConnectAndSubscribe();
            }

            // Load settings
            Settings = this.ReadCommonSettings<MQTTPluginSettings>("GeneralSettings", () => new MQTTPluginSettings());

            // Declare a property available in the property list, this gets evaluated "on demand" (when shown or used in formulas)
            this.AttachDelegate("CurrentDateTime", () => DateTime.Now);

            this.AttachDelegate("MQTTMessage", () => mqttMessage);

            // Declare an event


            // Declare an action which can be called
            this.AddAction("PublishTest", (a, b) =>
            {
                // a = pluginmanager
                // b = button pressed

                string message = "toggle";
                string topic = "simhub/commands/kantoor/climate";
                MQTTClient.Publish(topic, message);

            });

            // Declare an action which can be called
        }

        public void AddAction(string name, string topic, string message)
        {
            this.AddAction(name, (a, b) =>
            {
                MQTTClient.Publish(topic, message);
            });
        }


    }
}
