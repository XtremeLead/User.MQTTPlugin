using System;
using System.Collections.Generic;
using System.Linq;
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
        internal static void Register(IActionRegistry registry, IEnumerable<MqttActionSetting> actions, Action<string, string> publish, IPluginLog log)
        {
            List<MqttActionSetting> toRegister = actions.ToList();
            registry.Clear();
            foreach (MqttActionSetting action in toRegister)
            {
                string topic = action.Topic;
                string message = action.Message;
                registry.Add(action.Name, () => publish(topic, message));
            }
            log.Info($"Registered {toRegister.Count} MQTT action(s).");
        }
    }
}
