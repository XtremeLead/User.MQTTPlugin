using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// The real M2Mqtt adapter, without a broker. An IP address skips DNS and the constructor
    /// doesn't connect; "broker.invalid" always fails DNS (RFC 6761).
    /// </summary>
    [TestClass]
    public class M2MqttClientTests
    {
        [TestMethod]
        public void Create_IpAddress_ReturnsDisconnectedClient()
        {
            IMqttClient client = new M2MqttClientFactory().Create("127.0.0.1", 1883, false);

            Assert.IsFalse(client.IsConnected);
        }

        [TestMethod]
        public void Create_UnresolvableHost_ThrowsSocketException()
        {
            Assert.ThrowsException<SocketException>(() => new M2MqttClientFactory().Create("broker.invalid", 1883, false));
        }

        [TestMethod]
        public void Disconnect_NotConnected_DoesNothing()
        {
            IMqttClient client = new M2MqttClientFactory().Create("127.0.0.1", 1883, false);

            client.Disconnect();

            Assert.IsFalse(client.IsConnected);
        }
    }
}
