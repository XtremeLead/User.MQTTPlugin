using System;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace User.MQTTPlugin
{
    /// <summary>
    /// The part of an MQTT client that MqttConnectionManager uses. Tests replace it with a fake.
    /// </summary>
    internal interface IMqttClient
    {
        bool IsConnected { get; }

        /// <summary>Raised on the client's receive thread with the topic and raw payload.</summary>
        event Action<string, byte[]> MessageReceived;

        /// <summary>Raised when the connection ends, also after Disconnect().</summary>
        event Action ConnectionClosed;

        /// <summary>Blocks until connected or failed; throws on network errors.</summary>
        void Connect(string clientId, string username, string password);

        void Subscribe(string topic);
        void Publish(string topic, byte[] payload);
        void Disconnect();
    }

    internal interface IMqttClientFactory
    {
        /// <summary>May throw, for example when the host name can't be resolved.</summary>
        IMqttClient Create(string broker, int port, bool useTls);
    }

    internal sealed class M2MqttClientFactory : IMqttClientFactory
    {
        public IMqttClient Create(string broker, int port, bool useTls)
        {
            // The M2Mqtt constructor resolves the host name, so it throws when DNS fails.
            return new M2MqttClient(new MqttClient(
                broker,
                port,
                useTls,
                useTls ? MqttSslProtocols.TLSv1_2 : MqttSslProtocols.None,
                null,
                null));
        }
    }

    internal sealed class M2MqttClient : IMqttClient
    {
        private readonly MqttClient client;

        public M2MqttClient(MqttClient client)
        {
            this.client = client;
            client.MqttMsgPublishReceived += (sender, e) => MessageReceived?.Invoke(e.Topic, e.Message);
            client.ConnectionClosed += (sender, e) => ConnectionClosed?.Invoke();
        }

        public event Action<string, byte[]> MessageReceived;
        public event Action ConnectionClosed;

        public bool IsConnected
        {
            get
            {
                try
                {
                    return client.IsConnected;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public void Connect(string clientId, string username, string password)
        {
            client.Connect(clientId, username, password);
        }

        public void Subscribe(string topic)
        {
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        }

        public void Publish(string topic, byte[] payload)
        {
            client.Publish(topic, payload);
        }

        public void Disconnect()
        {
            if (IsConnected) client.Disconnect();
        }
    }
}
