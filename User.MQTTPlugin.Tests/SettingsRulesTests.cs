using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class SettingsRulesTests
    {
        private static Dictionary<string, string> Settings(params string[] keyValues)
        {
            var settings = new Dictionary<string, string>();
            for (int i = 0; i < keyValues.Length; i += 2)
            {
                settings[keyValues[i]] = keyValues[i + 1];
            }
            return settings;
        }

        // Current behaviour (B7): on a fresh install mqtttls is "False", which counts as a
        // configured setting, so the count is never 0.
        [TestMethod]
        public void CountConfiguredMqttSettings_FreshInstall_CountsTlsDefault()
        {
            var fresh = new FakeSettingsStore();
            var settings = new Dictionary<string, string>();
            foreach (string name in fresh.Names) settings[name] = fresh[name].ToString();

            Assert.AreEqual(1, SettingsRules.CountConfiguredMqttSettings(settings));
        }

        [TestMethod]
        public void CountConfiguredMqttSettings_CountsOnlyNonEmptyMqttKeys()
        {
            var settings = Settings(
                "mqttserver", "broker.example",
                "mqttport", "1883",
                "mqttuser", "",
                "lastConnectionSuccessful", "True");

            Assert.AreEqual(2, SettingsRules.CountConfiguredMqttSettings(settings));
        }

        [TestMethod]
        public void CountConfiguredMqttSettings_KeyMatchIsCaseSensitive()
        {
            Assert.AreEqual(0, SettingsRules.CountConfiguredMqttSettings(Settings("MqttServer", "broker.example")));
        }

        [TestMethod]
        public void CountConfiguredMqttSettings_Empty_ReturnsZero()
        {
            Assert.AreEqual(0, SettingsRules.CountConfiguredMqttSettings(Settings()));
        }

        [TestMethod]
        [DataRow("True", true, DisplayName = "True")]
        [DataRow("False", false, DisplayName = "False")]
        [DataRow("true", false, DisplayName = "lower-case true is not enabled")]
        [DataRow("", false, DisplayName = "empty")]
        public void IsTlsEnabled_ComparesExactText(string value, bool expected)
        {
            Assert.AreEqual(expected, SettingsRules.IsTlsEnabled(Settings("mqtttls", value)));
        }

        [TestMethod]
        public void IsTlsEnabled_MissingSetting_ReturnsFalse()
        {
            Assert.IsFalse(SettingsRules.IsTlsEnabled(Settings()));
        }

        // There is no range check: anything int.TryParse accepts passes.
        [TestMethod]
        [DataRow("1883", true, 1883, DisplayName = "normal port")]
        [DataRow(" 8883 ", true, 8883, DisplayName = "surrounding spaces")]
        [DataRow("0", true, 0, DisplayName = "zero")]
        [DataRow("-1", true, -1, DisplayName = "negative")]
        [DataRow("70000", true, 70000, DisplayName = "above 65535")]
        [DataRow("", false, 0, DisplayName = "empty")]
        [DataRow("abc", false, 0, DisplayName = "letters")]
        [DataRow("99999999999", false, 0, DisplayName = "overflow")]
        public void TryParsePort_AcceptsWhatIntTryParseAccepts(string text, bool expectedOk, int expectedPort)
        {
            bool ok = SettingsRules.TryParsePort(text, out int port);

            Assert.AreEqual(expectedOk, ok);
            Assert.AreEqual(expectedPort, port);
        }
    }
}
