using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using System.IO;
using uPLibrary.Networking.M2Mqtt.Messages;
using User.MQTTPlugin;
using System.Globalization;

namespace User.MQTTPlugin
{
    // https://docs.emqx.com/en/cloud/latest/connect_to_deployments/c_sharp_sdk.html
    public class MQTTClient
    {

        public static MqttClient CLIENT = null;

        public static void ConnectMQTT(string broker, int port, string clientId, string username, string password)
        {
            MqttClient client = new MqttClient(broker, port, false, MqttSslProtocols.None, null, null);
            try
            {
                client.Connect(clientId, username, password);
            }
            catch (Exception e)
            {
                Properties.Settings.Default["lastConnectionSuccessful"] = false;
                SimHub.Logging.Current.Info($"Failed to connect to MQTT Broker {broker}. {e.InnerException.Message}");
                //throw;
            }

            IsConnected = client.IsConnected;
            Properties.Settings.Default["lastConnectionSuccessful"] = client.IsConnected;
            if (client.IsConnected)
            {
                SimHub.Logging.Current.Info($"Connected to MQTT Broker {broker}");
            }
            else
            {
                SimHub.Logging.Current.Info($"Failed to connect to MQTT Broker {broker}");
            }
            CLIENT = client;
        }

        public static bool IsConnected = false;
        public static bool TestConnection(string broker, int port, string username, string password)
        {
            if (CLIENT.IsConnected) CLIENT.Disconnect();
            string clientId = Guid.NewGuid().ToString();
            MqttClient client = new MqttClient(broker, port, false, MqttSslProtocols.None, null, null);
            client.Connect(clientId, username, password);
            IsConnected = client.IsConnected;
            Properties.Settings.Default["lastConnectionSuccessful"] = client.IsConnected;
            if (client.IsConnected)
            {
                SimHub.Logging.Current.Info($"Connection test to connect to MQTT Broker {broker} succeeded. Disconnecting...");
                client.Disconnect();
                return true;
            }
            else
            {
                SimHub.Logging.Current.Info($"Connection test to connect to MQTT Broker {broker} failed.");
                client.Disconnect();
                return false;
            }
        }

        public static void Subscribe(MqttClient client, string topic)
        {
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        }
        public static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string payload = System.Text.Encoding.Default.GetString(e.Message);
            string LogMessage = $"Received MQTT {payload} from topic {e.Topic.ToString()}";
            SimHub.Logging.Current.Info(LogMessage);


            MQTTPlugin plugin = new MQTTPlugin();
            //plugin.MqttMessage = payload;
            DateTime localDate = DateTime.Now;
            string timestamp = localDate.ToString(new CultureInfo("nl-NL"));
            plugin.MqttMessage = "{"
                + "\"topic\":\"" + e.Topic.ToString() + "\","
                + "\"payload\":\"" + payload + "\","
                + "\"timestamp\":\"" + timestamp
                + "\"}";
        }

        public static void ConnectAndSubscribe()
        {
            string broker = Properties.Settings.Default["mqttserver"].ToString();
            int port = int.Parse(Properties.Settings.Default["mqttport"].ToString());
            string topic = Properties.Settings.Default["mqtttopic"].ToString();
            string username = Properties.Settings.Default["mqttuser"].ToString();
            string password = Properties.Settings.Default["mqttpass"].ToString();
            string clientId = Guid.NewGuid().ToString();

            SimHub.Logging.Current.Info($"Connecting MQTT on {broker}.");
            if (CLIENT == null || !CLIENT.IsConnected)
            {
                ConnectMQTT(broker, port, clientId, username, password);
            }
            Subscribe(CLIENT, topic);
        }

    }
}
