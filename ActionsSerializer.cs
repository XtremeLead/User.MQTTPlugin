using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Reads the actions as stored in user.config before F4: a DataContractSerializer XML dump of
    /// action name to (topic, message). Only the migration (SettingsMigration) uses it.
    /// </summary>
    internal static class ActionsSerializer
    {
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
