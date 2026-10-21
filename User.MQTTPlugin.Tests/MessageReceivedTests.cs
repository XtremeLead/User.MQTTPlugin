using System;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Covers MQTTClient.client_MqttMsgPublishReceived end to end: decoding the bytes and
    /// handing the formatted message to the plugin. The formatting itself is in MessageFormatterTests.
    /// </summary>
    [TestClass]
    public class MessageReceivedTests
    {
        private MQTTPlugin plugin;

        [TestInitialize]
        public void Initialize()
        {
            TestState.Reset();
            plugin = new MQTTPlugin();
            TestState.SetPluginInstance(plugin);
        }

        [TestCleanup]
        public void Cleanup() => TestState.Reset();

        private static void Raise(string topic, byte[] payload)
        {
            var args = new MqttMsgPublishEventArgs(topic, payload, false, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
            TestState.InvokeStatic(typeof(MQTTClient), "client_MqttMsgPublishReceived", null, args);
        }

        /// <summary>
        /// Delivers a message and returns the resulting MQTTMessage. Outside SimHub the
        /// MqttMessage setter's AttachDelegate call throws after the value is stored (see
        /// MessageReceived_OutsideSimHub_SetterThrowsAfterStoringValue), so that is ignored here.
        /// </summary>
        private JObject Receive(string topic, byte[] payload)
        {
            try
            {
                Raise(topic, payload);
            }
            catch (NullReferenceException)
            {
            }
            return JObject.Parse(plugin.MqttMessage);
        }

        [TestMethod]
        public void MessageReceived_SetsFormattedMessageOnPlugin()
        {
            JObject message = Receive("home/sensor", Encoding.UTF8.GetBytes("{\"a\":1}"));

            Assert.AreEqual("home/sensor", (string)message["topic"]);
            Assert.AreEqual(1, (int)message["payload"]["a"]);
        }

        [TestMethod]
        public void MessageReceived_InvalidUtf8_IsDecodedWithReplacementCharacter()
        {
            JObject message = Receive("t", new byte[] { 0x61, 0xFF, 0x62 });

            Assert.AreEqual("a�b", (string)message["payload"]);
        }

        [TestMethod]
        public void MessageReceived_Timestamp_IsCurrentLocalTime()
        {
            DateTime before = DateTime.Now.AddSeconds(-1);
            JObject message = Receive("t", Encoding.UTF8.GetBytes("on"));
            DateTime after = DateTime.Now.AddSeconds(1);

            string timestamp = (string)message["timestamp"];
            DateTime parsed = DateTime.Parse(timestamp, new CultureInfo("nl-NL"));
            Assert.IsTrue(parsed >= before && parsed <= after, timestamp);
        }

        // Current behaviour (B8): the setter calls AttachDelegate, which fails without a
        // PluginManager. Inside SimHub it succeeds; the value is stored first either way.
        [TestMethod]
        public void MessageReceived_OutsideSimHub_SetterThrowsAfterStoringValue()
        {
            Assert.ThrowsException<NullReferenceException>(() => Raise("t", Encoding.UTF8.GetBytes("on")));
            StringAssert.Contains(plugin.MqttMessage, "\"payload\":\"on\"");
        }

        [TestMethod]
        public void MessageReceived_NoPluginInstance_DoesNotThrow()
        {
            TestState.SetPluginInstance(null);

            Raise("t", Encoding.UTF8.GetBytes("on"));

            Assert.AreEqual("", plugin.MqttMessage);
        }
    }
}
