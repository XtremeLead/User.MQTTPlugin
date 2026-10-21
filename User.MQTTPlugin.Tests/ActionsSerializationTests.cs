using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Pins the XML format of the mqttactions setting. Existing user.config files contain this
    /// format, so F3/F4 must keep reading it.
    /// </summary>
    [TestClass]
    public class ActionsSerializationTests
    {
        private const string SingleActionXml =
            "<ArrayOfKeyValueOfstringValueTupleOfstringstringU6ho3Bhd xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\">\r\n" +
            "  <KeyValueOfstringValueTupleOfstringstringU6ho3Bhd>\r\n" +
            "    <Key>ToggleClimate</Key>\r\n" +
            "    <Value xmlns:d3p1=\"http://schemas.datacontract.org/2004/07/System\">\r\n" +
            "      <d3p1:Item1>simhub/climate</d3p1:Item1>\r\n" +
            "      <d3p1:Item2>toggle</d3p1:Item2>\r\n" +
            "    </Value>\r\n" +
            "  </KeyValueOfstringValueTupleOfstringstringU6ho3Bhd>\r\n" +
            "</ArrayOfKeyValueOfstringValueTupleOfstringstringU6ho3Bhd>";

        [TestMethod]
        public void Serialize_SingleAction_ProducesIndentedDataContractXml()
        {
            var actions = new Dictionary<string, ValueTuple<string, string>> { { "ToggleClimate", ("simhub/climate", "toggle") } };

            Assert.AreEqual(SingleActionXml, ActionsSerializer.Serialize(actions));
        }

        [TestMethod]
        public void Deserialize_StoredXml_ReturnsActions()
        {
            Dictionary<string, ValueTuple<string, string>> actions = ActionsSerializer.Deserialize(SingleActionXml);

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(("simhub/climate", "toggle"), actions["ToggleClimate"]);
        }

        [TestMethod]
        public void Serialize_ThenDeserialize_KeepsSpecialCharactersAndOrder()
        {
            var actions = new Dictionary<string, ValueTuple<string, string>>
            {
                { "Zeta", ("a/b", "{\"x\":1}") },
                { "Alpha & <Beta>", ("home/ünï", "'quoted' \"text\"") },
                { "Empty", ("", "") },
            };

            Dictionary<string, ValueTuple<string, string>> result = ActionsSerializer.Deserialize(ActionsSerializer.Serialize(actions));

            CollectionAssert.AreEqual(actions.Keys.ToList(), result.Keys.ToList());
            foreach (var action in actions)
            {
                Assert.AreEqual(action.Value, result[action.Key], action.Key);
            }
        }

        [TestMethod]
        public void Serialize_ThenDeserialize_EmptyDictionary_ReturnsEmpty()
        {
            var result = ActionsSerializer.Deserialize(ActionsSerializer.Serialize(new Dictionary<string, ValueTuple<string, string>>()));

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Deserialize_InvalidXml_ThrowsSerializationException()
        {
            Assert.ThrowsException<SerializationException>(() => ActionsSerializer.Deserialize("<not-closed>"));
        }

        // LoadExistingActions skips an empty setting, so this path isn't reached from the UI.
        [TestMethod]
        public void Deserialize_EmptyString_ThrowsXmlException()
        {
            Assert.ThrowsException<System.Xml.XmlException>(() => ActionsSerializer.Deserialize(""));
        }

        // The legacy SettingsControl keeps its own copy until F5 removes it (D3).
        [TestMethod]
        public void LegacyDeserializeDict_StoredXml_ReturnsSameActions()
        {
            var actions = (Dictionary<string, ValueTuple<string, string>>)SettingsControl.DeserializeDict(SingleActionXml, typeof(Dictionary<string, ValueTuple<string, string>>));

            CollectionAssert.AreEqual(ActionsSerializer.Deserialize(SingleActionXml).ToList(), actions.ToList());
        }
    }
}
