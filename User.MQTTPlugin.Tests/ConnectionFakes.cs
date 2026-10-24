using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// An in-memory MQTT client. Connect succeeds unless the factory says otherwise.
    /// </summary>
    internal sealed class FakeMqttClient : IMqttClient
    {
        private readonly FakeMqttClientFactory factory;
        private readonly object sync = new object();
        private readonly List<string> subscriptions = new List<string>();
        private readonly List<string> published = new List<string>();
        private volatile bool connected;
        private int disconnectCount;

        public FakeMqttClient(FakeMqttClientFactory factory, int index, string broker, int port, bool useTls)
        {
            this.factory = factory;
            Index = index;
            Broker = broker;
            Port = port;
            UseTls = useTls;
        }

        public int Index { get; }
        public string Broker { get; }
        public int Port { get; }
        public bool UseTls { get; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public int DisconnectCount => disconnectCount;
        public bool IsConnected => connected;

        public IList<string> Subscriptions { get { lock (sync) return subscriptions.ToList(); } }

        /// <summary>Published messages as "topic=payload".</summary>
        public IList<string> Published { get { lock (sync) return published.ToList(); } }

        public event Action<string, byte[]> MessageReceived;
        public event Action ConnectionClosed;

        public void Connect(string clientId, string username, string password)
        {
            Username = username;
            Password = password;
            factory.OnConnect(this);
            connected = factory.ConnectSucceeds;
        }

        public void Subscribe(string topic)
        {
            lock (sync) subscriptions.Add(topic);
        }

        public void Publish(string topic, byte[] payload)
        {
            if (factory.PublishException != null) throw factory.PublishException;
            lock (sync) published.Add(topic + "=" + Encoding.UTF8.GetString(payload));
        }

        public void Disconnect()
        {
            Interlocked.Increment(ref disconnectCount);
            if (connected)
            {
                connected = false;
                ConnectionClosed?.Invoke();
            }
        }

        /// <summary>Simulates a message from the broker.</summary>
        public void Receive(string topic, byte[] payload) => MessageReceived?.Invoke(topic, payload);

        public void Receive(string topic, string payload) => Receive(topic, Encoding.UTF8.GetBytes(payload));

        /// <summary>Simulates the broker dropping the connection.</summary>
        public void Drop()
        {
            connected = false;
            ConnectionClosed?.Invoke();
        }
    }

    internal sealed class FakeMqttClientFactory : IMqttClientFactory
    {
        private readonly object sync = new object();
        private readonly List<FakeMqttClient> created = new List<FakeMqttClient>();
        private int connectsInProgress;
        private int maxConnectsInProgress;

        /// <summary>Thrown by Create, like M2Mqtt does when DNS fails.</summary>
        public Exception CreateException { get; set; }

        /// <summary>Thrown by Connect.</summary>
        public Exception ConnectException { get; set; }

        /// <summary>Whether Connect ends connected (false: the broker refused).</summary>
        public bool ConnectSucceeds { get; set; } = true;

        public Exception PublishException { get; set; }

        /// <summary>Runs inside Connect, with the client; use it to block a connect.</summary>
        public Action<FakeMqttClient> ConnectHook { get; set; }

        public IList<FakeMqttClient> Created { get { lock (sync) return created.ToList(); } }

        public int MaxConnectsInProgress => maxConnectsInProgress;

        public IMqttClient Create(string broker, int port, bool useTls)
        {
            if (CreateException != null) throw CreateException;
            lock (sync)
            {
                var client = new FakeMqttClient(this, created.Count, broker, port, useTls);
                created.Add(client);
                return client;
            }
        }

        internal void OnConnect(FakeMqttClient client)
        {
            int inProgress = Interlocked.Increment(ref connectsInProgress);
            int max;
            while (inProgress > (max = maxConnectsInProgress))
            {
                Interlocked.CompareExchange(ref maxConnectsInProgress, inProgress, max);
            }
            try
            {
                ConnectHook?.Invoke(client);
                if (ConnectException != null) throw ConnectException;
            }
            finally
            {
                Interlocked.Decrement(ref connectsInProgress);
            }
        }
    }

    internal sealed class FakePluginLog : IPluginLog
    {
        private readonly object sync = new object();
        private readonly List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();

        public void Debug(string message) => Add("Debug", message);
        public void Info(string message) => Add("Info", message);
        public void Warn(string message) => Add("Warn", message);
        public void Error(string message) => Add("Error", message);

        public IList<string> At(string level)
        {
            lock (sync) return entries.Where(e => e.Key == level).Select(e => e.Value).ToList();
        }

        public IList<string> Levels
        {
            get { lock (sync) return entries.Select(e => e.Key).ToList(); }
        }

        public bool Contains(string level, string text) => At(level).Any(m => m.Contains(text));

        private void Add(string level, string message)
        {
            lock (sync) entries.Add(new KeyValuePair<string, string>(level, message));
        }
    }

    internal sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(1));
    }

    internal sealed class FakeTimer : IDisposable
    {
        public TimeSpan Interval { get; set; }
        public Action Tick { get; set; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// A MqttConnectionManager wired to fakes, with editable saved settings.
    /// </summary>
    internal sealed class ManagerHarness
    {
        private readonly object sync = new object();
        private readonly List<TimeSpan> delays = new List<TimeSpan>();

        private MQTTPluginSettings settings = new MQTTPluginSettings
        {
            SettingsVersion = MQTTPluginSettings.CurrentVersion,
            Server = "broker.test",
            Port = 1883,
            Topic = "home/#",
        };

        public ManagerHarness()
        {
            Manager = new MqttConnectionManager(
                Factory,
                Log,
                Clock,
                () => { lock (sync) return settings; },
                (delay, token) => Delay(delay, token),
                (interval, tick) =>
                {
                    Timer = new FakeTimer { Interval = interval, Tick = tick };
                    return Timer;
                });
        }

        public MqttConnectionManager Manager { get; }
        public FakeMqttClientFactory Factory { get; } = new FakeMqttClientFactory();
        public FakePluginLog Log { get; } = new FakePluginLog();
        public FakeClock Clock { get; } = new FakeClock();
        public FakeTimer Timer { get; private set; }

        /// <summary>The backoff wait; by default it records the delay and returns at once.</summary>
        public Func<TimeSpan, CancellationToken, Task> DelayBehaviour { get; set; } = (delay, token) => Task.CompletedTask;

        public IList<int> DelaySeconds
        {
            get { lock (sync) return delays.Select(d => (int)d.TotalSeconds).ToList(); }
        }

        /// <summary>Changes the saved settings the way MQTTSettings does: on a copy that replaces them.</summary>
        public void EditSettings(Action<MQTTPluginSettings> change)
        {
            lock (sync)
            {
                MQTTPluginSettings next = settings.Clone();
                change(next);
                settings = next;
            }
        }

        public FakeMqttClient LastClient => Factory.Created.Last();

        private Task Delay(TimeSpan delay, CancellationToken token)
        {
            lock (sync) delays.Add(delay);
            return DelayBehaviour(delay, token);
        }
    }
}
