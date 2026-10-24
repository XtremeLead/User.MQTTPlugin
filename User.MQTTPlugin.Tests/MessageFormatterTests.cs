using System;
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
        private static readonly DateTimeOffset ReceivedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(1));

        private static string Format(string payload)
        {
            object parsed = MessageFormatter.ParsePayload(payload, out bool _);
            return MessageFormatter.Format("home/sensor", parsed, ReceivedAt);
        }

        // Read without date parsing, so the expected values are compared as written.
        private static JObject ParseRaw(string json)
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }

        [TestMethod]
        [DataRow("{\"a\":1}", true, DisplayName = "object")]
        [DataRow("[1,2]", true, DisplayName = "array")]
        [DataRow("42", true, DisplayName = "number")]
        [DataRow("true", true, DisplayName = "boolean")]
        [DataRow("null", true, DisplayName = "null")]
        [DataRow("\"hi\"", true, DisplayName = "JSON string")]
        [DataRow("{\"a\":1} // note", true, DisplayName = "trailing comment")]
        [DataRow("on", false, DisplayName = "plain text")]
        [DataRow("{broken", false, DisplayName = "invalid JSON")]
        [DataRow("{\"a\":1} extra", false, DisplayName = "content after the value")]
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
        [DataRow("1.10", "1.10", DisplayName = "decimal keeps its trailing zero")]
        [DataRow("{\"v\":0.1}", "{\"v\":0.1}", DisplayName = "decimal is not rounded through double")]
        [DataRow("12345678901234567890", "12345678901234567890", DisplayName = "integer larger than long")]
        [DataRow("true", "true", DisplayName = "boolean becomes a boolean")]
        [DataRow("null", "null", DisplayName = "text null becomes JSON null")]
        [DataRow("\"hi\"", "\"hi\"", DisplayName = "JSON string is unquoted")]
        [DataRow("on", "\"on\"", DisplayName = "plain text becomes a string")]
        [DataRow("{broken", "\"{broken\"", DisplayName = "invalid JSON becomes a string")]
        [DataRow("", "\"\"", DisplayName = "empty payload becomes an empty string")]
        [DataRow("  {\"a\":1}  ", "{\"a\":1}", DisplayName = "surrounding whitespace is ignored")]
        public void Format_Payload_IsEmbeddedAsJson(string payload, string expectedPayloadJson)
        {
            StringAssert.Contains(Format(payload), "\"payload\":" + expectedPayloadJson + ",");
        }

        // B11: date strings stay exactly as the sender wrote them.
        [TestMethod]
        [DataRow("2026-10-21T10:00:00+05:00", DisplayName = "offset")]
        [DataRow("2026-10-21T10:00:00Z", DisplayName = "UTC")]
        [DataRow("2026-10-21T10:00:00", DisplayName = "no zone")]
        [DataRow("2026-10-21", DisplayName = "date only")]
        public void Format_DateStringInJson_IsKeptAsWritten(string date)
        {
            string formatted = Format("{\"t\":\"" + date + "\"}");

            StringAssert.Contains(formatted, "\"payload\":{\"t\":\"" + date + "\"}");
        }

        // A number too large for decimal can't be read with decimal parsing, so it is passed on
        // as the raw text, like any other payload that isn't valid JSON.
        [TestMethod]
        public void ParsePayload_NumberBeyondDecimalRange_FallsBackToString()
        {
            object result = MessageFormatter.ParsePayload("1e400", out bool isJson);

            Assert.IsFalse(isJson);
            Assert.AreEqual("1e400", result);
        }

        [TestMethod]
        public void Format_WrapsAsTopicPayloadTimestampTimestampIsoInThatOrder()
        {
            JObject message = ParseRaw(MessageFormatter.Format("home/sensor", "on", ReceivedAt));

            CollectionAssert.AreEqual(new[] { "topic", "payload", "timestamp", "timestampIso" }, message.Properties().Select(p => p.Name).ToArray());
            Assert.AreEqual("home/sensor", (string)message["topic"]);
        }

        // The format follows the nl-NL culture: no leading zero on day and month. It shows the
        // clock time of the given moment, without converting it.
        [TestMethod]
        public void Format_Timestamp_UsesDutchCulture()
        {
            JObject message = ParseRaw(MessageFormatter.Format("t", "on", ReceivedAt));

            Assert.AreEqual("4-3-2026 05:06:07", (string)message["timestamp"]);
        }

        // D2: timestampIso is ISO 8601 with the offset of the moment.
        [TestMethod]
        public void Format_TimestampIso_IsRoundTripFormatWithOffset()
        {
            JObject message = ParseRaw(MessageFormatter.Format("t", "on", ReceivedAt));

            Assert.AreEqual("2026-03-04T05:06:07.0000000+01:00", (string)message["timestampIso"]);
        }

        [TestMethod]
        public void Format_ExactOutput_ForPlainTextPayload()
        {
            Assert.AreEqual(
                "{\"topic\":\"t\",\"payload\":\"on\",\"timestamp\":\"4-3-2026 05:06:07\",\"timestampIso\":\"2026-03-04T05:06:07.0000000+01:00\"}",
                MessageFormatter.Format("t", "on", ReceivedAt));
        }
    }
}
