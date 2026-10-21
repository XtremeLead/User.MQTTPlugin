using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Resets the static state of the plugin between tests and swaps in test doubles: a
    /// FakeSettingsStore, a reconnect that does nothing and a watchdog sleep that returns at once.
    /// Private static fields are reached through reflection until F2 replaces them.
    /// </summary>
    internal static class TestState
    {
        // The production wiring, captured before the first Reset() replaces it.
        public static readonly ISettingsStore OriginalStore = MQTTSettings.Store;
        public static readonly Func<Task> OriginalReconnect = MQTTSettings.Reconnect;
        public static readonly Action<int> OriginalSleepSeconds = MQTTClient.SleepSeconds;

        // An explicit static constructor makes the fields above initialize before the first
        // Reset() runs, instead of lazily at their first use.
        static TestState()
        {
        }

        public static FakeSettingsStore Store => (FakeSettingsStore)MQTTSettings.Store;

        public static void Reset()
        {
            MQTTClient.StopWatchdog();
            MQTTClient.CLIENT = null;
            MQTTClient.IsConnected = false;
            SetStatic(typeof(MQTTClient), "reconnectAttempt", 0);
            SetStatic(typeof(MQTTClient), "reconnecting", false);
            MQTTClient.SleepSeconds = seconds => { };
            ((IDictionary<string, string>)GetStatic(typeof(MQTTSettings), "SETTINGS")).Clear();
            MQTTSettings.Store = new FakeSettingsStore();
            MQTTSettings.Reconnect = () => Task.CompletedTask;
            SetPluginInstance(null);
        }

        public static object GetStatic(Type type, string field)
        {
            return type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

        public static void SetStatic(Type type, string field, object value)
        {
            type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
        }

        public static void SetPluginInstance(MQTTPlugin plugin)
        {
            typeof(MQTTPlugin).GetProperty("Instance").SetValue(null, plugin);
        }

        public static object InvokeStatic(Type type, string method, params object[] args)
        {
            try
            {
                return type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}
