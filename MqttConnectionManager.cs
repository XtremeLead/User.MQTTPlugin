using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Owns the live MQTT connection: connecting, the reconnect watchdog, subscribing, publishing
    /// and turning received messages into MQTTMessage JSON.
    ///
    /// There is one shared instance (D4). MQTTPlugin.Init starts it and End stops it; because
    /// SimHub rebuilds plugins on every game change, Start and Stop take the plugin instance as
    /// owner, and only the owner that started it last can stop it.
    ///
    /// Thread safety: the client field and the running state are guarded by <c>sync</c>. Only
    /// one connect runs at a time (<c>connectGate</c>), and the blocking Connect call runs
    /// outside <c>sync</c>, so Publish never waits for it. A connect that finishes after
    /// Stop() is discarded.
    /// </summary>
    internal sealed class MqttConnectionManager
    {
        internal static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);

        // Backoff schedule in seconds; caps out and repeats at the last value.
        private static readonly int[] BackoffSchedule = { 2, 5, 10, 30, 60 };

        internal static MqttConnectionManager Shared { get; } = new MqttConnectionManager(
            new M2MqttClientFactory(),
            new SimHubLog(),
            new SystemClock(),
            () => MQTTSettings.Current,
            Task.Delay,
            StartSystemTimer);

        private readonly IMqttClientFactory factory;
        private readonly IPluginLog log;
        private readonly IClock clock;
        private readonly Func<MQTTPluginSettings> loadSettings;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private readonly Func<TimeSpan, Action, IDisposable> startTimer;

        private readonly object sync = new object();
        private readonly SemaphoreSlim connectGate = new SemaphoreSlim(1, 1);
        private IMqttClient client;
        private object owner;
        private IDisposable watchdogTimer;
        private CancellationTokenSource stopSource;
        private int reconnectAttempt;
        private int reconnectPending;

        internal MqttConnectionManager(
            IMqttClientFactory factory,
            IPluginLog log,
            IClock clock,
            Func<MQTTPluginSettings> loadSettings,
            Func<TimeSpan, CancellationToken, Task> delay,
            Func<TimeSpan, Action, IDisposable> startTimer)
        {
            this.factory = factory;
            this.log = log;
            this.clock = clock;
            this.loadSettings = loadSettings;
            this.delay = delay;
            this.startTimer = startTimer;
        }

        /// <summary>Raised on the receive thread with the MQTTMessage JSON.</summary>
        public event Action<string> MessageReceived;

        /// <summary>Raised with true after connecting, and with false when the live connection drops.</summary>
        public event Action<bool> ConnectionStateChanged;

        public bool IsConnected
        {
            get
            {
                lock (sync)
                {
                    return client != null && client.IsConnected;
                }
            }
        }

        internal bool IsRunning
        {
            get
            {
                lock (sync)
                {
                    return owner != null;
                }
            }
        }

        internal static int GetBackoffDelaySeconds(int reconnectAttempt)
        {
            int delayIndex = Math.Min(reconnectAttempt, BackoffSchedule.Length - 1);
            return BackoffSchedule[delayIndex];
        }

        /// <summary>
        /// Starts the watchdog and connects in the background (D5). If it is already running,
        /// the new owner takes over without reconnecting. Returns the background connect.
        /// </summary>
        public Task Start(object newOwner)
        {
            CancellationToken token;
            lock (sync)
            {
                bool wasRunning = owner != null;
                owner = newOwner;
                if (wasRunning)
                {
                    return Task.CompletedTask;
                }
                stopSource = new CancellationTokenSource();
                token = stopSource.Token;
                watchdogTimer = startTimer(WatchdogInterval, () => CheckConnection());
            }
            log.Info("MQTT watchdog started.");
            return Task.Run(() => ConnectAsync(false, token));
        }

        /// <summary>
        /// Stops the watchdog, cancels a pending reconnect and disconnects. Ignored when another
        /// owner has started the manager since.
        /// </summary>
        public void Stop(object stoppingOwner)
        {
            IMqttClient oldClient;
            CancellationTokenSource oldStopSource;
            lock (sync)
            {
                if (owner == null || owner != stoppingOwner)
                {
                    return;
                }
                owner = null;
                watchdogTimer.Dispose();
                watchdogTimer = null;
                oldStopSource = stopSource;
                stopSource = null;
                oldClient = client;
                client = null;
            }
            oldStopSource.Cancel();
            DisconnectQuietly(oldClient, "during shutdown");
            log.Info("MQTT stopped.");
        }

        /// <summary>
        /// Drops the current connection and connects again with the saved settings, so new
        /// settings (including a new topic) take effect right away. Used by Save.
        /// </summary>
        public Task ReconnectAsync()
        {
            CancellationToken token;
            lock (sync)
            {
                if (owner == null)
                {
                    log.Warn("MQTT is not running; settings will be used when the plugin starts.");
                    return Task.CompletedTask;
                }
                token = stopSource.Token;
            }
            return Task.Run(() => ConnectAsync(true, token));
        }

        /// <summary>
        /// Connects with a separate client to check the settings, then disconnects it. Never
        /// touches the live connection.
        /// </summary>
        public Task<bool> TestAsync(ConnectionSettings settings)
        {
            return Task.Run(() =>
            {
                IMqttClient testClient;
                try
                {
                    testClient = factory.Create(settings.Broker, settings.Port, settings.UseTls);
                    testClient.Connect(Guid.NewGuid().ToString(), settings.Username, settings.Password);
                }
                catch (Exception e)
                {
                    log.Warn($"Connection test to connect to MQTT Broker {settings.Broker} failed. {e.InnerException?.Message ?? e.Message}");
                    return false;
                }

                bool connected = testClient.IsConnected;
                if (connected)
                {
                    log.Info($"Connection test to connect to MQTT Broker {settings.Broker} succeeded. Disconnecting...");
                }
                else
                {
                    log.Warn($"Connection test to connect to MQTT Broker {settings.Broker} failed.");
                }
                DisconnectQuietly(testClient, "after the connection test");
                return connected;
            });
        }

        public void Publish(string topic, string message)
        {
            IMqttClient current;
            lock (sync)
            {
                current = client;
            }
            if (current == null || !current.IsConnected)
            {
                log.Warn($"MQTT is not connected; message to topic {topic} was not published.");
                return;
            }
            log.Debug($"Publishing MQTT message '{message}' to topic {topic}");
            try
            {
                current.Publish(topic, Encoding.UTF8.GetBytes(message));
            }
            catch (Exception e)
            {
                log.Warn($"Failed to publish MQTT message to topic {topic}. {e.Message}");
            }
        }

        /// <summary>
        /// One watchdog tick: when running and disconnected, waits the backoff delay and
        /// reconnects. A tick while a reconnect is pending does nothing. Returns the reconnect,
        /// or a completed task when there is nothing to do.
        /// </summary>
        internal Task CheckConnection()
        {
            CancellationToken token;
            lock (sync)
            {
                if (owner == null || (client != null && client.IsConnected))
                {
                    return Task.CompletedTask;
                }
                token = stopSource.Token;
            }
            if (Interlocked.CompareExchange(ref reconnectPending, 1, 0) != 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                if (!SettingsRules.HasBrokerConfigured(loadSettings()))
                {
                    Interlocked.Exchange(ref reconnectPending, 0);
                    return Task.CompletedTask;
                }
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref reconnectPending, 0);
                throw;
            }

            int attempt = Interlocked.Increment(ref reconnectAttempt);
            int delaySeconds = GetBackoffDelaySeconds(attempt - 1);
            log.Info($"MQTT disconnected, attempting reconnect (attempt {attempt}, backoff {delaySeconds}s)...");

            return Task.Run(async () =>
            {
                try
                {
                    await delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
                    await ConnectAsync(false, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    log.Error($"MQTT reconnect failed: {e.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref reconnectPending, 0);
                }
            });
        }

        /// <summary>
        /// Connects with the saved settings and subscribes. Without <paramref name="force"/> it
        /// does nothing while connected; with it, the current connection is dropped first.
        /// </summary>
        private async Task ConnectAsync(bool force, CancellationToken token)
        {
            try
            {
                await connectGate.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                IMqttClient oldClient = null;
                lock (sync)
                {
                    if (!IsCurrentRun(token))
                    {
                        return;
                    }
                    if (client != null && client.IsConnected && !force)
                    {
                        return;
                    }
                    if (force)
                    {
                        oldClient = client;
                        client = null;
                    }
                }
                DisconnectQuietly(oldClient, "to apply new settings");

                MQTTPluginSettings savedSettings = loadSettings();
                ConnectionSettings settings = ConnectionSettings.FromSettings(savedSettings, out ConnectionSettingsProblem problem);
                if (problem == ConnectionSettingsProblem.NoBroker)
                {
                    log.Info("No MQTT broker configured; not connecting.");
                    return;
                }
                if (problem == ConnectionSettingsProblem.InvalidPort)
                {
                    log.Warn($"Invalid MQTT port {savedSettings.Port}. Aborting connect.");
                    return;
                }

                IMqttClient newClient = Connect(settings);
                if (newClient == null)
                {
                    return;
                }

                bool discarded;
                lock (sync)
                {
                    discarded = !IsCurrentRun(token);
                    if (!discarded)
                    {
                        client = newClient;
                    }
                }
                if (discarded)
                {
                    log.Debug("MQTT stopped while connecting; dropping the new connection.");
                    DisconnectQuietly(newClient, "after stopping");
                    return;
                }

                Interlocked.Exchange(ref reconnectAttempt, 0);
                ConnectionStateChanged?.Invoke(true);
                Subscribe(newClient, settings.Topic);
            }
            finally
            {
                connectGate.Release();
            }
        }

        /// <summary>
        /// Whether <paramref name="token"/> belongs to the run that is still active. Call inside
        /// <c>sync</c>: Stop() clears <c>stopSource</c> there before it cancels the token.
        /// </summary>
        private bool IsCurrentRun(CancellationToken token)
        {
            return stopSource != null && stopSource.Token == token && !token.IsCancellationRequested;
        }

        /// <summary>Returns the connected client, or null after logging why it failed.</summary>
        private IMqttClient Connect(ConnectionSettings settings)
        {
            log.Info($"Connecting MQTT on {settings.Broker}.");
            IMqttClient newClient;
            try
            {
                newClient = factory.Create(settings.Broker, settings.Port, settings.UseTls);
            }
            catch (Exception e)
            {
                log.Warn($"Failed to connect to MQTT Broker {settings.Broker}. {e.Message}");
                return null;
            }

            newClient.MessageReceived += OnMessageReceived;
            newClient.ConnectionClosed += () => OnConnectionClosed(newClient);
            try
            {
                newClient.Connect(Guid.NewGuid().ToString(), settings.Username, settings.Password);
            }
            catch (Exception e)
            {
                log.Warn($"Failed to connect to MQTT Broker {settings.Broker}. {e.InnerException?.Message ?? e.Message}");
                return null;
            }
            if (!newClient.IsConnected)
            {
                log.Warn($"Failed to connect to MQTT Broker {settings.Broker}");
                return null;
            }
            log.Info($"Connected to MQTT Broker {settings.Broker}");
            return newClient;
        }

        private void Subscribe(IMqttClient subscriber, string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                log.Warn("No MQTT topic configured; not subscribing.");
                return;
            }
            try
            {
                subscriber.Subscribe(topic);
                log.Info($"Subscribed to MQTT topic {topic}");
            }
            catch (Exception e)
            {
                log.Warn($"Failed to subscribe to MQTT topic {topic}. {e.Message}");
            }
        }

        private void OnMessageReceived(string topic, byte[] payloadBytes)
        {
            string payload = Encoding.UTF8.GetString(payloadBytes);
            object parsed = MessageFormatter.ParsePayload(payload, out bool isJson);
            log.Debug($"Received MQTT {payload} from topic {topic}" + (isJson ? "" : " (not valid JSON; passed on as a string)"));

            string message = MessageFormatter.Format(topic, parsed, clock.Now);
            Action<string> handler = MessageReceived;
            if (handler == null)
            {
                log.Debug("Received MQTT message but no active MQTTPlugin instance to update.");
                return;
            }
            handler(message);
        }

        private void OnConnectionClosed(IMqttClient closedClient)
        {
            bool wasLive;
            lock (sync)
            {
                // A client we dropped on purpose is no longer the live one.
                wasLive = client == closedClient;
            }
            if (wasLive)
            {
                log.Warn("MQTT connection closed unexpectedly.");
                ConnectionStateChanged?.Invoke(false);
            }
        }

        private void DisconnectQuietly(IMqttClient oldClient, string when)
        {
            if (oldClient == null) return;
            try
            {
                oldClient.Disconnect();
            }
            catch (Exception e)
            {
                log.Warn($"Error while disconnecting MQTT client {when}: {e.Message}");
            }
        }

        private static IDisposable StartSystemTimer(TimeSpan interval, Action tick)
        {
            System.Timers.Timer timer = new System.Timers.Timer(interval.TotalMilliseconds) { AutoReset = true };
            timer.Elapsed += (sender, e) => tick();
            timer.Start();
            return timer;
        }

        /// <summary>The default for MQTTSettings.Reconnect: reconnect the shared manager.</summary>
        internal static Task ReconnectSharedAsync()
        {
            return Shared.ReconnectAsync();
        }
    }
}
