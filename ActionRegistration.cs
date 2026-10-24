using System;
using System.Collections.Generic;
using SimHub.Plugins;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Where actions are registered. SimHubActionRegistry is the real one; tests use a fake.
    /// </summary>
    internal interface IActionRegistry
    {
        /// <summary>Removes every action this plugin registered.</summary>
        void Clear();

        /// <summary>Registers an action that SimHub shows as "MQTTPlugin.&lt;name&gt;".</summary>
        void Add(string name, Action run);
    }

    internal sealed class SimHubActionRegistry : IActionRegistry
    {
        private readonly MQTTPlugin plugin;
        private readonly PluginManager pluginManager;

        public SimHubActionRegistry(MQTTPlugin plugin, PluginManager pluginManager)
        {
            this.plugin = plugin;
            this.pluginManager = pluginManager;
        }

        public void Clear() => pluginManager.ClearActions(typeof(MQTTPlugin));

        public void Add(string name, Action run) => plugin.AddAction(name, (manager, input) => run());
    }

    /// <summary>
    /// Registers the saved MQTT actions with SimHub (A3). The whole set is replaced each time, so
    /// renamed and removed actions disappear (B4).
    /// </summary>
    internal static class ActionRegistration
    {
        /// <summary>
        /// Clears the registered actions and registers <paramref name="actions"/>; running one
        /// publishes its message to its topic.
        /// </summary>
        internal static void Register(IActionRegistry registry, IDictionary<string, ValueTuple<string, string>> actions, Action<string, string> publish, IPluginLog log)
        {
            registry.Clear();
            foreach (KeyValuePair<string, ValueTuple<string, string>> action in actions)
            {
                string topic = action.Value.Item1;
                string message = action.Value.Item2;
                registry.Add(action.Key, () => publish(topic, message));
            }
            log.Info($"Registered {actions.Count} MQTT action(s).");
        }

        /// <summary>
        /// Reads the saved actions from MQTTSettings.LoadSettings() output. Returns none when
        /// there are none or they can't be read; the latter is logged as an error.
        /// </summary>
        internal static Dictionary<string, ValueTuple<string, string>> LoadSaved(IDictionary<string, string> settings, IPluginLog log)
        {
            if (!settings.TryGetValue("mqttactions", out string xml) || string.IsNullOrEmpty(xml))
            {
                return new Dictionary<string, ValueTuple<string, string>>();
            }
            try
            {
                return ActionsSerializer.Deserialize(xml);
            }
            catch (Exception e)
            {
                log.Error($"Could not load saved MQTT actions: {e.Message}");
                return new Dictionary<string, ValueTuple<string, string>>();
            }
        }
    }
}
