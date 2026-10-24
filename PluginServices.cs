using System;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Logging as used by MqttConnectionManager. Tests replace it to check messages and levels.
    /// </summary>
    internal interface IPluginLog
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    internal sealed class SimHubLog : IPluginLog
    {
        public void Debug(string message) => SimHub.Logging.Current.Debug(message);
        public void Info(string message) => SimHub.Logging.Current.Info(message);
        public void Warn(string message) => SimHub.Logging.Current.Warn(message);
        public void Error(string message) => SimHub.Logging.Current.Error(message);
    }

    internal interface IClock
    {
        DateTimeOffset Now { get; }
    }

    internal sealed class SystemClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}
