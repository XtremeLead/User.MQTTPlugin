using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Globalization;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;

namespace User.MQTTPlugin
{
    // https://docs.emqx.com/en/cloud/latest/connect_to_deployments/c_sharp_sdk.html
    public class MQTTClient
    {

        public static MqttClient CLIENT = null;
        public static bool IsConnected = false;

        internal static readonly object ClientLock = new object();
        private static System.Timers.Timer watchdogTimer;
        private static int reconnectAttempt = 0;
        private static bool reconnecting = false;
        private static string lastTopic = null;

        // Backoff schedule in seconds; caps out and repeats at the last value.
        private static readonly int[] BackoffSchedule = { 2, 5, 10, 30, 60 };

        public static void ConnectMQTT(string broker, int port, string clientId, string username, string password)
        {
            lock (ClientLock)
            {
                if (CLIENT != null && CLIENT.IsConnected)
                {
                    CLIENT.Disconnect();
                }

                MqttClient client = new MqttClient(broker, port, false, MqttSslProtocols.None, null, null);
                client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
                client.ConnectionClosed += client_ConnectionClosed;

                try
                {
                    client.Connect(clientId, username, password);
                }
                catch (Exception e)
                {
                    Properties.Settings.Default["lastConnectionSuccessful"] = false;
                    SimHub.Logging.Current.Info($"Failed to connect to MQTT Broker {broker}. {e.InnerException?.Message ?? e.Message}");
                }

                IsConnected = client.IsConnected;
                Properties.Settings.Default["lastConnectionSuccessful"] = client.IsConnected;
                if (client.IsConnected)
                {
                    SimHub.Logging.Current.Info($"Connected to MQTT Broker {broker}");
                    reconnectAttempt = 0;
                }
                else
                {
                    SimHub.Logging.Current.Info($"Failed to connect to MQTT Broker {broker}");
                }
                CLIENT = client;
            }

        }

        private static void client_ConnectionClosed(object sender, EventArgs e)
        {
            IsConnected = false;
            SimHub.Logging.Current.Info("MQTT connection closed unexpectedly.");
        }

        public static Task<bool> TestConnectionAsync(string broker, int port, string username, string password)
        {
            if (CLIENT != null && CLIENT.IsConnected) CLIENT.Disconnect();
            //string clientId = Guid.NewGuid().ToString();
            //MqttClient client = new MqttClient(broker, port, false, MqttSslProtocols.None, null, null);
            //client.Connect(clientId, username, password);
            //IsConnected = client.IsConnected;
            //Properties.Settings.Default["lastConnectionSuccessful"] = client.IsConnected;
            //if (client.IsConnected)
            //{
            //    SimHub.Logging.Current.Info($"Connection test to connect to MQTT Broker {broker} succeeded. Disconnecting...");
            //    client.Disconnect();
            //    return true;
            //}
            //else
            //{
            //    SimHub.Logging.Current.Info($"Connection test to connect to MQTT Broker {broker} failed.");
            //    client.Disconnect();
            //    return false;
            //}
            return Task.Run(() =>
            {
                string clientId = Guid.NewGuid().ToString();

                MqttClient client = new MqttClient(broker, port, false, MqttSslProtocols.None, null, null);

                try
                {
                    client.Connect(clientId, username, password);
                }
                catch (Exception e)
                {
                    SimHub.Logging.Current.Info($"Connection test to connect to MQTT Broker {broker} failed. {e.InnerException?.Message ?? e.Message}");
                    Properties.Settings.Default["lastConnectionSuccessful"] = false;
                    return false;
                }

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
            });

        }

        private static void Subscribe(MqttClient client, string topic)
        {
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        }

        private static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string payload = Encoding.Default.GetString(e.Message);
            object obj;
            try
            {
                obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(payload);
            }
            catch (Exception)
            {
                obj = payload;
            }
            string LogMessage = $"Received MQTT {payload} from topic {e.Topic}";
            SimHub.Logging.Current.Info(LogMessage);


            //MQTTPlugin plugin = new MQTTPlugin();
            //plugin.MqttMessage = payload;
            DateTime localDate = DateTime.Now;
            string timestamp = localDate.ToString(new CultureInfo("nl-NL"));

