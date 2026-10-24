using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Holds the current settings and saves changes (F4). MQTTPlugin.Init loads them through
    /// Initialize; the settings page saves through SaveConnectionAsync and SaveActions.
    ///
    /// Current is replaced, never changed: every save works on a copy, writes it to the store and
    /// only then makes it Current. So readers on other threads (the connection watchdog) always
    /// see a complete set, and a failed save leaves Current as it is on disk.
    /// </summary>
    internal static class MQTTSettings
    {
        private static readonly object sync = new object();
        private static volatile MQTTPluginSettings current = new MQTTPluginSettings();
        private static IPluginSettingsStore store;

        // What happens after saving the connection, and where messages go. Tests replace both.
        internal static Func<Task> Reconnect = MqttConnectionManager.ReconnectSharedAsync;
        internal static IPluginLog Log = new SimHubLog();

        /// <summary>The current settings. Don't change them; use the save methods.</summary>
        public static MQTTPluginSettings Current => current;

        /// <summary>
        /// Loads the settings. The first time (SettingsVersion 0) it migrates the settings from
        /// <paramref name="loadLegacy"/> and saves them (D1); after that user.config is never read.
        /// </summary>
        internal static void Initialize(IPluginSettingsStore settingsStore, Func<IDictionary<string, string>> loadLegacy)
        {
            MQTTPluginSettings loaded = settingsStore.Load() ?? new MQTTPluginSettings();
            if (loaded.Actions == null) loaded.Actions = new List<MqttActionSetting>();

            if (loaded.SettingsVersion < MQTTPluginSettings.CurrentVersion)
            {
                MQTTPluginSettings migrated = SettingsMigration.FromLegacy(loadLegacy(), Log);
                try
                {
                    settingsStore.Save(migrated);
                }
                catch (Exception e)
                {
                    // Use them anyway; the migration runs again on the next start.
                    Log.Error($"Could not save the migrated MQTT settings: {e.Message}");
                }
                loaded = migrated;
            }

            lock (sync)
            {
                store = settingsStore;
                current = loaded;
            }
        }

        /// <summary>
        /// Saves the connection settings from the settings page, with the password encrypted,
        /// and reconnects with them.
        /// </summary>
        public static Task SaveConnectionAsync(ConnectionSettings connection)
        {
            Update(settings =>
            {
                settings.Server = connection.Broker;
                settings.Port = connection.Port;
                settings.Username = connection.Username;
                settings.EncryptedPassword = Crypt.ProtectString(connection.Password);
                settings.Topic = connection.Topic;
                settings.UseTls = connection.UseTls;
            });
            return Reconnect();
        }

        public static void SaveActions(IEnumerable<MqttActionSetting> actions)
        {
            List<MqttActionSetting> copy = actions.Select(a => a.Clone()).ToList();
            Update(settings => settings.Actions = copy);
        }

        private static void Update(Action<MQTTPluginSettings> change)
        {
            lock (sync)
            {
                if (store == null)
                {
                    throw new InvalidOperationException("The MQTT settings haven't been loaded yet.");
                }
                MQTTPluginSettings next = current.Clone();
                change(next);
                try
                {
                    store.Save(next);
                }
                catch (Exception e)
                {
                    Log.Error($"Could not save MQTT settings: {e.Message}");
                    throw;
                }
                current = next;
            }
        }
    }
}
