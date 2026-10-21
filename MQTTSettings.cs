using System;
using System.Collections.Generic;
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

        public static Task SaveSettingsAsync()
        {
            foreach (KeyValuePair<string, string> item in SETTINGS)
            {
                Store[item.Key] = item.Value;
            }
            Store.Save();

            // Settings have been tested, so connect
            return Reconnect();
        }
    }
}
