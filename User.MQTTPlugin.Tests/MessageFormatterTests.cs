using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Pins the MQTTMessage JSON that dashboards parse.
    /// </summary>
    [TestClass]
    public class MessageFormatterTests
    {
        private static readonly DateTime ReceivedAt = new DateTime(2026, 3, 4, 5, 6, 7);

        private static string FormatPayload(string payload)
        {
            object parsed = MessageFormatter.ParsePayload(payload, out bool _);
            JObject message = JObject.Parse(MessageFormatter.Format("home/sensor", parsed, ReceivedAt));
            return message["payload"].ToString(Newtonsoft.Json.Formatting.None);
        }

        [TestMethod]
        [DataRow("{\"a\":1}", true, DisplayName = "object")]
        [DataRow("[1,2]", true, DisplayName = "array")]
        [DataRow("42", true, DisplayName = "number")]
        [DataRow("true", true, DisplayName = "boolean")]
        [DataRow("null", true, DisplayName = "null")]
        [DataRow("\"hi\"", true, DisplayName = "JSON string")]
        [DataRow("on", false, DisplayName = "plain text")]
        [DataRow("{broken", false, DisplayName = "invalid JSON")]
        [DataRow("", false, DisplayName = "empty")]
        public void ParsePayload_ReportsWhetherPayloadIsJson(string payload, bool expectedIsJson)
        {
            MessageFormatter.ParsePayload(payload, out bool isJson);

            Assert.AreEqual(expectedIsJson, isJson);
        }

        [TestMethod]
        public void ParsePayload_NotJson_ReturnsRawString()
        {
            object result = MessageFormatter.ParsePayload("{broken", out bool _);

            Assert.AreEqual("{broken", result);
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
        [DataRow("{\"t\":\"2026-10-21T10:00:00Z\"}", "{\"t\":\"2026-10-21T10:00:00Z\"}", DisplayName = "UTC date string is kept")]
        public void Format_Payload_IsEmbeddedAsJson(string payload, string expectedPayloadJson)
        {
            Assert.AreEqual(expectedPayloadJson, FormatPayload(payload));
        }

        // Surprise: Newtonsoft parses date strings, so a date with an offset is rewritten
        // in the local time zone of the SimHub pc.
        [TestMethod]
        public void Format_DateStringWithOffset_IsRewrittenToLocalTime()
        {
            DateTimeOffset local = DateTimeOffset.Parse("2026-10-21T10:00:00+05:00", CultureInfo.InvariantCulture).ToLocalTime();
            string expected = "{\"t\":\"" + local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture) + "\"}";

            Assert.AreEqual(expected, FormatPayload("{\"t\":\"2026-10-21T10:00:00+05:00\"}"));
        }

        [TestMethod]
        public void Format_WrapsAsTopicPayloadTimestampInThatOrder()
        {
            JObject message = JObject.Parse(MessageFormatter.Format("home/sensor", "on", ReceivedAt));

            CollectionAssert.AreEqual(new[] { "topic", "payload", "timestamp" }, message.Properties().Select(p => p.Name).ToArray());
            Assert.AreEqual("home/sensor", (string)message["topic"]);
        }

        // The format follows the nl-NL culture: no leading zero on day and month.
        [TestMethod]
        public void Format_Timestamp_UsesDutchCulture()
        {
            JObject message = JObject.Parse(MessageFormatter.Format("t", "on", ReceivedAt));

            Assert.AreEqual("4-3-2026 05:06:07", (string)message["timestamp"]);
        }

        [TestMethod]
        public void Format_ExactOutput_ForPlainTextPayload()
        {
            Assert.AreEqual(
                "{\"topic\":\"t\",\"payload\":\"on\",\"timestamp\":\"4-3-2026 05:06:07\"}",
                MessageFormatter.Format("t", "on", ReceivedAt));
        }
    }
}
