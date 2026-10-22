using System;
using System.Globalization;
using System.Linq;
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

        private JObject Receive(string topic, byte[] payload)
        {
            Raise(topic, payload);
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

        [TestMethod]
        public void MessageReceived_TimestampIso_IsCurrentTimeWithOffset()
        {
            DateTimeOffset before = DateTimeOffset.Now.AddSeconds(-1);
            Raise("t", Encoding.UTF8.GetBytes("on"));
            DateTimeOffset after = DateTimeOffset.Now.AddSeconds(1);

            string iso;
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(plugin.MqttMessage)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
            {
                iso = (string)JObject.Load(reader)["timestampIso"];
            }
            DateTimeOffset parsed = DateTimeOffset.ParseExact(iso, "o", CultureInfo.InvariantCulture);
            Assert.IsTrue(parsed >= before && parsed <= after, iso);
            Assert.AreEqual(DateTimeOffset.Now.Offset, parsed.Offset, iso);
        }

        // B8: the setter no longer calls AttachDelegate (Init attaches the property once), so it
        // works without a PluginManager and from any thread.
        [TestMethod]
        public void MessageReceived_OutsideSimHub_SetsMessageWithoutError()
        {
            Raise("t", Encoding.UTF8.GetBytes("on"));

            StringAssert.Contains(plugin.MqttMessage, "\"payload\":\"on\"");
        }

        // B10: received messages are logged at Debug only, including plain-text payloads.
        [TestMethod]
        [DataRow("{\"a\":1}", DisplayName = "JSON payload")]
        [DataRow("on", DisplayName = "plain-text payload")]
        public void MessageReceived_LogsOnlyAtDebug(string payload)
        {
            using (var log = new LogCapture())
            {
                Raise("home/sensor", Encoding.UTF8.GetBytes(payload));

                Assert.IsTrue(log.Contains(log4net.Core.Level.Debug, "Received MQTT " + payload + " from topic home/sensor"));
                CollectionAssert.AreEqual(new[] { log4net.Core.Level.Debug }, log.Events.Select(e => e.Level).Distinct().ToArray());
            }
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
