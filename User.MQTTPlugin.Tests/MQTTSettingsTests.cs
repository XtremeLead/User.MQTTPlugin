using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class MQTTSettingsTests
    {
        private int legacyReads;

        [TestInitialize]
        public void Initialize() => TestState.Reset();

        [TestCleanup]
        public void Cleanup() => TestState.Reset();

        private Func<IDictionary<string, string>> Legacy(params string[] keyValues)
        {
            return () =>
            {
                legacyReads++;
                var settings = new Dictionary<string, string>();
                for (int i = 0; i < keyValues.Length; i += 2) settings[keyValues[i]] = keyValues[i + 1];
                return settings;
            };
        }

        private static ConnectionSettings Connection(string broker = "broker.test", string password = "secret")
        {
            return new ConnectionSettings(broker, 8883, "user", password, "home/#", true);
        }

        [TestMethod]
        public void Reconnect_ByDefault_ReconnectsTheSharedManager()
        {
            Assert.AreEqual(
                typeof(MqttConnectionManager).GetMethod("ReconnectSharedAsync", BindingFlags.NonPublic | BindingFlags.Static),
                TestState.OriginalReconnect.Method);
        }

        [TestMethod]
        public void Log_ByDefault_IsSimHubLog()
        {
            Assert.IsInstanceOfType(TestState.OriginalLog, typeof(SimHubLog));
        }

        // --- Initialize and the one-time migration (D1) ---

        [TestMethod]
        public void Initialize_AlreadyMigrated_UsesStoredSettingsWithoutReadingUserConfig()
        {
            var store = new FakePluginSettingsStore(new MQTTPluginSettings { SettingsVersion = 1, Server = "stored.test" });

            MQTTSettings.Initialize(store, Legacy("mqttserver", "legacy.test"));

            Assert.AreEqual("stored.test", MQTTSettings.Current.Server);
            Assert.AreEqual(0, legacyReads);
            Assert.AreEqual(0, store.SaveCount);
        }

        // B5: the settings from user.config move to SimHub's storage once.
        [TestMethod]
        public void Initialize_NotMigratedYet_MigratesSavesAndUsesTheResult()
        {
            var store = new FakePluginSettingsStore(new MQTTPluginSettings { SpeedWarningLevel = 80 });

            MQTTSettings.Initialize(store, Legacy("mqttserver", "legacy.test", "mqttport", "1884", "mqtttopic", "home/#"));

            Assert.AreEqual("legacy.test", MQTTSettings.Current.Server);
            Assert.AreEqual(1884, MQTTSettings.Current.Port);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(MQTTPluginSettings.CurrentVersion, store.Stored.SettingsVersion);
            Assert.AreEqual("legacy.test", store.Stored.Server);
            Assert.AreEqual(80, store.Stored.SpeedWarningLevel, "values from the existing file are kept");
        }

        [TestMethod]
        public void Initialize_AfterMigration_DoesNotReadUserConfigAgain()
        {
            var store = new FakePluginSettingsStore(new MQTTPluginSettings());
            MQTTSettings.Initialize(store, Legacy("mqttserver", "legacy.test"));

            MQTTSettings.Initialize(store, Legacy("mqttserver", "changed.test"));

            Assert.AreEqual(1, legacyReads);
            Assert.AreEqual("legacy.test", MQTTSettings.Current.Server);
            Assert.AreEqual(1, store.SaveCount);
        }

        // A new user has no user.config: .NET then returns the defaults, so nothing is taken over.
        [TestMethod]
        public void Initialize_NewUserWithoutUserConfig_SavesDefaultsAsMigrated()
        {
            var store = new FakePluginSettingsStore(new MQTTPluginSettings());

            MQTTSettings.Initialize(store, Legacy(
                "mqttserver", "", "mqttport", "", "mqttuser", "", "mqttpass", "",
                "mqtttopic", "", "mqtttls", "False", "mqttactions", "", "lastConnectionSuccessful", "False"));

            Assert.AreEqual("", MQTTSettings.Current.Server);
            Assert.AreEqual(1883, MQTTSettings.Current.Port);
            Assert.AreEqual(0, MQTTSettings.Current.Actions.Count);
            Assert.AreEqual(MQTTPluginSettings.CurrentVersion, store.Stored.SettingsVersion);
            Assert.IsTrue(TestState.Log.Contains("Info", "No previous MQTT settings found; starting with defaults."));
            Assert.AreEqual(0, TestState.Log.At("Error").Count);
        }

        [TestMethod]
        public void Initialize_StoreReturnsNothing_MigratesLikeANewFile()
        {
            var store = new FakePluginSettingsStore(null);

            MQTTSettings.Initialize(store, Legacy("mqttserver", "legacy.test"));

            Assert.AreEqual("legacy.test", MQTTSettings.Current.Server);
            Assert.AreEqual(1, store.SaveCount);
        }

        [TestMethod]
        public void Initialize_StoredActionsNull_BecomeEmpty()
        {
            var store = new FakePluginSettingsStore(new MQTTPluginSettings { SettingsVersion = 1, Actions = null });

            MQTTSettings.Initialize(store, TestState.NoLegacySettings);

            Assert.IsNotNull(MQTTSettings.Current.Actions);
            Assert.AreEqual(0, MQTTSettings.Current.Actions.Count);
        }

        [TestMethod]
        public void Initialize_SavingMigrationFails_LogsErrorAndStillUsesMigratedSettings()
        {
            var store = new FakePluginSettingsStore(new MQTTPluginSettings()) { SaveException = new UnauthorizedAccessException("read-only") };

            MQTTSettings.Initialize(store, Legacy("mqttserver", "legacy.test"));

            Assert.AreEqual("legacy.test", MQTTSettings.Current.Server);
            Assert.IsTrue(TestState.Log.Contains("Error", "Could not save the migrated MQTT settings: read-only"));
        }

        // --- SaveConnectionAsync ---

        [TestMethod]
        public void SaveConnectionAsync_StoresFieldsWithEncryptedPasswordThenReconnects()
        {
            var steps = new List<string>();
            var reconnect = new TaskCompletionSource<bool>().Task;
            MQTTSettings.Reconnect = () =>
            {
                steps.Add("reconnect after " + TestState.Store.SaveCount + " save(s)");
                return reconnect;
            };

            Task result = MQTTSettings.SaveConnectionAsync(Connection());

            MQTTPluginSettings stored = TestState.Store.Stored;
            Assert.AreEqual("broker.test", stored.Server);
            Assert.AreEqual(8883, stored.Port);
            Assert.AreEqual("user", stored.Username);
            Assert.AreEqual("home/#", stored.Topic);
            Assert.IsTrue(stored.UseTls);
            Assert.AreNotEqual("secret", stored.EncryptedPassword);
            Assert.AreEqual("secret", Crypt.UnprotectString(stored.EncryptedPassword));
            Assert.AreEqual("broker.test", MQTTSettings.Current.Server);
            CollectionAssert.AreEqual(new[] { "reconnect after 1 save(s)" }, steps);
            Assert.AreSame(reconnect, result);
        }

        [TestMethod]
        public void SaveConnectionAsync_KeepsTheActions()
        {
            MQTTSettings.SaveActions(new[] { new MqttActionSetting("Light", "home/light", "on") });

            MQTTSettings.SaveConnectionAsync(Connection());

            Assert.AreEqual("Light", TestState.Store.Stored.Actions.Single().Name);
        }

        [TestMethod]
        public void SaveConnectionAsync_StoreFails_ThrowsLogsAndKeepsSettingsWithoutReconnecting()
        {
            int reconnects = 0;
            MQTTSettings.Reconnect = () => { reconnects++; return Task.CompletedTask; };
            TestState.Store.SaveException = new UnauthorizedAccessException("read-only");

            Assert.ThrowsException<UnauthorizedAccessException>(() => MQTTSettings.SaveConnectionAsync(Connection()));

            Assert.AreEqual("", MQTTSettings.Current.Server);
            Assert.AreEqual(0, reconnects);
            Assert.IsTrue(TestState.Log.Contains("Error", "Could not save MQTT settings: read-only"));
        }

        // --- SaveActions ---

        [TestMethod]
        public void SaveActions_StoresActionsInOrderAndKeepsConnection()
        {
            MQTTSettings.SaveConnectionAsync(Connection());

            MQTTSettings.SaveActions(new[]
            {
                new MqttActionSetting("Light", "home/light", "on"),
                new MqttActionSetting("Climate", "home/climate", "toggle"),
            });

            CollectionAssert.AreEqual(new[] { "Light", "Climate" }, TestState.Store.Stored.Actions.Select(a => a.Name).ToArray());
            Assert.AreEqual("home/climate", MQTTSettings.Current.Actions[1].Topic);
            Assert.AreEqual("broker.test", TestState.Store.Stored.Server);
        }

        [TestMethod]
        public void SaveActions_LaterChangeToTheGivenList_DoesNotAffectSettings()
        {
            var actions = new List<MqttActionSetting> { new MqttActionSetting("Light", "home/light", "on") };
            MQTTSettings.SaveActions(actions);

            actions[0].Name = "Changed";
            actions.Add(new MqttActionSetting("Extra", "t", "m"));

            Assert.AreEqual("Light", MQTTSettings.Current.Actions.Single().Name);
        }

        [TestMethod]
        public void SaveActions_StoreFails_ThrowsAndKeepsActions()
        {
            MQTTSettings.SaveActions(new[] { new MqttActionSetting("Light", "home/light", "on") });
            TestState.Store.SaveException = new UnauthorizedAccessException("read-only");

            Assert.ThrowsException<UnauthorizedAccessException>(() => MQTTSettings.SaveActions(new MqttActionSetting[0]));

            Assert.AreEqual(1, MQTTSettings.Current.Actions.Count);
        }

        // Readers on other threads keep a complete, unchanging set.
        [TestMethod]
        public void Save_ReplacesCurrentInsteadOfChangingIt()
        {
            MQTTPluginSettings before = MQTTSettings.Current;

            MQTTSettings.SaveConnectionAsync(Connection());

            Assert.AreNotSame(before, MQTTSettings.Current);
            Assert.AreEqual("", before.Server);
        }

        [TestMethod]
        public void Save_BeforeInitialize_Throws()
        {
            typeof(MQTTSettings).GetField("store", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);

            Assert.ThrowsException<InvalidOperationException>(() => MQTTSettings.SaveActions(new MqttActionSetting[0]));
        }
    }
}
