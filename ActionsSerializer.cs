using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Reads and writes the mqttactions setting: a DataContractSerializer XML dump of
    /// action name to (topic, message).
    /// </summary>
    internal static class ActionsSerializer
    {
        internal static string Serialize(Dictionary<string, ValueTuple<string, string>> actions)
        {
            DataContractSerializer serializer = new DataContractSerializer(actions.GetType());
            using (StringWriter sw = new StringWriter())
            using (XmlTextWriter writer = new XmlTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.WriteObject(writer, actions);
                writer.Flush();
                return sw.ToString();
            }
        }

        internal static Dictionary<string, ValueTuple<string, string>> Deserialize(string xml)
        {
            using (Stream stream = new MemoryStream())
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(xml);
                stream.Write(data, 0, data.Length);
                stream.Position = 0;
                DataContractSerializer deserializer = new DataContractSerializer(typeof(Dictionary<string, ValueTuple<string, string>>));
                return (Dictionary<string, ValueTuple<string, string>>)deserializer.ReadObject(stream);
            }
        }
    }
}
