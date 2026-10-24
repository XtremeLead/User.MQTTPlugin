using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Resets the static state of MQTTSettings and MQTTPlugin between tests: MQTTSettings is
    /// loaded from a FakePluginSettingsStore with already-migrated defaults, logs to a
    /// FakePluginLog and reconnects to nothing. Tests never start MqttConnectionManager.Shared;
    /// connection tests build their own manager (ManagerHarness).
    /// </summary>
    internal static class TestState
    {
        // The production wiring, captured before the first Reset() replaces it.
        public static readonly Func<Task> OriginalReconnect = MQTTSettings.Reconnect;
        public static readonly IPluginLog OriginalLog = MQTTSettings.Log;

        // An explicit static constructor makes the fields above initialize before the first
        // Reset() runs, instead of lazily at their first use.
        static TestState()
        {
        }

        public static FakePluginSettingsStore Store { get; private set; }
        public static FakePluginLog Log { get; private set; }

        /// <summary>A legacy loader for tests that must not read user.config.</summary>
        public static IDictionary<string, string> NoLegacySettings()
        {
            throw new AssertFailedException("user.config must not be read here.");
        }

        public static void Reset()
        {
            Store = new FakePluginSettingsStore(new MQTTPluginSettings { SettingsVersion = MQTTPluginSettings.CurrentVersion });
            Log = new FakePluginLog();
            MQTTSettings.Log = Log;
            MQTTSettings.Reconnect = () => Task.CompletedTask;
            MQTTSettings.Initialize(Store, NoLegacySettings);
            typeof(MQTTPlugin).GetProperty("Instance").SetValue(null, null);
        }
    }
}
