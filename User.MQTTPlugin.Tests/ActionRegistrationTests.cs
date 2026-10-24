using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class ActionRegistrationTests
    {
        private sealed class FakeActionRegistry : IActionRegistry
        {
            public List<string> Calls { get; } = new List<string>();
            public Dictionary<string, Action> Actions { get; } = new Dictionary<string, Action>();

            public void Clear()
            {
                Calls.Add("clear");
                Actions.Clear();
            }

            public void Add(string name, Action run)
            {
                Calls.Add("add " + name);
                Actions[name] = run;
            }
        }

        private readonly FakeActionRegistry registry = new FakeActionRegistry();
        private readonly FakePluginLog log = new FakePluginLog();
        private readonly List<string> published = new List<string>();

        private void Register(Dictionary<string, ValueTuple<string, string>> actions)
        {
            ActionRegistration.Register(registry, actions, (topic, message) => published.Add(topic + "=" + message), log);
        }

        // B4: the whole set is replaced, so a renamed or removed action doesn't stay behind.
        [TestMethod]
        public void Register_ClearsFirstThenAddsEachActionInOrder()
        {
            Register(new Dictionary<string, ValueTuple<string, string>>
            {
                { "Light", ("home/light", "on") },
                { "Climate", ("home/climate", "toggle") },
            });

            CollectionAssert.AreEqual(new[] { "clear", "add Light", "add Climate" }, registry.Calls);
            Assert.IsTrue(log.Contains("Info", "Registered 2 MQTT action(s)."));
        }

        [TestMethod]
        public void Register_RunningAnAction_PublishesItsMessageToItsTopic()
        {
            Register(new Dictionary<string, ValueTuple<string, string>>
            {
                { "Light", ("home/light", "on") },
                { "Climate", ("home/climate", "toggle") },
            });

            registry.Actions["Climate"]();

            CollectionAssert.AreEqual(new[] { "home/climate=toggle" }, published);
        }

        [TestMethod]
        public void Register_NoActions_OnlyClears()
        {
            Register(new Dictionary<string, ValueTuple<string, string>>());

            CollectionAssert.AreEqual(new[] { "clear" }, registry.Calls);
        }

        [TestMethod]
        public void Register_Renamed_OldNameIsGone()
        {
            Register(new Dictionary<string, ValueTuple<string, string>> { { "Old", ("t", "m") } });

            Register(new Dictionary<string, ValueTuple<string, string>> { { "New", ("t", "m") } });

            CollectionAssert.AreEqual(new[] { "New" }, registry.Actions.Keys.ToArray());
        }

        // --- LoadSaved ---

        [TestMethod]
        public void LoadSaved_StoredXml_ReturnsActions()
        {
            var saved = new Dictionary<string, ValueTuple<string, string>> { { "Light", ("home/light", "on") } };
            var settings = new Dictionary<string, string> { { "mqttactions", ActionsSerializer.Serialize(saved) } };

            Dictionary<string, ValueTuple<string, string>> actions = ActionRegistration.LoadSaved(settings, log);

            Assert.AreEqual(("home/light", "on"), actions["Light"]);
            Assert.AreEqual(0, log.Levels.Count);
        }

        [TestMethod]
        public void LoadSaved_NoActionsSaved_ReturnsNoneWithoutLogging()
        {
            var settings = new Dictionary<string, string> { { "mqttactions", "" } };

            Assert.AreEqual(0, ActionRegistration.LoadSaved(settings, log).Count);
            Assert.AreEqual(0, log.Levels.Count);
        }

        [TestMethod]
        public void LoadSaved_SettingMissing_ReturnsNone()
        {
            Assert.AreEqual(0, ActionRegistration.LoadSaved(new Dictionary<string, string>(), log).Count);
        }

        [TestMethod]
        public void LoadSaved_UnreadableXml_ReturnsNoneAndLogsError()
        {
            var settings = new Dictionary<string, string> { { "mqttactions", "<not-closed>" } };

            Assert.AreEqual(0, ActionRegistration.LoadSaved(settings, log).Count);
            Assert.IsTrue(log.Contains("Error", "Could not load saved MQTT actions"));
        }
    }
}
