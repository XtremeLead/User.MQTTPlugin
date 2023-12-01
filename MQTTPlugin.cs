using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Windows.Media;
using System.IO;
using System.Globalization;

namespace User.MQTTPlugin
{
    [PluginDescription("Plugin to subscribe to MQTT topics")]
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
        private double currentSpeed = 0;
        private string mqttMessage = "";

        public double CurrentSpeed
        {
            get { return currentSpeed; }
            set { currentSpeed = value; }
        }
        public string MqttMessage
        {
            get
            {
                return mqttMessage;
            }
            set
            {
                DateTime localDate = DateTime.Now;
                string timestamp = localDate.ToString(new CultureInfo("nl-NL"));
                mqttMessage = timestamp + ": " + value;
                this.AttachDelegate("MQTTMessage", () => this.mqttMessage);
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
                    //this.WriteLog(data.OldData.SpeedKmh);
                    //data.OldData.SpeedKmh < Settings.SpeedWarningLevel && 
                    if (data.OldData.SpeedKmh < Settings.SpeedWarningLevel && data.OldData.SpeedKmh >= Settings.SpeedWarningLevel)
                    {
                        this.WriteLog(data.NewData.SpeedKmh);
                        // Trigger an event
                        this.TriggerEvent("SpeedWarning");
                    }
                    this.CurrentSpeed = data.NewData.SpeedKmh;
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
            this.AttachDelegate("CurrentSpeedWarningLevel", () => Settings.SpeedWarningLevel);
            this.AttachDelegate("CurrentSpeed", () => this.CurrentSpeed);
            this.AttachDelegate("MQTTMessage", () => this.mqttMessage);

            // Declare an event
            this.AddEvent("SpeedWarning");

            // Declare an action which can be called
            this.AddAction("IncrementSpeedWarning", (a, b) =>
            {
                Settings.SpeedWarningLevel++;
                SimHub.Logging.Current.Info("Speed warning changed");
            });

            // Declare an action which can be called
            this.AddAction("DecrementSpeedWarning", (a, b) =>
            {
                Settings.SpeedWarningLevel--;
            });
        }

        private void WriteLog(double value)
        {
            // Set a variable to the Documents path.
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Append text to an existing file named "WriteLines.txt".
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "WriteLines.txt"), true))
            {
                outputFile.WriteLine(value.ToString());
            }
        }
    }
}
