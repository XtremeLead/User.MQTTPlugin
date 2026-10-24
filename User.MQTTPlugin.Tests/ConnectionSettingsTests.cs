using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class ConnectionSettingsTests
    {
        private static MQTTPluginSettings Saved()
        {
            return new MQTTPluginSettings
            {
                Server = "broker.test",
                Port = 8883,
                Username = "user",
                EncryptedPassword = Crypt.ProtectString("secret"),
                Topic = "home/#",
                UseTls = true,
            };
        }

        [TestMethod]
        public void FromSettings_Complete_ReturnsAllValuesWithDecryptedPassword()
        {
            ConnectionSettings settings = ConnectionSettings.FromSettings(Saved(), out ConnectionSettingsProblem problem);

            Assert.AreEqual(ConnectionSettingsProblem.None, problem);
            Assert.AreEqual("broker.test", settings.Broker);
            Assert.AreEqual(8883, settings.Port);
            Assert.AreEqual("user", settings.Username);
            Assert.AreEqual("secret", settings.Password);
            Assert.AreEqual("home/#", settings.Topic);
            Assert.IsTrue(settings.UseTls);
        }

        [TestMethod]
        public void FromSettings_NoBroker_ReturnsNullWithNoBroker()
        {
            MQTTPluginSettings saved = Saved();
            saved.Server = "";

            Assert.IsNull(ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem problem));
            Assert.AreEqual(ConnectionSettingsProblem.NoBroker, problem);
        }

        // The broker is checked first: without one, an invalid port is not worth a warning.
        [TestMethod]
        public void FromSettings_NoBrokerAndInvalidPort_ReportsNoBroker()
        {
            MQTTPluginSettings saved = Saved();
            saved.Server = "";
            saved.Port = 0;

            ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem problem);

            Assert.AreEqual(ConnectionSettingsProblem.NoBroker, problem);
        }

        [TestMethod]
        [DataRow(0, DisplayName = "0 (an invalid migrated port)")]
        [DataRow(70000, DisplayName = "above 65535")]
        public void FromSettings_InvalidPort_ReturnsNullWithInvalidPort(int port)
        {
            MQTTPluginSettings saved = Saved();
            saved.Port = port;

            Assert.IsNull(ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem problem));
            Assert.AreEqual(ConnectionSettingsProblem.InvalidPort, problem);
        }

        [TestMethod]
        public void FromSettings_NoPassword_ReturnsEmptyPassword()
        {
            MQTTPluginSettings saved = Saved();
            saved.EncryptedPassword = "";

            Assert.AreEqual("", ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem _).Password);
        }

        [TestMethod]
        public void FromSettings_NullTexts_BecomeEmpty()
        {
            MQTTPluginSettings saved = Saved();
            saved.Username = null;
            saved.Topic = null;

            ConnectionSettings settings = ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem _);

            Assert.AreEqual("", settings.Username);
            Assert.AreEqual("", settings.Topic);
        }
    }
}
