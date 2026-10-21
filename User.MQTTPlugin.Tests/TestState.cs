using System;
using System.Collections.Generic;
using System.Reflection;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Resets the static state of the plugin between tests. The production code keeps its
    /// state in private static fields, so this goes through reflection until F1/F2 add seams.
    /// Properties.Settings.Default is only changed in memory and never saved: Reload() restores
    /// the test host's own settings, never SimHub's user.config.
    /// </summary>
    internal static class TestState
    {
        public static void Reset()
        {
            MQTTClient.StopWatchdog();
            MQTTClient.CLIENT = null;
            MQTTClient.IsConnected = false;
            SetStatic(typeof(MQTTClient), "reconnectAttempt", 0);
            SetStatic(typeof(MQTTClient), "reconnecting", false);
            ((IDictionary<string, string>)GetStatic(typeof(MQTTSettings), "SETTINGS")).Clear();
            Properties.Settings.Default.Reload();
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
