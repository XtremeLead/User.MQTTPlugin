using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Pins the XML format of the mqttactions setting in user.config before F4. The migration
    /// must keep reading it.
    /// </summary>
    [TestClass]
    public class ActionsSerializationTests
    {
        /// <summary>Exactly what the plugin wrote to user.config for one action before F4.</summary>
        internal const string SingleActionXml =
            "<ArrayOfKeyValueOfstringValueTupleOfstringstringU6ho3Bhd xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\">\r\n" +
            "  <KeyValueOfstringValueTupleOfstringstringU6ho3Bhd>\r\n" +
            "    <Key>ToggleClimate</Key>\r\n" +
            "    <Value xmlns:d3p1=\"http://schemas.datacontract.org/2004/07/System\">\r\n" +
            "      <d3p1:Item1>simhub/climate</d3p1:Item1>\r\n" +
            "      <d3p1:Item2>toggle</d3p1:Item2>\r\n" +
            "    </Value>\r\n" +
            "  </KeyValueOfstringValueTupleOfstringstringU6ho3Bhd>\r\n" +
            "</ArrayOfKeyValueOfstringValueTupleOfstringstringU6ho3Bhd>";

        /// <summary>Writes actions the way the plugin did before F4 (the removed SerializeDict).</summary>
        internal static string LegacyXml(Dictionary<string, ValueTuple<string, string>> actions)
        {
            var serializer = new DataContractSerializer(actions.GetType());
            using (var sw = new StringWriter())
            using (var writer = new XmlTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.WriteObject(writer, actions);
                writer.Flush();
                return sw.ToString();
            }
        }

        [TestMethod]
        public void LegacyXml_SingleAction_MatchesStoredFormat()
        {
            var actions = new Dictionary<string, ValueTuple<string, string>> { { "ToggleClimate", ("simhub/climate", "toggle") } };

            Assert.AreEqual(SingleActionXml, LegacyXml(actions));
        }

        [TestMethod]
        public void Deserialize_StoredXml_ReturnsActions()
        {
            Dictionary<string, ValueTuple<string, string>> actions = ActionsSerializer.Deserialize(SingleActionXml);

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(("simhub/climate", "toggle"), actions["ToggleClimate"]);
        }

        [TestMethod]
        public void Deserialize_SpecialCharacters_KeepsValuesAndOrder()
        {
            var actions = new Dictionary<string, ValueTuple<string, string>>
            {
                { "Zeta", ("a/b", "{\"x\":1}") },
                { "Alpha & <Beta>", ("home/ünï", "'quoted' \"text\"") },
                { "Empty", ("", "") },
            };

            Dictionary<string, ValueTuple<string, string>> result = ActionsSerializer.Deserialize(LegacyXml(actions));

            CollectionAssert.AreEqual(actions.Keys.ToList(), result.Keys.ToList());
            foreach (var action in actions)
            {
                Assert.AreEqual(action.Value, result[action.Key], action.Key);
            }
        }

        [TestMethod]
        public void Deserialize_EmptyDictionary_ReturnsEmpty()
        {
            Assert.AreEqual(0, ActionsSerializer.Deserialize(LegacyXml(new Dictionary<string, ValueTuple<string, string>>())).Count);
        }

        [TestMethod]
        public void Deserialize_InvalidXml_ThrowsSerializationException()
        {
            Assert.ThrowsException<SerializationException>(() => ActionsSerializer.Deserialize("<not-closed>"));
        }

        [TestMethod]
        public void Deserialize_EmptyString_ThrowsXmlException()
        {
            Assert.ThrowsException<XmlException>(() => ActionsSerializer.Deserialize(""));
        }
    }
}
