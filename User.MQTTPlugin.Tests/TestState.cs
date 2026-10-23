using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Resets the static state of MQTTSettings and MQTTPlugin between tests and swaps in test
    /// doubles: a FakeSettingsStore and a reconnect that does nothing. The staged settings are a
    /// private static field and are cleared through reflection. Tests never start
    /// MqttConnectionManager.Shared; connection tests build their own manager (ManagerHarness).
    /// </summary>
    internal static class TestState
    {
        // The production wiring, captured before the first Reset() replaces it.
        public static readonly ISettingsStore OriginalStore = MQTTSettings.Store;
        public static readonly Func<Task> OriginalReconnect = MQTTSettings.Reconnect;

        // An explicit static constructor makes the fields above initialize before the first
        // Reset() runs, instead of lazily at their first use.
        static TestState()
        {
        }

        public static FakeSettingsStore Store => (FakeSettingsStore)MQTTSettings.Store;

        public static void Reset()
        {
            ((IDictionary<string, string>)typeof(MQTTSettings).GetField("SETTINGS", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();
            MQTTSettings.Store = new FakeSettingsStore();
            MQTTSettings.Reconnect = () => Task.CompletedTask;
            typeof(MQTTPlugin).GetProperty("Instance").SetValue(null, null);
        }
    }
}
