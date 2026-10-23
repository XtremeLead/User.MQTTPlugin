using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class ConnectionSettingsTests
    {
        private static Dictionary<string, string> Saved()
        {
            return new Dictionary<string, string>
            {
                { "mqttserver", "broker.test" },
                { "mqttport", "8883" },
                { "mqttuser", "user" },
                { "mqttpass", Crypt.ProtectString("secret") },
                { "mqtttopic", "home/#" },
                { "mqtttls", "True" },
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
            var saved = Saved();
            saved["mqttserver"] = "";

            Assert.IsNull(ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem problem));
            Assert.AreEqual(ConnectionSettingsProblem.NoBroker, problem);
        }

        // The broker is checked first: without one, an empty port is not worth a warning.
        [TestMethod]
        public void FromSettings_NoBrokerAndNoPort_ReportsNoBroker()
        {
            var saved = Saved();
            saved["mqttserver"] = "";
            saved["mqttport"] = "";

            ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem problem);

            Assert.AreEqual(ConnectionSettingsProblem.NoBroker, problem);
        }

        [TestMethod]
        public void FromSettings_InvalidPort_ReturnsNullWithInvalidPort()
        {
            var saved = Saved();
            saved["mqttport"] = "abc";

            Assert.IsNull(ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem problem));
            Assert.AreEqual(ConnectionSettingsProblem.InvalidPort, problem);
        }

        [TestMethod]
        public void FromSettings_NoPassword_ReturnsEmptyPassword()
        {
            var saved = Saved();
            saved["mqttpass"] = "";

            Assert.AreEqual("", ConnectionSettings.FromSettings(saved, out ConnectionSettingsProblem _).Password);
        }
    }
}
