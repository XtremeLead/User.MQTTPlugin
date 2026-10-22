using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Turns a received MQTT message into the JSON that is exposed as MQTTPlugin.MQTTMessage.
    /// </summary>
    internal static class MessageFormatter
    {
        /// <summary>
        /// Parses the payload as JSON. Falls back to the raw string when it isn't valid JSON.
        /// Date strings stay strings and decimals keep their digits, so the payload comes
        /// through as the sender wrote it.
        /// </summary>
        internal static object ParsePayload(string payload, out bool isJson)
        {
            try
            {
                using (JsonTextReader reader = new JsonTextReader(new StringReader(payload)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Decimal;
                    JToken token = JToken.ReadFrom(reader);
                    // Like JToken.Parse: anything but comments after the value is invalid.
                    while (reader.Read())
                    {
                    }
                    isJson = true;
                    return token;
                }
            }
            catch (Exception)
            {
                isJson = false;
                return payload;
            }
        }

        internal static string Format(string topic, object payload, DateTimeOffset receivedAt)
        {
            string timestamp = receivedAt.DateTime.ToString(new CultureInfo("nl-NL"));

            Dictionary<string, dynamic> dMessage = new Dictionary<string, dynamic>();
            dMessage.Add("topic", topic.ToString());
            dMessage.Add("payload", payload);
            dMessage.Add("timestamp", timestamp);
            dMessage.Add("timestampIso", receivedAt.ToString("o", CultureInfo.InvariantCulture));
            return JsonConvert.SerializeObject(dMessage);
        }
    }
}
