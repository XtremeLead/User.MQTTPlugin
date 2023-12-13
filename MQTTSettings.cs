using System.Collections.Generic;
using System.Configuration;

namespace User.MQTTPlugin
{
    class MQTTSettings
    {
        private static IDictionary<string, string> SETTINGS = new Dictionary<string, string>();
        public static IDictionary<string, string> LoadSettings()
        {
            IDictionary<string, string> settings = new Dictionary<string, string>();

            foreach (SettingsProperty currentProperty in Properties.Settings.Default.Properties)
            {
                string name = currentProperty.Name;
                settings.Add(name, Properties.Settings.Default[name].ToString());
            }
            return settings;
        }

        public static void RememberSetting(string key, string value)
        {
            SETTINGS[key] = value;
        }

        public string GetSetting(string key)
        {
            return Properties.Settings.Default[key].ToString();
        }

        public static void SaveSettings()
        {
            foreach (var item in SETTINGS)
            {
                Properties.Settings.Default[item.Key] = item.Value;
            }
            Properties.Settings.Default.Save();

            // Settings have been tested, so connect
            MQTTClient.ConnectAndSubscribe();
        }
    }
}
