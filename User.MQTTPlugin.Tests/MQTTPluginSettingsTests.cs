using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// SimHub stores the settings as JSON with Newtonsoft (WoteverCommon.JsonExtensions); these
    /// tests use plain JsonConvert as a stand-in for that round trip.
    /// </summary>
    [TestClass]
    public class MQTTPluginSettingsTests
    {
        private static MQTTPluginSettings Full()
        {
            return new MQTTPluginSettings
            {
                SettingsVersion = 1,
                Server = "172.16.4.10",
                Port = 8883,
                Username = "user",
                EncryptedPassword = "AQID",
                Topic = "simhub/status/#",
                UseTls = true,
                Actions =
                {
                    new MqttActionSetting("Light", "home/light", "on"),
                    new MqttActionSetting("Climate", "home/climate", "toggle"),
                },
            };
        }

        [TestMethod]
        public void Json_RoundTrip_KeepsEveryValue()
        {
            MQTTPluginSettings result = JsonConvert.DeserializeObject<MQTTPluginSettings>(JsonConvert.SerializeObject(Full()));

            Assert.AreEqual(1, result.SettingsVersion);
            Assert.AreEqual("172.16.4.10", result.Server);
            Assert.AreEqual(8883, result.Port);
            Assert.AreEqual("user", result.Username);
            Assert.AreEqual("AQID", result.EncryptedPassword);
            Assert.AreEqual("simhub/status/#", result.Topic);
            Assert.IsTrue(result.UseTls);
            CollectionAssert.AreEqual(new[] { "Light", "Climate" }, result.Actions.Select(a => a.Name).ToArray());
            Assert.AreEqual("toggle", result.Actions[1].Message);
        }

        // The file as it is before F4: it must count as not migrated yet. Its template leftover
        // SpeedWarningLevel is no longer in the model and is ignored (A5).
        [TestMethod]
        public void Json_FileFromBeforeF4_IsVersionZeroWithDefaults()
        {
            MQTTPluginSettings result = JsonConvert.DeserializeObject<MQTTPluginSettings>("{\"SpeedWarningLevel\":100}");

            Assert.AreEqual(0, result.SettingsVersion);
            Assert.AreEqual("", result.Server);
            Assert.AreEqual(1883, result.Port);
            Assert.AreEqual(0, result.Actions.Count);
        }

        [TestMethod]
        public void Clone_CopiesValuesAndActionsDeeply()
        {
            MQTTPluginSettings original = Full();

            MQTTPluginSettings clone = original.Clone();
            clone.Actions[0].Name = "Changed";
            clone.Actions.Clear();
            clone.Server = "other";

            Assert.AreEqual("Light", original.Actions[0].Name);
            Assert.AreEqual(2, original.Actions.Count);
            Assert.AreEqual("172.16.4.10", original.Server);
        }

        [TestMethod]
        public void Clone_NullActions_GivesEmptyList()
        {
            MQTTPluginSettings clone = new MQTTPluginSettings { Actions = null }.Clone();

            Assert.AreEqual(0, clone.Actions.Count);
        }
    }
}
