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
        public static bool IsConnected = false;

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

        public static bool TestConnection(string broker, int port, string username, string password)
        {
            if (CLIENT != null && CLIENT.IsConnected) CLIENT.Disconnect();
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

        private static void Subscribe(MqttClient client, string topic)
        {
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        }
        private static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string payload = Encoding.Default.GetString(e.Message);
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
        public static void Publish(string topic, string message)
        {
            if (CLIENT != null && CLIENT.IsConnected)
            {
                SimHub.Logging.Current.Info($"Publishing MQTT message '{message}' to topic {topic}");
                CLIENT.Publish(topic, Encoding.UTF8.GetBytes(message));
            }
        }
        public static void ConnectAndSubscribe()
        {
            IDictionary<string, string> settings = MQTTSettings.LoadSettings();
            string broker = settings["mqttserver"];
            int port = int.Parse(settings["mqttport"]);
            string topic = settings["mqtttopic"];
            string username = settings["mqttuser"];
            string password = settings["mqttpass"];
            string clientId = Guid.NewGuid().ToString();

            Dictionary<string, string> MqttSettingsFound = settings
                .Where(kvp => kvp.Key.IndexOf("mqtt") > -1 && kvp.Value != "")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (MqttSettingsFound.Count == 0)
            {
                SimHub.Logging.Current.Info($"Found {MqttSettingsFound.Count} MQTT settings!");
                return;
            }

            SimHub.Logging.Current.Info($"Connecting MQTT on {broker}.");
            if (CLIENT == null || !CLIENT.IsConnected)
            {
                ConnectMQTT(broker, port, clientId, username, password);
            }
            Subscribe(CLIENT, topic);
        }

    }
}
