using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using log4net.Core;
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

        private static void WaitForReconnectToFinish()
        {
            var stopwatch = Stopwatch.StartNew();
            while (Reconnecting && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(10);
            }
            Assert.IsFalse(Reconnecting, "the reconnect task didn't finish");
        }

        // --- ConnectAndSubscribe ---

        // B7: without a broker there is nothing to connect to; that is normal, not a warning.
        [TestMethod]
        public void ConnectAndSubscribe_NoBrokerConfigured_DoesNotConnectAndLogsInfo()
        {
            TestState.Store["mqttport"] = "1883";

            using (var log = new LogCapture())
            {
                MQTTClient.ConnectAndSubscribe();

                Assert.IsTrue(log.Contains(Level.Info, "No MQTT broker configured"));
                Assert.AreEqual(0, log.MessagesAt(Level.Warn).Count());
            }
            Assert.IsNull(MQTTClient.CLIENT);
        }

        [TestMethod]
        public void ConnectAndSubscribe_NonNumericPort_DoesNotConnectAndWarns()
        {
            TestState.Store["mqttserver"] = UnresolvableBroker;
            TestState.Store["mqttport"] = "abc";

            using (var log = new LogCapture())
            {
                MQTTClient.ConnectAndSubscribe();

                Assert.IsTrue(log.Contains(Level.Warn, "Invalid or missing MQTT port 'abc'"));
            }
            Assert.IsNull(MQTTClient.CLIENT);
        }

        // B1: a failing DNS lookup is a failed connect, not an exception that stops the caller
        // (Init, the Save button or the watchdog).
        [TestMethod]
        public void ConnectAndSubscribe_UnresolvableBroker_FailsWithoutThrowing()
        {
            TestState.Store["mqttserver"] = UnresolvableBroker;
            TestState.Store["mqttport"] = "1883";
            TestState.Store["lastConnectionSuccessful"] = true;
            MQTTClient.IsConnected = true;

            using (var log = new LogCapture())
            {
                MQTTClient.ConnectAndSubscribe();

                Assert.IsTrue(log.Contains(Level.Warn, "Failed to connect to MQTT Broker " + UnresolvableBroker));
            }
            Assert.IsNull(MQTTClient.CLIENT);
            Assert.IsFalse(MQTTClient.IsConnected);
            Assert.AreEqual(false, TestState.Store["lastConnectionSuccessful"]);
        }

        // B1: the Test button gets "failed" instead of a faulted task.
        [TestMethod]
        public void TestConnectionAsync_UnresolvableBroker_ReturnsFalse()
        {
            using (var log = new LogCapture())
            {
                bool result = MQTTClient.TestConnectionAsync(UnresolvableBroker, 1883, "", "").Result;

                Assert.IsFalse(result);
                Assert.IsTrue(log.Contains(Level.Warn, "Connection test to connect to MQTT Broker " + UnresolvableBroker + " failed"));
            }
            Assert.AreEqual(false, TestState.Store["lastConnectionSuccessful"]);
        }

        // --- Publish ---

        // B10: a message that can't be sent is reported instead of dropped silently.
        [TestMethod]
        public void Publish_NoClient_WarnsThatMessageWasNotPublished()
        {
            using (var log = new LogCapture())
            {
                MQTTClient.Publish("home/light", "on");

                Assert.IsTrue(log.Contains(Level.Warn, "message to topic home/light was not published"));
            }
        }

        [TestMethod]
        public void Publish_ClientNotConnected_WarnsThatMessageWasNotPublished()
        {
            // An IP address skips DNS, and the constructor doesn't connect.
            var client = new MqttClient("127.0.0.1");
            MQTTClient.CLIENT = client;

            using (var log = new LogCapture())
            {
                MQTTClient.Publish("home/light", "on");

                Assert.IsTrue(log.Contains(Level.Warn, "message to topic home/light was not published"));
            }
            Assert.IsFalse(client.IsConnected);
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
            Assert.AreEqual(expectedSeconds, MQTTClient.GetBackoffDelaySeconds(attempt));
        }

        [TestMethod]
        public void SleepSeconds_ByDefault_SleepsThatManySeconds()
        {
            var stopwatch = Stopwatch.StartNew();

            TestState.OriginalSleepSeconds(1);

            Assert.IsTrue(stopwatch.Elapsed >= TimeSpan.FromSeconds(0.9), stopwatch.Elapsed.ToString());
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
            TestState.Store["mqttserver"] = UnresolvableBroker;
            TestState.SetStatic(typeof(MQTTClient), "reconnecting", true);

            WatchdogTick();

            Assert.AreEqual(0, ReconnectAttempt);
        }

        [TestMethod]
        public void WatchdogTick_DuringReconnect_DoesNotStartAnother()
        {
            // The empty port makes the delayed ConnectAndSubscribe stop before any network call.
            TestState.Store["mqttserver"] = UnresolvableBroker;
            var release = new ManualResetEventSlim();
            MQTTClient.SleepSeconds = seconds => release.Wait(TimeSpan.FromSeconds(10));

            WatchdogTick();
            Assert.IsTrue(Reconnecting);
            WatchdogTick();
            release.Set();
            WaitForReconnectToFinish();

            Assert.AreEqual(1, ReconnectAttempt);
        }

        // Failed attempts never reset the counter, so the waits follow the backoff schedule.
        [TestMethod]
        public void WatchdogTick_RepeatedFailures_WaitsAccordingToBackoff()
        {
            TestState.Store["mqttserver"] = UnresolvableBroker;
            var waits = new List<int>();
            MQTTClient.SleepSeconds = seconds => waits.Add(seconds);

            for (int i = 0; i < 7; i++)
            {
                WatchdogTick();
                WaitForReconnectToFinish();
            }

            CollectionAssert.AreEqual(new[] { 2, 5, 10, 30, 60, 60, 60 }, waits);
            Assert.AreEqual(7, ReconnectAttempt);
        }

        // B1: an unreachable broker makes the reconnect fail quietly, so the next tick retries.
        [TestMethod]
        public void WatchdogTick_UnresolvableBroker_FailsAndAllowsNextAttempt()
        {
            TestState.Store["mqttserver"] = UnresolvableBroker;
            TestState.Store["mqttport"] = "1883";

            using (var log = new LogCapture())
            {
                WatchdogTick();
                WaitForReconnectToFinish();
                WatchdogTick();
                WaitForReconnectToFinish();

                Assert.AreEqual(0, log.MessagesAt(Level.Error).Count());
            }
            Assert.AreEqual(2, ReconnectAttempt);
            Assert.IsNull(MQTTClient.CLIENT);
        }

        [TestMethod]
        public void WatchdogTick_ReconnectThrows_LogsErrorAndClearsFlag()
        {
            TestState.Store["mqttserver"] = UnresolvableBroker;
            MQTTClient.SleepSeconds = seconds => { throw new InvalidOperationException("boom"); };

            using (var log = new LogCapture())
            {
                WatchdogTick();
                WaitForReconnectToFinish();

                Assert.IsTrue(log.Contains(Level.Error, "MQTT reconnect failed: boom"));
            }
        }
    }
}
