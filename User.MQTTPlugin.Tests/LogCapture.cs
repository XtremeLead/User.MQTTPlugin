using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Captures what the plugin logs through SimHub.Logging.Current (a log4net logger) while
    /// it is alive. Until F2 adds a logger interface (T4), this is how tests check log levels.
    /// </summary>
    internal sealed class LogCapture : IDisposable
    {
        private readonly MemoryAppender appender = new MemoryAppender();
        private readonly Hierarchy hierarchy;

        public LogCapture()
        {
            hierarchy = (Hierarchy)LogManager.GetRepository(typeof(SimHub.Logging).Assembly);
            hierarchy.Root.Level = Level.All;
            hierarchy.Root.AddAppender(appender);
            hierarchy.Configured = true;
        }

        public IList<LoggingEvent> Events => appender.GetEvents();

        public IEnumerable<string> MessagesAt(Level level)
        {
            return Events.Where(e => e.Level == level).Select(e => e.RenderedMessage);
        }

        public bool Contains(Level level, string text)
        {
            return MessagesAt(level).Any(m => m.Contains(text));
        }

        public void Dispose()
        {
            hierarchy.Root.RemoveAppender(appender);
        }
    }
}
