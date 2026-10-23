using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class MqttConnectionManagerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private ManagerHarness harness;
        private MqttConnectionManager manager;
        private readonly object owner = new object();

        [TestInitialize]
        public void Initialize()
        {
            harness = new ManagerHarness();
            manager = harness.Manager;
        }

        [TestCleanup]
        public void Cleanup()
        {
            harness.Factory.ConnectHook = null;
            manager.Stop(owner);
        }

        private static void Wait(Task task)
        {
            Assert.IsTrue(task.Wait(Timeout), "the task didn't finish in time");
        }

        private static T Wait<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(Timeout), "the task didn't finish in time");
            return task.Result;
        }

        private void StartConnected()
        {
            Wait(manager.Start(owner));
            Assert.IsTrue(manager.IsConnected, "setup: the manager should be connected");
        }

        // --- Start and Stop ---

        [TestMethod]
        public void Start_ConnectsWithSavedSettingsAndSubscribes()
        {
            harness.SetSetting("mqttport", "8883");
            harness.SetSetting("mqtttls", "True");
            harness.SetSetting("mqttuser", "user");
            harness.SetSetting("mqttpass", Crypt.ProtectString("secret"));

            Wait(manager.Start(owner));

            FakeMqttClient client = harness.LastClient;
            Assert.AreEqual("broker.test", client.Broker);
            Assert.AreEqual(8883, client.Port);
            Assert.IsTrue(client.UseTls);
            Assert.AreEqual("user", client.Username);
            Assert.AreEqual("secret", client.Password);
            CollectionAssert.AreEqual(new[] { "home/#" }, client.Subscriptions.ToArray());
            Assert.IsTrue(manager.IsConnected);
            Assert.IsTrue(harness.Log.Contains("Info", "Connected to MQTT Broker broker.test"));
            Assert.IsTrue(harness.Log.Contains("Info", "Subscribed to MQTT topic home/#"));
        }

        [TestMethod]
        public void Start_StartsWatchdogTimerEveryFiveSeconds()
        {
            Wait(manager.Start(owner));

            Assert.IsNotNull(harness.Timer);
            Assert.AreEqual(TimeSpan.FromSeconds(5), harness.Timer.Interval);
            Assert.IsTrue(harness.Log.Contains("Info", "MQTT watchdog started."));
        }

        // D5: Start returns before the blocking Connect finishes.
        [TestMethod]
        public void Start_DoesNotWaitForTheConnection()
        {
            var release = new ManualResetEventSlim();
            harness.Factory.ConnectHook = c => release.Wait(Timeout);

            Task connect = manager.Start(owner);

            Assert.IsTrue(manager.IsRunning);
            Assert.IsFalse(connect.IsCompleted);
            release.Set();
            Wait(connect);
            Assert.IsTrue(manager.IsConnected);
        }

        [TestMethod]
        public void Start_NoBroker_LogsInfoAndDoesNotConnect()
        {
            harness.SetSetting("mqttserver", "");

            Wait(manager.Start(owner));

            Assert.AreEqual(0, harness.Factory.Created.Count);
            Assert.IsTrue(harness.Log.Contains("Info", "No MQTT broker configured; not connecting."));
            Assert.AreEqual(0, harness.Log.At("Warn").Count);
        }

        [TestMethod]
        public void Start_InvalidPort_WarnsAndDoesNotConnect()
        {
            harness.SetSetting("mqttport", "abc");

            Wait(manager.Start(owner));

            Assert.AreEqual(0, harness.Factory.Created.Count);
            Assert.IsTrue(harness.Log.Contains("Warn", "Invalid or missing MQTT port 'abc'. Aborting connect."));
        }

        [TestMethod]
        public void Start_NoTopic_ConnectsWithoutSubscribingAndWarns()
        {
            harness.SetSetting("mqtttopic", "");

            Wait(manager.Start(owner));

            Assert.IsTrue(manager.IsConnected);
            Assert.AreEqual(0, harness.LastClient.Subscriptions.Count);
            Assert.IsTrue(harness.Log.Contains("Warn", "No MQTT topic configured; not subscribing."));
        }

        // B1: a failed DNS lookup is a failed connect, not an exception.
        [TestMethod]
        public void Start_HostCannotBeResolved_WarnsAndStaysDisconnected()
        {
            harness.Factory.CreateException = new SocketException(11001);

            Wait(manager.Start(owner));

            Assert.IsFalse(manager.IsConnected);
            Assert.IsTrue(manager.IsRunning);
            Assert.IsTrue(harness.Log.Contains("Warn", "Failed to connect to MQTT Broker broker.test."));
        }

        [TestMethod]
        public void Start_ConnectThrows_WarnsWithInnerMessage()
        {
            harness.Factory.ConnectException = new Exception("outer", new Exception("refused by broker"));

            Wait(manager.Start(owner));

            Assert.IsFalse(manager.IsConnected);
            Assert.IsTrue(harness.Log.Contains("Warn", "Failed to connect to MQTT Broker broker.test. refused by broker"));
        }

        [TestMethod]
        public void Start_BrokerRefuses_WarnsAndStaysDisconnected()
        {
            harness.Factory.ConnectSucceeds = false;

            Wait(manager.Start(owner));

            Assert.IsFalse(manager.IsConnected);
            Assert.IsTrue(harness.Log.Contains("Warn", "Failed to connect to MQTT Broker broker.test"));
        }

        [TestMethod]
        public void Start_ConnectSucceeds_RaisesConnectionStateTrue()
        {
            bool? state = null;
            manager.ConnectionStateChanged += connected => state = connected;

            Wait(manager.Start(owner));

            Assert.AreEqual(true, state);
        }

        // SimHub may call Init on the new plugin before End on the old one.
        [TestMethod]
        public void Start_SecondOwner_TakesOverWithoutReconnecting()
        {
            var newOwner = new object();
            StartConnected();

            Wait(manager.Start(newOwner));
            manager.Stop(owner);

            Assert.AreEqual(1, harness.Factory.Created.Count);
            Assert.IsTrue(manager.IsConnected, "the old owner's Stop must not stop the new owner's connection");
            manager.Stop(newOwner);
            Assert.IsFalse(manager.IsRunning);
        }

        [TestMethod]
        public void Stop_DisconnectsStopsTimerWithoutUnexpectedCloseWarning()
        {
            StartConnected();
            FakeMqttClient client = harness.LastClient;

            manager.Stop(owner);

            Assert.IsFalse(manager.IsRunning);
            Assert.IsFalse(manager.IsConnected);
            Assert.AreEqual(1, client.DisconnectCount);
            Assert.IsTrue(harness.Timer.Disposed);
            Assert.IsFalse(harness.Log.Contains("Warn", "closed unexpectedly"));
        }

        [TestMethod]
        public void Stop_NotRunning_DoesNothing()
        {
            manager.Stop(owner);

            Assert.IsFalse(manager.IsRunning);
            Assert.AreEqual(0, harness.Log.Levels.Count);
        }

        [TestMethod]
        public void Start_AfterStop_ConnectsAgain()
        {
            StartConnected();
            manager.Stop(owner);

            Wait(manager.Start(owner));

            Assert.AreEqual(2, harness.Factory.Created.Count);
            Assert.IsTrue(manager.IsConnected);
        }

        // --- Watchdog ---

        [TestMethod]
        public void CheckConnection_NotRunning_DoesNothing()
        {
            Wait(manager.CheckConnection());

            Assert.AreEqual(0, harness.DelaySeconds.Count);
            Assert.AreEqual(0, harness.Factory.Created.Count);
        }

        [TestMethod]
        public void CheckConnection_Connected_DoesNothing()
        {
            StartConnected();

            Wait(manager.CheckConnection());

            Assert.AreEqual(0, harness.DelaySeconds.Count);
            Assert.AreEqual(1, harness.Factory.Created.Count);
        }

        [TestMethod]
        public void CheckConnection_NoBroker_DoesNothing()
        {
            harness.SetSetting("mqttserver", "");
            Wait(manager.Start(owner));

            Wait(manager.CheckConnection());

            Assert.AreEqual(0, harness.DelaySeconds.Count);
        }

        [TestMethod]
        public void CheckConnection_Disconnected_WaitsBackoffThenReconnects()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));
            harness.Factory.ConnectSucceeds = true;

            Wait(manager.CheckConnection());

            CollectionAssert.AreEqual(new[] { 2 }, harness.DelaySeconds.ToArray());
            Assert.IsTrue(manager.IsConnected);
            Assert.IsTrue(harness.Log.Contains("Info", "MQTT disconnected, attempting reconnect (attempt 1, backoff 2s)..."));
        }

        [TestMethod]
        public void TimerTick_RunsTheConnectionCheck()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));

            harness.Timer.Tick();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (harness.DelaySeconds.Count == 0 && stopwatch.Elapsed < Timeout) Thread.Sleep(10);
            CollectionAssert.AreEqual(new[] { 2 }, harness.DelaySeconds.ToArray());
        }

        [TestMethod]
        public void CheckConnection_RepeatedFailures_FollowBackoffSchedule()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));

            for (int i = 0; i < 7; i++)
            {
                Wait(manager.CheckConnection());
            }

            CollectionAssert.AreEqual(new[] { 2, 5, 10, 30, 60, 60, 60 }, harness.DelaySeconds.ToArray());
        }

        [TestMethod]
        public void CheckConnection_AfterSuccessfulReconnect_BackoffStartsOver()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));
            Wait(manager.CheckConnection());
            Wait(manager.CheckConnection());
            harness.Factory.ConnectSucceeds = true;
            Wait(manager.CheckConnection());
            harness.LastClient.Drop();

            Wait(manager.CheckConnection());

            CollectionAssert.AreEqual(new[] { 2, 5, 10, 2 }, harness.DelaySeconds.ToArray());
        }

        [TestMethod]
        public void CheckConnection_WhileReconnectPending_DoesNotStartAnother()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));
            var release = new TaskCompletionSource<bool>();
            harness.DelayBehaviour = (delay, token) => release.Task;

            Task first = manager.CheckConnection();
            Wait(manager.CheckConnection());
            release.SetResult(true);
            Wait(first);

            Assert.AreEqual(1, harness.DelaySeconds.Count);
        }

        [TestMethod]
        public void ConnectionDropped_WarnsRaisesStateFalseAndWatchdogReconnects()
        {
            StartConnected();
            bool? state = null;
            manager.ConnectionStateChanged += connected => state = connected;

            harness.LastClient.Drop();

            Assert.IsTrue(harness.Log.Contains("Warn", "MQTT connection closed unexpectedly."));
            Assert.AreEqual(false, state);
            Assert.IsFalse(manager.IsConnected);

            Wait(manager.CheckConnection());

            Assert.IsTrue(manager.IsConnected);
            Assert.AreEqual(2, harness.Factory.Created.Count);
        }

        // --- B3: Test and Save ---

        [TestMethod]
        public void TestAsync_UsesSeparateClientAndKeepsLiveConnection()
        {
            StartConnected();
            FakeMqttClient live = harness.LastClient;

            bool result = Wait(manager.TestAsync(new ConnectionSettings("other.test", 1884, "u", "p", "", false)));

            FakeMqttClient testClient = harness.LastClient;
            Assert.IsTrue(result);
            Assert.AreNotSame(live, testClient);
            Assert.AreEqual("other.test", testClient.Broker);
            Assert.AreEqual(1884, testClient.Port);
            Assert.AreEqual(1, testClient.DisconnectCount);
            Assert.AreEqual(0, live.DisconnectCount);
            Assert.IsTrue(manager.IsConnected);
        }

        [TestMethod]
        public void TestAsync_HostCannotBeResolved_ReturnsFalseAndWarns()
        {
            StartConnected();
            harness.Factory.CreateException = new SocketException(11001);

            bool result = Wait(manager.TestAsync(new ConnectionSettings("other.test", 1883, "", "", "", false)));

            Assert.IsFalse(result);
            Assert.IsTrue(harness.Log.Contains("Warn", "Connection test to connect to MQTT Broker other.test failed."));
            Assert.IsTrue(manager.IsConnected);
        }

        [TestMethod]
        public void TestAsync_BrokerRefuses_ReturnsFalse()
        {
            harness.Factory.ConnectSucceeds = false;

            bool result = Wait(manager.TestAsync(new ConnectionSettings("other.test", 1883, "", "", "", false)));

            Assert.IsFalse(result);
            Assert.IsTrue(harness.Log.Contains("Warn", "Connection test to connect to MQTT Broker other.test failed."));
        }

        [TestMethod]
        public void TestAsync_DoesNotNeedTheManagerToRun()
        {
            Assert.IsTrue(Wait(manager.TestAsync(new ConnectionSettings("other.test", 1883, "", "", "", false))));
        }

        // B3: Save applies new settings right away, even while connected, and only the new topic
        // stays subscribed.
        [TestMethod]
        public void ReconnectAsync_WhileConnected_ConnectsWithNewSettingsAndTopic()
        {
            StartConnected();
            FakeMqttClient oldClient = harness.LastClient;
            harness.SetSetting("mqttserver", "new.test");
            harness.SetSetting("mqtttopic", "new/#");

            Wait(manager.ReconnectAsync());

            FakeMqttClient newClient = harness.LastClient;
            Assert.AreNotSame(oldClient, newClient);
            Assert.AreEqual(1, oldClient.DisconnectCount);
            Assert.IsFalse(oldClient.IsConnected);
            Assert.AreEqual("new.test", newClient.Broker);
            CollectionAssert.AreEqual(new[] { "new/#" }, newClient.Subscriptions.ToArray());
            Assert.IsTrue(harness.Log.Contains("Info", "Subscribed to MQTT topic new/#"));
            Assert.IsTrue(manager.IsConnected);
            Assert.IsFalse(harness.Log.Contains("Warn", "closed unexpectedly"));
        }

        [TestMethod]
        public void ReconnectAsync_BrokerRemoved_DisconnectsAndStaysDisconnected()
        {
            StartConnected();
            FakeMqttClient oldClient = harness.LastClient;
            harness.SetSetting("mqttserver", "");

            Wait(manager.ReconnectAsync());

            Assert.AreEqual(1, oldClient.DisconnectCount);
            Assert.IsFalse(manager.IsConnected);
            Assert.AreEqual(1, harness.Factory.Created.Count);
        }

        [TestMethod]
        public void ReconnectAsync_NotRunning_WarnsAndDoesNothing()
        {
            Wait(manager.ReconnectAsync());

            Assert.AreEqual(0, harness.Factory.Created.Count);
            Assert.IsTrue(harness.Log.Contains("Warn", "MQTT is not running"));
        }

        // --- B6: one connect at a time, nothing after Stop ---

        [TestMethod]
        public void ReconnectAsync_DuringWatchdogConnect_ConnectsOneAtATime()
        {
            var release = new ManualResetEventSlim();
            harness.Factory.ConnectHook = c => { if (c.Index == 0) release.Wait(Timeout); };
            Task startConnect = manager.Start(owner);

            Task save = manager.ReconnectAsync();
            Thread.Sleep(100);
            Assert.AreEqual(1, harness.Factory.Created.Count, "Save must wait for the running connect");
            release.Set();
            Wait(startConnect);
            Wait(save);

            Assert.AreEqual(1, harness.Factory.MaxConnectsInProgress);
            Assert.AreEqual(2, harness.Factory.Created.Count);
            Assert.AreEqual(1, harness.Factory.Created[0].DisconnectCount);
            Assert.IsTrue(harness.Factory.Created[1].IsConnected);
            Assert.IsTrue(manager.IsConnected);
        }

        [TestMethod]
        public void Stop_DuringBlockingConnect_DiscardsTheNewConnection()
        {
            var release = new ManualResetEventSlim();
            harness.Factory.ConnectHook = c => release.Wait(Timeout);
            Task connect = manager.Start(owner);
            while (harness.Factory.Created.Count == 0) Thread.Sleep(10);

            manager.Stop(owner);
            release.Set();
            Wait(connect);

            Assert.IsFalse(manager.IsConnected);
            Assert.IsFalse(harness.LastClient.IsConnected);
            Assert.AreEqual(1, harness.LastClient.DisconnectCount);
        }

        // A game change: the old plugin stops while its connect is still running and the new
        // plugin starts. The late connect must not replace the new run's connection.
        [TestMethod]
        public void StopThenStart_DuringBlockingConnect_OnlyTheNewRunConnects()
        {
            var newOwner = new object();
            var release = new ManualResetEventSlim();
            harness.Factory.ConnectHook = c => { if (c.Index == 0) release.Wait(Timeout); };
            Task oldConnect = manager.Start(owner);
            while (harness.Factory.Created.Count == 0) Thread.Sleep(10);

            manager.Stop(owner);
            Task newConnect = manager.Start(newOwner);
            release.Set();
            Wait(oldConnect);
            Wait(newConnect);

            Assert.IsFalse(harness.Factory.Created[0].IsConnected);
            Assert.IsTrue(harness.Factory.Created[1].IsConnected);
            Assert.IsTrue(manager.IsConnected);
            manager.Stop(newOwner);
        }

        [TestMethod]
        public void Stop_DuringBackoffDelay_CancelsTheReconnect()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));
            harness.DelayBehaviour = (delay, token) => Task.Delay(System.Threading.Timeout.Infinite, token);
            Task reconnect = manager.CheckConnection();

            manager.Stop(owner);
            Wait(reconnect);

            Assert.AreEqual(1, harness.Factory.Created.Count);
            Assert.AreEqual(0, harness.Log.At("Error").Count);
        }

        [TestMethod]
        public void CheckConnection_ReconnectThrows_LogsErrorAndAllowsNextAttempt()
        {
            harness.Factory.ConnectSucceeds = false;
            Wait(manager.Start(owner));
            harness.DelayBehaviour = (delay, token) => { throw new InvalidOperationException("boom"); };

            Wait(manager.CheckConnection());
            Wait(manager.CheckConnection());

            Assert.AreEqual(2, harness.Log.At("Error").Count(m => m == "MQTT reconnect failed: boom"));
        }

        // --- Publish ---

        [TestMethod]
        public void Publish_Connected_SendsUtf8AndLogsDebug()
        {
            StartConnected();

            manager.Publish("home/light", "aan €");

            CollectionAssert.AreEqual(new[] { "home/light=aan €" }, harness.LastClient.Published.ToArray());
            Assert.IsTrue(harness.Log.Contains("Debug", "Publishing MQTT message 'aan €' to topic home/light"));
        }

        // B10: a message that can't be sent is reported instead of dropped silently.
        [TestMethod]
        public void Publish_NotConnected_WarnsThatMessageWasNotPublished()
        {
            manager.Publish("home/light", "on");

            Assert.IsTrue(harness.Log.Contains("Warn", "MQTT is not connected; message to topic home/light was not published."));
        }

        [TestMethod]
        public void Publish_ClientThrows_Warns()
        {
            StartConnected();
            harness.Factory.PublishException = new InvalidOperationException("socket closed");

            manager.Publish("home/light", "on");

            Assert.IsTrue(harness.Log.Contains("Warn", "Failed to publish MQTT message to topic home/light. socket closed"));
        }

        // --- Received messages ---

        [TestMethod]
        public void MessageReceived_RaisesFormattedJsonWithClockTime()
        {
            string received = null;
            manager.MessageReceived += message => received = message;
            StartConnected();

            harness.LastClient.Receive("home/sensor", "{\"a\":1.10}");

            Assert.AreEqual(
                "{\"topic\":\"home/sensor\",\"payload\":{\"a\":1.10},\"timestamp\":\"4-3-2026 05:06:07\",\"timestampIso\":\"2026-03-04T05:06:07.0000000+01:00\"}",
                received);
        }

        [TestMethod]
        public void MessageReceived_InvalidUtf8_IsDecodedWithReplacementCharacter()
        {
            string received = null;
            manager.MessageReceived += message => received = message;
            StartConnected();

            harness.LastClient.Receive("t", new byte[] { 0x61, 0xFF, 0x62 });

            StringAssert.Contains(received, "\"payload\":\"a�b\"");
        }

        // B10: received messages are logged at Debug only, including plain-text payloads.
        [TestMethod]
        [DataRow("{\"a\":1}", "", DisplayName = "JSON payload")]
        [DataRow("on", " (not valid JSON; passed on as a string)", DisplayName = "plain-text payload")]
        public void MessageReceived_LogsOnlyAtDebug(string payload, string suffix)
        {
            manager.MessageReceived += message => { };
            StartConnected();
            int before = harness.Log.Levels.Count;

            harness.LastClient.Receive("home/sensor", payload);

            CollectionAssert.AreEqual(new[] { "Debug" }, harness.Log.Levels.Skip(before).ToArray());
            Assert.IsTrue(harness.Log.Contains("Debug", "Received MQTT " + payload + " from topic home/sensor" + suffix));
        }

        [TestMethod]
        public void MessageReceived_NoSubscriber_LogsDebug()
        {
            StartConnected();

            harness.LastClient.Receive("t", "on");

            Assert.IsTrue(harness.Log.Contains("Debug", "no active MQTTPlugin instance"));
        }

        // --- Backoff ---

        [TestMethod]
        [DataRow(0, 2, DisplayName = "attempt 0")]
        [DataRow(1, 5, DisplayName = "attempt 1")]
        [DataRow(2, 10, DisplayName = "attempt 2")]
        [DataRow(3, 30, DisplayName = "attempt 3")]
        [DataRow(4, 60, DisplayName = "attempt 4")]
        [DataRow(5, 60, DisplayName = "attempt 5 repeats the last step")]
        [DataRow(1000, 60, DisplayName = "attempt 1000 repeats the last step")]
        public void GetBackoffDelaySeconds_FollowsSchedule(int attempt, int expectedSeconds)
        {
            Assert.AreEqual(expectedSeconds, MqttConnectionManager.GetBackoffDelaySeconds(attempt));
        }
    }
}
