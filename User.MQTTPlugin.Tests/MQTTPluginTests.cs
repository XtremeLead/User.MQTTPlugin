using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class MQTTPluginTests
    {
        [TestMethod]
        public void MqttMessage_NewPlugin_IsEmpty()
        {
            Assert.AreEqual("", new MQTTPlugin().MqttMessage);
        }

        [TestMethod]
        public void LeftMenuTitle_IsMqttPlugin()
        {
            Assert.AreEqual("MQTT plugin", new MQTTPlugin().LeftMenuTitle);
        }

        // Actions and the MQTTMessage property appear in SimHub as "MQTTPlugin.<name>", and
        // dashboards and control mappings refer to that name, so the class name must stay.
        [TestMethod]
        public void PluginClass_IsNamedMQTTPlugin()
        {
            Assert.AreEqual("MQTTPlugin", typeof(MQTTPlugin).Name);
        }
    }
}
