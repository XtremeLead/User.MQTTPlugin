using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class SettingsMigrationTests
    {
        private readonly FakePluginLog log = new FakePluginLog();

        /// <summary>user.config of a new user: .NET returns the defaults from Settings.settings.</summary>
        private static Dictionary<string, string> Defaults()
        {
            return new Dictionary<string, string>
            {
                { "mqttserver", "" },
                { "mqttport", "" },
                { "mqttuser", "" },
                { "mqttpass", "" },
                { "mqtttopic", "" },
                { "mqtttls", "False" },
                { "mqttactions", "" },
                { "lastConnectionSuccessful", "False" },
            };
        }

        private static Dictionary<string, string> With(string name, string value)
        {
            Dictionary<string, string> legacy = Defaults();
            legacy[name] = value;
            return legacy;
        }

        [TestMethod]
        public void FromLegacy_ConfiguredUser_TakesOverEveryValue()
        {
            string encrypted = Crypt.ProtectString("secret");
            var actions = new Dictionary<string, ValueTuple<string, string>>
            {
                { "Light", ("home/light", "on") },
                { "Climate", ("home/climate", "toggle") },
            };
            var legacy = new Dictionary<string, string>
            {
                { "mqttserver", "172.16.4.10" },
                { "mqttport", "8883" },
                { "mqttuser", "user" },
                { "mqttpass", encrypted },
                { "mqtttopic", "simhub/status/#" },
                { "mqtttls", "True" },
                { "mqttactions", ActionsSerializationTests.LegacyXml(actions) },
                { "lastConnectionSuccessful", "True" },
            };

            MQTTPluginSettings settings = SettingsMigration.FromLegacy(legacy, log);

            Assert.AreEqual(MQTTPluginSettings.CurrentVersion, settings.SettingsVersion);
            Assert.AreEqual("172.16.4.10", settings.Server);
            Assert.AreEqual(8883, settings.Port);
            Assert.AreEqual("user", settings.Username);
            Assert.AreEqual(encrypted, settings.EncryptedPassword);
            Assert.AreEqual("simhub/status/#", settings.Topic);
            Assert.IsTrue(settings.UseTls);
            CollectionAssert.AreEqual(new[] { "Light", "Climate" }, settings.Actions.Select(a => a.Name).ToArray());
            Assert.AreEqual("home/climate", settings.Actions[1].Topic);
            Assert.AreEqual("toggle", settings.Actions[1].Message);
            Assert.IsTrue(log.Contains("Info", "Migrated MQTT settings from user.config: broker '172.16.4.10', 2 action(s)."));
        }

        // The password is encrypted the same way before and after F4, so it keeps working.
        [TestMethod]
        public void FromLegacy_Password_StaysEncryptedAndStillDecrypts()
        {
            MQTTPluginSettings settings = SettingsMigration.FromLegacy(With("mqttpass", Crypt.ProtectString("secret")), log);

            Assert.AreEqual("secret", Crypt.UnprotectString(settings.EncryptedPassword));
        }

        [TestMethod]
        public void FromLegacy_NewUser_GivesDefaults()
        {
            MQTTPluginSettings settings = SettingsMigration.FromLegacy(Defaults(), log);

            Assert.AreEqual(MQTTPluginSettings.CurrentVersion, settings.SettingsVersion);
            Assert.AreEqual("", settings.Server);
            Assert.AreEqual(1883, settings.Port);
            Assert.AreEqual("", settings.EncryptedPassword);
            Assert.IsFalse(settings.UseTls);
            Assert.AreEqual(0, settings.Actions.Count);
            Assert.IsTrue(log.Contains("Info", "No previous MQTT settings found; starting with defaults."));
            Assert.AreEqual(0, log.At("Error").Count);
        }

        [TestMethod]
        public void FromLegacy_NoValuesAtAll_GivesDefaults()
        {
            MQTTPluginSettings settings = SettingsMigration.FromLegacy(new Dictionary<string, string>(), log);

            Assert.AreEqual("", settings.Server);
            Assert.AreEqual(1883, settings.Port);
            Assert.AreEqual(0, settings.Actions.Count);
        }

        [TestMethod]
        [DataRow("", 1883, DisplayName = "empty becomes the default")]
        [DataRow("1884", 1884, DisplayName = "valid port")]
        [DataRow("abc", 0, DisplayName = "text becomes invalid (0)")]
        [DataRow("-1", 0, DisplayName = "negative becomes invalid (0)")]
        [DataRow("70000", 0, DisplayName = "above 65535 becomes invalid (0)")]
        public void FromLegacy_Port(string port, int expected)
        {
            Assert.AreEqual(expected, SettingsMigration.FromLegacy(With("mqttport", port), log).Port);
        }

        [TestMethod]
        [DataRow("True", true, DisplayName = "True")]
        [DataRow("False", false, DisplayName = "False")]
        public void FromLegacy_Tls(string tls, bool expected)
        {
            Assert.AreEqual(expected, SettingsMigration.FromLegacy(With("mqtttls", tls), log).UseTls);
        }

        [TestMethod]
        public void FromLegacy_UnreadableActions_LogsErrorAndKeepsTheRest()
        {
            Dictionary<string, string> legacy = With("mqttactions", "<not-closed>");
            legacy["mqttserver"] = "172.16.4.10";

            MQTTPluginSettings settings = SettingsMigration.FromLegacy(legacy, log);

            Assert.AreEqual(0, settings.Actions.Count);
            Assert.AreEqual("172.16.4.10", settings.Server);
            Assert.IsTrue(log.Contains("Error", "Could not migrate the saved MQTT actions"));
        }

        [TestMethod]
        public void FromLegacy_OnlyActions_CountsAsMigrated()
        {
            MQTTPluginSettings settings = SettingsMigration.FromLegacy(With("mqttactions", ActionsSerializationTests.SingleActionXml), log);

            Assert.AreEqual("ToggleClimate", settings.Actions.Single().Name);
            Assert.IsTrue(log.Contains("Info", "Migrated MQTT settings from user.config: broker '', 1 action(s)."));
        }
    }
}
