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
    /// Pins how MQTTClient.client_MqttMsgPublishReceived turns a received message into the
    /// MQTTMessage JSON that dashboards parse.
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
        private string Receive(string topic, byte[] payload)
        {
            try
            {
                Raise(topic, payload);
            }
            catch (NullReferenceException)
            {
            }
            return plugin.MqttMessage;
        }

        private JObject ReceiveJson(string payload)
        {
            return JObject.Parse(Receive("home/sensor", Encoding.UTF8.GetBytes(payload)));
        }

        [TestMethod]
        public void MessageReceived_WrapsAsTopicPayloadTimestampInThatOrder()
        {
            JObject message = ReceiveJson("on");

            CollectionAssert.AreEqual(new[] { "topic", "payload", "timestamp" }, message.Properties().Select(p => p.Name).ToArray());
            Assert.AreEqual("home/sensor", (string)message["topic"]);
        }

        [TestMethod]
        [DataRow("{\"a\":1}", "{\"a\":1}", DisplayName = "object stays an object")]
        [DataRow("[1,2]", "[1,2]", DisplayName = "array stays an array")]
        [DataRow("42", "42", DisplayName = "number becomes a number")]
        [DataRow("1.10", "1.1", DisplayName = "trailing zeros of a decimal are lost")]
        [DataRow("true", "true", DisplayName = "boolean becomes a boolean")]
        [DataRow("null", "null", DisplayName = "text null becomes JSON null")]
        [DataRow("\"hi\"", "\"hi\"", DisplayName = "JSON string is unquoted")]
        [DataRow("on", "\"on\"", DisplayName = "plain text becomes a string")]
        [DataRow("{broken", "\"{broken\"", DisplayName = "invalid JSON becomes a string")]
        [DataRow("", "\"\"", DisplayName = "empty payload becomes an empty string")]
        [DataRow("  {\"a\":1}  ", "{\"a\":1}", DisplayName = "surrounding whitespace is ignored")]
        public void MessageReceived_Payload_IsEmbeddedAsJson(string payload, string expectedPayloadJson)
        {
            JObject message = ReceiveJson(payload);

            Assert.AreEqual(expectedPayloadJson, message["payload"].ToString(Newtonsoft.Json.Formatting.None));
        }

        [TestMethod]
        public void MessageReceived_InvalidUtf8_IsDecodedWithReplacementCharacter()
        {
            JObject message = JObject.Parse(Receive("t", new byte[] { 0x61, 0xFF, 0x62 }));

            Assert.AreEqual("a�b", (string)message["payload"]);
        }

        [TestMethod]
        public void MessageReceived_UtcDateStringInJson_IsKeptAsWritten()
        {
            JObject message = ReceiveJson("{\"t\":\"2026-10-21T10:00:00Z\"}");

            Assert.AreEqual("{\"t\":\"2026-10-21T10:00:00Z\"}", message["payload"].ToString(Newtonsoft.Json.Formatting.None));
        }

        // Surprise: Newtonsoft parses date strings, so a date with an offset is rewritten
        // in the local time zone of the SimHub pc.
        [TestMethod]
        public void MessageReceived_DateStringWithOffsetInJson_IsRewrittenToLocalTime()
        {
            string raw = Receive("t", Encoding.UTF8.GetBytes("{\"t\":\"2026-10-21T10:00:00+05:00\"}"));

            DateTimeOffset local = DateTimeOffset.Parse("2026-10-21T10:00:00+05:00", CultureInfo.InvariantCulture).ToLocalTime();
            string expected = local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
            StringAssert.Contains(raw, "\"payload\":{\"t\":\"" + expected + "\"}");
        }

        [TestMethod]
        public void MessageReceived_Timestamp_IsLocalTimeInDutchFormat()
        {
            DateTime before = DateTime.Now.AddSeconds(-1);
            JObject message = ReceiveJson("on");
            DateTime after = DateTime.Now.AddSeconds(1);

            string timestamp = (string)message["timestamp"];
            DateTime parsed = DateTime.ParseExact(timestamp, "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
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
