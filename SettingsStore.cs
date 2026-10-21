using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace User.MQTTPlugin
{
    /// <summary>
    /// The store behind MQTTSettings. Tests replace it so they never touch the user's settings.
    /// </summary>
    internal interface ISettingsStore
    {
        IEnumerable<string> Names { get; }
        object this[string name] { get; set; }
        void Save();
    }

    internal class PropertiesSettingsStore : ISettingsStore
    {
        public IEnumerable<string> Names => Properties.Settings.Default.Properties.Cast<SettingsProperty>().Select(p => p.Name);

        public object this[string name]
        {
            get => Properties.Settings.Default[name];
            set => Properties.Settings.Default[name] = value;
        }

        public void Save() => Properties.Settings.Default.Save();
    }
}
