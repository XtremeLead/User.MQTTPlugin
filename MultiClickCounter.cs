namespace User.MQTTPlugin
{
    /// <summary>
    /// Counts consecutive clicks on the same target. A click counts as part of the series when
    /// it lands on the same target within the double-click time of the previous click.
    /// WPF's own MouseButtonEventArgs.ClickCount doesn't reach 3 in SimHub's text fields, so the
    /// settings page counts clicks itself.
    /// </summary>
    internal sealed class MultiClickCounter
    {
        private readonly uint maxIntervalMs;
        private object lastTarget;
        private int lastTimestamp;
        private int count;

        public MultiClickCounter(uint maxIntervalMs)
        {
            this.maxIntervalMs = maxIntervalMs;
        }

        /// <summary>
        /// Registers a click and returns its position in the series (1 for a new series).
        /// <paramref name="timestampMs"/> is a tick count in milliseconds, like
        /// MouseEventArgs.Timestamp; it may wrap around.
        /// </summary>
        public int Register(object target, int timestampMs)
        {
            uint elapsed = unchecked((uint)(timestampMs - lastTimestamp));
            bool continuesSeries = count > 0 && ReferenceEquals(target, lastTarget) && elapsed <= maxIntervalMs;
            count = continuesSeries ? count + 1 : 1;
            lastTarget = target;
            lastTimestamp = timestampMs;
            return count;
        }
    }
}
