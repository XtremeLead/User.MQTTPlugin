using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class SettingsRulesTests
    {
        // B7: a fresh install has a port and TLS off, but no broker, so it has no settings.
        [TestMethod]
        public void HasBrokerConfigured_FreshInstall_ReturnsFalse()
        {
            Assert.IsFalse(SettingsRules.HasBrokerConfigured(new MQTTPluginSettings()));
        }

        [TestMethod]
        public void HasBrokerConfigured_BrokerSet_ReturnsTrue()
        {
            Assert.IsTrue(SettingsRules.HasBrokerConfigured(new MQTTPluginSettings { Server = "broker.example" }));
        }

        [TestMethod]
        public void HasBrokerConfigured_OtherSettingsWithoutBroker_ReturnsFalse()
        {
            var settings = new MQTTPluginSettings { Server = "", Port = 1883, Topic = "home/#", UseTls = true };

            Assert.IsFalse(SettingsRules.HasBrokerConfigured(settings));
        }

        [TestMethod]
        public void HasBrokerConfigured_NullServer_ReturnsFalse()
        {
            Assert.IsFalse(SettingsRules.HasBrokerConfigured(new MQTTPluginSettings { Server = null }));
        }

        // The TLS flag in user.config before F4, read by the migration.
        [TestMethod]
        [DataRow("True", true, DisplayName = "True")]
        [DataRow("False", false, DisplayName = "False")]
        [DataRow("true", false, DisplayName = "lower-case true is not enabled")]
        [DataRow("", false, DisplayName = "empty")]
        public void IsTlsEnabled_ComparesExactText(string value, bool expected)
        {
            Assert.AreEqual(expected, SettingsRules.IsTlsEnabled(new Dictionary<string, string> { { "mqtttls", value } }));
        }

        [TestMethod]
        public void IsTlsEnabled_MissingSetting_ReturnsFalse()
        {
            Assert.IsFalse(SettingsRules.IsTlsEnabled(new Dictionary<string, string>()));
        }

        [TestMethod]
        [DataRow(1, true, DisplayName = "lowest port")]
        [DataRow(65535, true, DisplayName = "highest port")]
        [DataRow(0, false, DisplayName = "zero")]
        [DataRow(65536, false, DisplayName = "above 65535")]
        [DataRow(-1, false, DisplayName = "negative")]
        public void IsValidPort_AcceptsOneTo65535(int port, bool expected)
        {
            Assert.AreEqual(expected, SettingsRules.IsValidPort(port));
        }

        [TestMethod]
        [DataRow("1883", true, 1883, DisplayName = "normal port")]
        [DataRow(" 8883 ", true, 8883, DisplayName = "surrounding spaces")]
        [DataRow("65535", true, 65535, DisplayName = "highest port")]
        [DataRow("0", false, 0, DisplayName = "zero")]
        [DataRow("-1", false, 0, DisplayName = "negative")]
        [DataRow("70000", false, 0, DisplayName = "above 65535")]
        [DataRow("", false, 0, DisplayName = "empty")]
        [DataRow("abc", false, 0, DisplayName = "letters")]
        [DataRow("99999999999", false, 0, DisplayName = "overflow")]
        public void TryParsePort_AcceptsOnlyValidPorts(string text, bool expectedOk, int expectedPort)
        {
            bool ok = SettingsRules.TryParsePort(text, out int port);

            Assert.AreEqual(expectedOk, ok);
            Assert.AreEqual(expectedPort, port);
        }
    }
}
