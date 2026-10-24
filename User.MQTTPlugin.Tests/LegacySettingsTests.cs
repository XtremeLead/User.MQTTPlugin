using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Reads the real Properties.Settings of the test host, which has no user.config: the same
    /// situation as a new user. LegacySettings only reads, so SimHub's user.config is never touched.
    /// </summary>
    [TestClass]
    public class LegacySettingsTests
    {
        [TestMethod]
        public void Load_NoUserConfig_ReturnsDefaultsAsText()
        {
            IDictionary<string, string> legacy = LegacySettings.Load();

            var expected = new Dictionary<string, string>
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
            Assert.AreEqual(expected.Count, legacy.Count);
            foreach (var setting in expected)
            {
                Assert.AreEqual(setting.Value, legacy[setting.Key], setting.Key);
            }
        }

        // End to end for a new user: the migration takes over nothing.
        [TestMethod]
        public void Load_NoUserConfig_MigratesToDefaults()
        {
            var log = new FakePluginLog();

            MQTTPluginSettings settings = SettingsMigration.FromLegacy(LegacySettings.Load(), log);

            Assert.AreEqual("", settings.Server);
            Assert.AreEqual(1883, settings.Port);
            Assert.AreEqual(0, settings.Actions.Count);
            Assert.IsTrue(log.Contains("Info", "No previous MQTT settings found"));
        }
    }
}
