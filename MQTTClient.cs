using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        // Backoff schedule in seconds; caps out and repeats at the last value.
        private static readonly int[] BackoffSchedule = { 2, 5, 10, 30, 60 };

        // How the reconnect task waits before it connects. Tests replace it so they don't sleep.
        internal static Action<int> SleepSeconds = seconds => Thread.Sleep(seconds * 1000);

        internal static int GetBackoffDelaySeconds(int reconnectAttempt)
        {
            int delayIndex = Math.Min(reconnectAttempt, BackoffSchedule.Length - 1);
            return BackoffSchedule[delayIndex];
        }

        public static void ConnectMQTT(string broker, int port, string clientId, string username, string password, bool useTls = false)
        {
            lock (ClientLock)
            {
                if (CLIENT != null && CLIENT.IsConnected)
                {
                    CLIENT.Disconnect();
                }

                MqttClient client;
                try
                {
                    // The constructor resolves the host name, so it fails when DNS does.
                    client = CreateClient(broker, port, useTls);
                }
                catch (Exception e)
                {
                    IsConnected = false;
                    MQTTSettings.Store["lastConnectionSuccessful"] = false;
                    SimHub.Logging.Current.Warn($"Failed to connect to MQTT Broker {broker}. {e.Message}");
                    return;
                }

                client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
                client.ConnectionClosed += client_ConnectionClosed;

                try
                {
                    client.Connect(clientId, username, password);
                }
                catch (Exception e)
                {
                    MQTTSettings.Store["lastConnectionSuccessful"] = false;
                    SimHub.Logging.Current.Warn($"Failed to connect to MQTT Broker {broker}. {e.InnerException?.Message ?? e.Message}");
                }

                IsConnected = client.IsConnected;
                MQTTSettings.Store["lastConnectionSuccessful"] = client.IsConnected;
                if (client.IsConnected)
                {
                    SimHub.Logging.Current.Info($"Connected to MQTT Broker {broker}");
                    reconnectAttempt = 0;
                }
                else
                {
                    SimHub.Logging.Current.Warn($"Failed to connect to MQTT Broker {broker}");
                }
                CLIENT = client;
            }

        }

        private static MqttClient CreateClient(string broker, int port, bool useTls)
        {
            return new MqttClient(broker,
                port,
                useTls,
                useTls ? MqttSslProtocols.TLSv1_2 : MqttSslProtocols.None,
                null,
                null);
        }

        private static void client_ConnectionClosed(object sender, EventArgs e)
        {
            IsConnected = false;
            SimHub.Logging.Current.Warn("MQTT connection closed unexpectedly.");
        }

        public static Task<bool> TestConnectionAsync(string broker, int port, string username, string password, bool useTls = false)
        {
            if (CLIENT != null && CLIENT.IsConnected) CLIENT.Disconnect();

            return Task.Run(() =>
            {
                string clientId = Guid.NewGuid().ToString();

                MqttClient client;
                try
                {
                    client = CreateClient(broker, port, useTls);
                    client.Connect(clientId, username, password);
                }
                catch (Exception e)
                {
                    SimHub.Logging.Current.Warn($"Connection test to connect to MQTT Broker {broker} failed. {e.InnerException?.Message ?? e.Message}");
                    MQTTSettings.Store["lastConnectionSuccessful"] = false;
                    return false;
                }

                IsConnected = client.IsConnected;
                MQTTSettings.Store["lastConnectionSuccessful"] = client.IsConnected;
                if (client.IsConnected)
                {
                    SimHub.Logging.Current.Info($"Connection test to connect to MQTT Broker {broker} succeeded. Disconnecting...");
                    client.Disconnect();
                    return true;
                }
                else
                {
                    SimHub.Logging.Current.Warn($"Connection test to connect to MQTT Broker {broker} failed.");
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
            string payload = Encoding.UTF8.GetString(e.Message);
            object obj = MessageFormatter.ParsePayload(payload, out bool isJson);
            string LogMessage = $"Received MQTT {payload} from topic {e.Topic}" + (isJson ? "" : " (not valid JSON; passed on as a string)");
            SimHub.Logging.Current.Debug(LogMessage);

            string sMessage = MessageFormatter.Format(e.Topic, obj, DateTimeOffset.Now);
            if (MQTTPlugin.Instance != null)
            {
                MQTTPlugin.Instance.MqttMessage = sMessage;
            }
            else
            {
                SimHub.Logging.Current.Debug("Received MQTT message but no active MQTTPlugin instance to update.");
            }
        }

        public static void Publish(string topic, string message)
        {
            lock (ClientLock)
            {
                if (CLIENT != null && CLIENT.IsConnected)
                {
                    SimHub.Logging.Current.Debug($"Publishing MQTT message '{message}' to topic {topic}");
                    CLIENT.Publish(topic, Encoding.UTF8.GetBytes(message));
                }
                else
                {
                    SimHub.Logging.Current.Warn($"MQTT is not connected; message to topic {topic} was not published.");
                }
            }
        }

        public static void ConnectAndSubscribe()
        {
            IDictionary<string, string> settings = MQTTSettings.LoadSettings();
            if (!SettingsRules.HasBrokerConfigured(settings))
            {
                SimHub.Logging.Current.Info("No MQTT broker configured; not connecting.");
                return;
            }
            string broker = settings["mqttserver"];
            if (!SettingsRules.TryParsePort(settings["mqttport"], out int port))
            {
                SimHub.Logging.Current.Warn($"Invalid or missing MQTT port '{settings["mqttport"]}'. Aborting connect.");
                return;
            }
            string topic = settings["mqtttopic"];
            string username = settings["mqttuser"];
            string password = Crypt.UnprotectString(settings["mqttpass"]);
            bool useTls = SettingsRules.IsTlsEnabled(settings);
            string clientId = Guid.NewGuid().ToString();

            SimHub.Logging.Current.Info($"Connecting MQTT on {broker}.");
            if (CLIENT == null || !CLIENT.IsConnected)
            {
                ConnectMQTT(broker, port, clientId, username, password, useTls);
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
            if (!SettingsRules.HasBrokerConfigured(MQTTSettings.LoadSettings()))
            {
                return;
            }

            int delaySeconds = GetBackoffDelaySeconds(reconnectAttempt);

            // Simple gate: only actually try every Nth tick matching the backoff delay,
            // since the timer itself ticks every 5s regardless of backoff stage.
            reconnecting = true;
            reconnectAttempt++;
            SimHub.Logging.Current.Info($"MQTT disconnected, attempting reconnect (attempt {reconnectAttempt}, backoff {delaySeconds}s)...");

            Task.Run(() =>
            {
                try
                {
                    SleepSeconds(delaySeconds);
                    ConnectAndSubscribe();
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Error($"MQTT reconnect failed: {ex.Message}");
                }
                finally
                {
                    reconnecting = false;
                }
            });
        }
    }
}
