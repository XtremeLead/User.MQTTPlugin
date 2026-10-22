using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
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
        public void Store_ByDefault_IsPropertiesSettings()
        {
            Assert.IsInstanceOfType(TestState.OriginalStore, typeof(PropertiesSettingsStore));
        }

        [TestMethod]
        public void Reconnect_ByDefault_IsConnectAndSubscribeAsync()
        {
            Assert.AreEqual(typeof(MQTTClient).GetMethod("ConnectAndSubscribeAsync"), TestState.OriginalReconnect.Method);
        }

        [TestMethod]
        public void LoadSettings_ReturnsEverySettingAsString()
        {
            IDictionary<string, string> settings = MQTTSettings.LoadSettings();

            Assert.AreEqual(8, settings.Count);
            Assert.AreEqual("", settings["mqttserver"]);
            Assert.AreEqual("False", settings["mqtttls"]);
            Assert.AreEqual("False", settings["lastConnectionSuccessful"]);
        }

        [TestMethod]
        public void LoadSettings_BooleanSettingTrue_ReturnsTrueText()
        {
            TestState.Store["mqtttls"] = true;

            Assert.AreEqual("True", MQTTSettings.LoadSettings()["mqtttls"]);
        }

        [TestMethod]
        public void LoadSettings_StringSetting_ReturnsValue()
        {
            TestState.Store["mqttserver"] = "broker.example";

            Assert.AreEqual("broker.example", MQTTSettings.LoadSettings()["mqttserver"]);
        }

        [TestMethod]
        public void GetSetting_ReturnsValueAsString()
        {
            TestState.Store["mqttport"] = "1883";

            Assert.AreEqual("1883", new MQTTSettings().GetSetting("mqttport"));
            Assert.AreEqual("False", new MQTTSettings().GetSetting("mqtttls"));
        }

        [TestMethod]
        public void RememberSetting_DoesNotChangeSettingsBeforeSave()
        {
            MQTTSettings.RememberSetting("mqttserver", "broker.example");

            Assert.AreEqual("", MQTTSettings.LoadSettings()["mqttserver"]);
            Assert.AreEqual(0, TestState.Store.SaveCount);
        }

        [TestMethod]
        public void SaveSettingsAsync_StagedStrings_WritesSavesOnceThenReconnects()
        {
            var steps = new List<string>();
            var reconnectTask = new TaskCompletionSource<bool>().Task;
            MQTTSettings.Reconnect = () =>
            {
                steps.Add("reconnect after " + TestState.Store.SaveCount + " save(s)");
                return reconnectTask;
            };
            MQTTSettings.RememberSetting("mqttserver", "broker.example");
            MQTTSettings.RememberSetting("mqttport", "1883");

            Task result = MQTTSettings.SaveSettingsAsync();

            Assert.AreEqual("broker.example", TestState.Store["mqttserver"]);
            Assert.AreEqual("1883", TestState.Store["mqttport"]);
            CollectionAssert.AreEqual(new[] { "reconnect after 1 save(s)" }, steps);
            Assert.AreSame(reconnectTask, result);
        }

        // Staged values are never cleared, so a later save writes them again.
        [TestMethod]
        public void SaveSettingsAsync_SecondSave_WritesEarlierStagedValuesAgain()
        {
            MQTTSettings.RememberSetting("mqttserver", "first.example");
            MQTTSettings.SaveSettingsAsync();
            TestState.Store["mqttserver"] = "changed.elsewhere";

            MQTTSettings.SaveSettingsAsync();

            Assert.AreEqual("first.example", TestState.Store["mqttserver"]);
            Assert.AreEqual(2, TestState.Store.SaveCount);
        }

        // B2: the UI stages mqtttls as text; it is converted to the bool the setting expects.
        [TestMethod]
        [DataRow("True", true, DisplayName = "True")]
        [DataRow("False", false, DisplayName = "False")]
        public void SaveSettingsAsync_TlsStagedAsText_SavesAsBool(string staged, bool expected)
        {
            int reconnects = 0;
            MQTTSettings.Reconnect = () => { reconnects++; return Task.CompletedTask; };
            TestState.Store["mqtttls"] = !expected;
            MQTTSettings.RememberSetting("mqtttls", staged);

            MQTTSettings.SaveSettingsAsync();

            Assert.AreEqual(expected, TestState.Store["mqtttls"]);
            Assert.AreEqual(1, TestState.Store.SaveCount);
            Assert.AreEqual(1, reconnects);
        }

        // B2: a value that can't be converted stops the whole save: nothing is written, the
        // staged values are discarded so the next save isn't blocked, and the error is logged.
        [TestMethod]
        public void SaveSettingsAsync_UnconvertibleValue_WritesNothingAndDiscardsStaging()
        {
            int reconnects = 0;
            MQTTSettings.Reconnect = () => { reconnects++; return Task.CompletedTask; };
            MQTTSettings.RememberSetting("mqttserver", "broker.example");
            MQTTSettings.RememberSetting("mqtttls", "yes");

            using (var log = new LogCapture())
            {
                Assert.ThrowsException<FormatException>(() => MQTTSettings.SaveSettingsAsync());
                Assert.IsTrue(log.Contains(log4net.Core.Level.Error, "Could not save MQTT settings"));
            }
            Assert.AreEqual("", TestState.Store["mqttserver"]);
            Assert.AreEqual(false, TestState.Store["mqtttls"]);
            Assert.AreEqual(0, TestState.Store.SaveCount);
            Assert.AreEqual(0, reconnects);

            MQTTSettings.SaveSettingsAsync();

            Assert.AreEqual("", TestState.Store["mqttserver"]);
            Assert.AreEqual(1, TestState.Store.SaveCount);
        }

        [TestMethod]
        public void SaveSettingsAsync_UnknownSetting_ThrowsNotFoundAndWritesNothing()
        {
            MQTTSettings.RememberSetting("mqttserver", "broker.example");
            MQTTSettings.RememberSetting("doesNotExist", "x");

            Assert.ThrowsException<SettingsPropertyNotFoundException>(() => MQTTSettings.SaveSettingsAsync());
            Assert.AreEqual("", TestState.Store["mqttserver"]);
            Assert.AreEqual(0, TestState.Store.SaveCount);
        }
    }
}
