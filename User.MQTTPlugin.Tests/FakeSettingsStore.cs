using System.Collections.Generic;
using System.Configuration;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// In-memory stand-in for Properties.Settings with the same names, defaults and types.
    /// Like the real store, it refuses a value of the wrong type.
    /// </summary>
    internal class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>
        {
            { "mqttserver", "" },
            { "mqttport", "" },
            { "mqttuser", "" },
            { "mqttpass", "" },
            { "mqtttopic", "" },
            { "lastConnectionSuccessful", false },
            { "mqttactions", "" },
            { "mqtttls", false },
        };

        public int SaveCount { get; private set; }

        public IEnumerable<string> Names => values.Keys;

        public object this[string name]
        {
            get
            {
                if (!values.ContainsKey(name)) throw new SettingsPropertyNotFoundException(name);
                return values[name];
            }
            set
            {
                if (!values.ContainsKey(name)) throw new SettingsPropertyNotFoundException(name);
                if (value != null && values[name].GetType() != value.GetType()) throw new SettingsPropertyWrongTypeException(name);
                values[name] = value;
            }
        }

        public void Save() => SaveCount++;
    }
}
