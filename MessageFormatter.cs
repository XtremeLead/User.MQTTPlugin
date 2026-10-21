using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// </summary>
        internal static object ParsePayload(string payload, out bool isJson)
        {
            try
            {
                JToken token = JToken.Parse(payload);
                isJson = true;
                return token;
            }
            catch (Exception)
            {
                isJson = false;
                return payload;
            }
        }

        internal static string Format(string topic, object payload, DateTime receivedAt)
        {
            string timestamp = receivedAt.ToString(new CultureInfo("nl-NL"));

            Dictionary<string, dynamic> dMessage = new Dictionary<string, dynamic>();
            dMessage.Add("topic", topic.ToString());
            dMessage.Add("payload", payload);
            dMessage.Add("timestamp", timestamp);
            return JsonConvert.SerializeObject(dMessage);
        }
    }
}
