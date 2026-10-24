using System;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// In-memory stand-in for SimHub's settings storage. Load and Save copy the settings, like
    /// the JSON round trip does, so a test can't change the stored settings by accident.
    /// </summary>
    internal sealed class FakePluginSettingsStore : IPluginSettingsStore
    {
        public FakePluginSettingsStore(MQTTPluginSettings stored)
        {
            Stored = stored;
        }

        /// <summary>What is "on disk"; null like a missing file before SimHub supplies the default.</summary>
        public MQTTPluginSettings Stored { get; set; }

        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }

        /// <summary>Thrown by Save, for example to simulate a file that can't be written.</summary>
        public Exception SaveException { get; set; }

        public MQTTPluginSettings Load()
        {
            LoadCount++;
            return Stored?.Clone();
        }

        public void Save(MQTTPluginSettings settings)
        {
            SaveCount++;
            if (SaveException != null) throw SaveException;
            Stored = settings.Clone();
        }
    }
}
