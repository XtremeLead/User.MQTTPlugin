using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace User.MQTTPlugin
{
    class MQTTSettings
    {
        private static IDictionary<string, string> SETTINGS = new Dictionary<string, string>();

        // Where the settings live, and what happens after saving. Tests replace both.
        internal static ISettingsStore Store = new PropertiesSettingsStore();
        internal static Func<Task> Reconnect = MQTTClient.ConnectAndSubscribeAsync;

        public static IDictionary<string, string> LoadSettings()
        {
            IDictionary<string, string> settings = new Dictionary<string, string>();

            foreach (string name in Store.Names)
            {
                settings.Add(name, Store[name].ToString());
            }
            return settings;
        }

        public static void RememberSetting(string key, string value)
        {
            SETTINGS[key] = value;
        }

        public string GetSetting(string key)
        {
            return Store[key].ToString();
        }

        /// <summary>
        /// Writes the staged settings, saves them and reconnects. Each staged text is converted
        /// to the type of its setting (for example "True" for the bool mqtttls) before anything
        /// is written. If one can't be converted, nothing is written, the staged values are
        /// discarded and the exception is rethrown.
        /// </summary>
        public static Task SaveSettingsAsync()
        {
            Dictionary<string, object> converted = new Dictionary<string, object>();
            try
            {
                foreach (KeyValuePair<string, string> item in SETTINGS)
                {
                    Type settingType = Store[item.Key].GetType();
                    converted[item.Key] = Convert.ChangeType(item.Value, settingType, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                SETTINGS.Clear();
                SimHub.Logging.Current.Error($"Could not save MQTT settings: {e.Message}");
                throw;
            }

            foreach (KeyValuePair<string, object> item in converted)
            {
                Store[item.Key] = item.Value;
            }
            Store.Save();

            // Settings have been tested, so connect
            return Reconnect();
        }
    }
}
