using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using System.IO;
using uPLibrary.Networking.M2Mqtt.Messages;
using User.MQTTPlugin;

namespace User.MQTTPlugin
{
    // https://docs.emqx.com/en/cloud/latest/connect_to_deployments/c_sharp_sdk.html
    public class MQTTClient
    {

        public static MqttClient CLIENT = null;

        public static void ConnectMQTT(string broker, int port, string clientId, string username, string password)
        {
            MqttClient client = new MqttClient(broker, port, false, MqttSslProtocols.None, null, null);
            client.Connect(clientId, username, password);
            IsConnected = client.IsConnected;
            Properties.Settings.Default["lastConnectionSuccessful"] = client.IsConnected;
            if (client.IsConnected)
            {
                WriteLog("Connected to MQTT Broker");
            }
            else
            {
                WriteLog("Failed to connect");
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
                client.Disconnect();
                return true;
            }
            else
            {
                client.Disconnect();
                return false;
            }
        }

        private static void WriteLog(string value)
        {
            // Set a variable to the Documents path.
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Append text to an existing file named "WriteLines.txt".
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "WriteLines.txt"), true))
            {
                outputFile.WriteLine(value.ToString());
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
            string LogMessage = "Received " + payload + " from topic " + e.Topic.ToString();
            WriteLog(LogMessage);

            MQTTPlugin plugin = new MQTTPlugin();
            plugin.MqttMessage = payload;
        }

        public static void ConnectAndSubscribe()
        {
            string broker = Properties.Settings.Default["mqttserver"].ToString();
            int port = int.Parse(Properties.Settings.Default["mqttport"].ToString());
            string topic = Properties.Settings.Default["mqtttopic"].ToString();
            string clientId = Guid.NewGuid().ToString();
            string username = Properties.Settings.Default["mqttuser"].ToString();
            string password = Properties.Settings.Default["mqttpass"].ToString();

            WriteLog("Connecting MQTT");
            if (CLIENT == null || !CLIENT.IsConnected)
            {
                ConnectMQTT(broker, port, clientId, username, password);
            }
            Subscribe(CLIENT, topic);
        }

    }
}