            Dictionary<string, dynamic> dMessage = new Dictionary<string, dynamic>();
            dMessage.Add("topic", e.Topic.ToString());
            dMessage.Add("payload", obj);
            dMessage.Add("timestamp", timestamp);
            string sMessage = JsonConvert.SerializeObject(dMessage);
            //plugin.MqttMessage = sMessage;
            if (MQTTPlugin.Instance != null)
            {
                MQTTPlugin.Instance.MqttMessage = sMessage;
            }
            else
            {
                SimHub.Logging.Current.Info("Received MQTT message but no active MQTTPlugin instance to update.");
            }
        }

        public static void Publish(string topic, string message)
        {
            lock (ClientLock)
            {
                if (CLIENT != null && CLIENT.IsConnected)
                {
                    SimHub.Logging.Current.Info($"Publishing MQTT message '{message}' to topic {topic}");
                    CLIENT.Publish(topic, Encoding.UTF8.GetBytes(message));
                }
            }
        }

        public static void ConnectAndSubscribe()
        {
            IDictionary<string, string> settings = MQTTSettings.LoadSettings();
            string broker = settings["mqttserver"];
            int port = int.Parse(settings["mqttport"]);
            string topic = settings["mqtttopic"];
            string username = settings["mqttuser"];
            string password = "";
            try
            {
                password = Crypt.UnprotectString(settings["mqttpass"]);
            }
            catch (Exception)
            {

            }
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
            if (CLIENT != null && CLIENT.IsConnected)
            {
                Subscribe(CLIENT, topic);
            }


        }
        /// <summary>
        /// Runs ConnectAndSubscribe() on a background thread so callers
        /// (e.g. UI event handlers) never block on the underlying blocking
        /// MqttClient.Connect() call.
        /// </summary>
        public static Task ConnectAndSubscribeAsync()
        {
            return Task.Run(() => ConnectAndSubscribe());
        }
        /// <summary>
        /// Starts a background timer that periodically checks the MQTT connection
        /// and reconnects with exponential-ish backoff if it has dropped.
        /// Safe to call multiple times; only starts one timer.
        /// </summary>
        public static void StartWatchdog()
        {
            if (watchdogTimer != null) return;

            watchdogTimer = new System.Timers.Timer(5000); // check every 5s
            watchdogTimer.Elapsed += WatchdogTick;
            watchdogTimer.AutoReset = true;
            watchdogTimer.Start();
            SimHub.Logging.Current.Info("MQTT watchdog started.");
        }

        public static void StopWatchdog()
        {
            if (watchdogTimer == null) return;
            watchdogTimer.Stop();
            watchdogTimer.Elapsed -= WatchdogTick;
            watchdogTimer.Dispose();
            watchdogTimer = null;
        }

        private static void WatchdogTick(object sender, ElapsedEventArgs e)
        {
            bool currentlyConnected;
            try
            {
                currentlyConnected = CLIENT != null && CLIENT.IsConnected;
            }
            catch (ObjectDisposedException)
            {
                currentlyConnected = false;
            }

            if (currentlyConnected || reconnecting)
            {
                return;
            }

            // Only attempt reconnect if we have settings worth reconnecting with.
            IDictionary<string, string> settings = MQTTSettings.LoadSettings();
            bool hasSettings = settings.TryGetValue("mqttserver", out string server) && !string.IsNullOrEmpty(server);
            if (!hasSettings)
            {
                return;
            }

            int delayIndex = Math.Min(reconnectAttempt, BackoffSchedule.Length - 1);
            int delaySeconds = BackoffSchedule[delayIndex];

            // Simple gate: only actually try every Nth tick matching the backoff delay,
            // since the timer itself ticks every 5s regardless of backoff stage.
            reconnecting = true;
            reconnectAttempt++;
            SimHub.Logging.Current.Info($"MQTT disconnected, attempting reconnect (attempt {reconnectAttempt}, backoff {delaySeconds}s)...");

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(delaySeconds * 1000);
                    ConnectAndSubscribe();
                }
                finally
                {
                    reconnecting = false;
                }
            });
        }
    }
}
