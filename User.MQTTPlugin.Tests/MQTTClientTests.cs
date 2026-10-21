using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using uPLibrary.Networking.M2Mqtt;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Covers the MQTTClient paths that need no broker. Tests that use the reserved
    /// "broker.invalid" host do a DNS lookup that is guaranteed to fail (RFC 6761); they never
    /// open a connection.
    /// </summary>
    [TestClass]
    public class MQTTClientTests
    {
        private const string UnresolvableBroker = "broker.invalid";

        [TestInitialize]
        public void Initialize() => TestState.Reset();

        [TestCleanup]
        public void Cleanup() => TestState.Reset();

        private static int ReconnectAttempt => (int)TestState.GetStatic(typeof(MQTTClient), "reconnectAttempt");
        private static bool Reconnecting => (bool)TestState.GetStatic(typeof(MQTTClient), "reconnecting");
        private static object WatchdogTimer => TestState.GetStatic(typeof(MQTTClient), "watchdogTimer");

        private static void WatchdogTick() => TestState.InvokeStatic(typeof(MQTTClient), "WatchdogTick", null, null);

        // --- ConnectAndSubscribe ---

        [TestMethod]
        public void ConnectAndSubscribe_DefaultSettings_StopsAtPortCheck()
        {
            MQTTClient.ConnectAndSubscribe();

            Assert.IsNull(MQTTClient.CLIENT);
        }

        [TestMethod]
        public void ConnectAndSubscribe_NonNumericPort_DoesNotConnect()
        {
            Properties.Settings.Default["mqttserver"] = UnresolvableBroker;
            Properties.Settings.Default["mqttport"] = "abc";

            MQTTClient.ConnectAndSubscribe();

            Assert.IsNull(MQTTClient.CLIENT);
        }

        // Current behaviour (B1): the MqttClient constructor resolves the host outside the try,
        // so the exception escapes ConnectAndSubscribe (and Init, before StartWatchdog).
        // The empty default password doesn't stop it: that failure is caught and logged.
        [TestMethod]
        public void ConnectAndSubscribe_UnresolvableBroker_ThrowsSocketException()
        {
            Properties.Settings.Default["mqttserver"] = UnresolvableBroker;
            Properties.Settings.Default["mqttport"] = "1883";

            Assert.ThrowsException<SocketException>(() => MQTTClient.ConnectAndSubscribe());
            Assert.IsNull(MQTTClient.CLIENT);
        }

        // Current behaviour (B1): the Test button gets a faulted task for the same reason.
        [TestMethod]
        public void TestConnectionAsync_UnresolvableBroker_FaultsWithSocketException()
        {
            var task = MQTTClient.TestConnectionAsync(UnresolvableBroker, 1883, "", "");

            var exception = Assert.ThrowsException<AggregateException>(() => task.Wait());
            Assert.IsInstanceOfType(exception.InnerException, typeof(SocketException));
        }

        // --- Publish ---

        // Current behaviour (B10): without a connection the message is dropped silently.
        [TestMethod]
        public void Publish_NoClient_DropsMessageWithoutError()
        {
            MQTTClient.Publish("t", "m");

            Assert.IsNull(MQTTClient.CLIENT);
        }

        [TestMethod]
        public void Publish_ClientNotConnected_DropsMessageWithoutError()
        {
            // An IP address skips DNS, and the constructor doesn't connect.
            var client = new MqttClient("127.0.0.1");
            MQTTClient.CLIENT = client;

            MQTTClient.Publish("t", "m");

            Assert.IsFalse(client.IsConnected);
        }

        // --- Watchdog ---

        [TestMethod]
        public void StartWatchdog_CalledTwice_KeepsOneTimer()
        {
            MQTTClient.StartWatchdog();
            object first = WatchdogTimer;
            MQTTClient.StartWatchdog();

            Assert.IsNotNull(first);
            Assert.AreSame(first, WatchdogTimer);
        }

        [TestMethod]
        public void StopWatchdog_RemovesTimerAndCanBeCalledAgain()
        {
            MQTTClient.StartWatchdog();

            MQTTClient.StopWatchdog();
            MQTTClient.StopWatchdog();

            Assert.IsNull(WatchdogTimer);
        }

        [TestMethod]
        public void WatchdogTick_NoBrokerConfigured_DoesNotReconnect()
        {
            WatchdogTick();

            Assert.AreEqual(0, ReconnectAttempt);
            Assert.IsFalse(Reconnecting);
        }

        [TestMethod]
        public void WatchdogTick_ReconnectAlreadyRunning_DoesNothing()
        {
            Properties.Settings.Default["mqttserver"] = UnresolvableBroker;
            TestState.SetStatic(typeof(MQTTClient), "reconnecting", true);

            WatchdogTick();

            Assert.AreEqual(0, ReconnectAttempt);
        }

        [TestMethod]
        public void WatchdogTick_Disconnected_StartsOneReconnectAndClearsFlagAfterward()
        {
            // The empty port makes the delayed ConnectAndSubscribe stop before any network call.
            Properties.Settings.Default["mqttserver"] = UnresolvableBroker;

            WatchdogTick();

            Assert.AreEqual(1, ReconnectAttempt);
            Assert.IsTrue(Reconnecting);

            WatchdogTick();
            Assert.AreEqual(1, ReconnectAttempt, "a second tick during the reconnect must not start another one");

            // First backoff step is 2 seconds.
            var stopwatch = Stopwatch.StartNew();
            while (Reconnecting && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(100);
            }
            Assert.IsFalse(Reconnecting);
            Assert.IsTrue(stopwatch.Elapsed >= TimeSpan.FromSeconds(1.5), stopwatch.Elapsed.ToString());
            Assert.AreEqual(1, ReconnectAttempt, "a failed attempt doesn't reset the counter");
        }
    }
}
