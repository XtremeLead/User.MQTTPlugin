using System.Collections.Generic;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class MQTTSettingsTests
    {
        [TestInitialize]
        public void Initialize() => TestState.Reset();

        [TestCleanup]
        public void Cleanup() => TestState.Reset();

        [TestMethod]
        public void LoadSettings_Defaults_ReturnsEverySettingAsString()
        {
            IDictionary<string, string> settings = MQTTSettings.LoadSettings();

            var expected = new Dictionary<string, string>
            {
                { "mqttserver", "" },
                { "mqttport", "" },
                { "mqttuser", "" },
                { "mqttpass", "" },
                { "mqtttopic", "" },
                { "mqttactions", "" },
                { "mqtttls", "False" },
                { "lastConnectionSuccessful", "False" },
            };
            Assert.AreEqual(expected.Count, settings.Count);
            foreach (var setting in expected)
            {
                Assert.AreEqual(setting.Value, settings[setting.Key], setting.Key);
            }
        }

        [TestMethod]
        public void LoadSettings_BooleanSettingTrue_ReturnsTrueText()
        {
            Properties.Settings.Default["mqtttls"] = true;

            Assert.AreEqual("True", MQTTSettings.LoadSettings()["mqtttls"]);
        }

        [TestMethod]
        public void LoadSettings_StringSetting_ReturnsValue()
        {
            Properties.Settings.Default["mqttserver"] = "broker.example";

            Assert.AreEqual("broker.example", MQTTSettings.LoadSettings()["mqttserver"]);
        }

        [TestMethod]
        public void GetSetting_ReturnsValueAsString()
        {
            Properties.Settings.Default["mqttport"] = "1883";

            Assert.AreEqual("1883", new MQTTSettings().GetSetting("mqttport"));
            Assert.AreEqual("False", new MQTTSettings().GetSetting("mqtttls"));
        }

        [TestMethod]
        public void RememberSetting_DoesNotChangeSettingsBeforeSave()
        {
            MQTTSettings.RememberSetting("mqttserver", "broker.example");

            Assert.AreEqual("", MQTTSettings.LoadSettings()["mqttserver"]);
        }

        // Current behaviour (B2): the UI stages mqtttls as text, but the setting is a bool.
        // Saving throws before Save() and the staged value stays, so every later save fails too.
        [TestMethod]
        public void SaveSettingsAsync_TlsStagedAsText_ThrowsAndKeepsFailingOnRetry()
        {
            MQTTSettings.RememberSetting("mqtttls", "True");

            Assert.ThrowsException<SettingsPropertyWrongTypeException>(() => MQTTSettings.SaveSettingsAsync());
            Assert.AreEqual(false, Properties.Settings.Default["mqtttls"]);
            Assert.ThrowsException<SettingsPropertyWrongTypeException>(() => MQTTSettings.SaveSettingsAsync());
        }
    }
}
