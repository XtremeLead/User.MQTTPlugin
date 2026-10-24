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

        private void Register(params MqttActionSetting[] actions)
        {
            ActionRegistration.Register(registry, actions, (topic, message) => published.Add(topic + "=" + message), log);
        }

        // B4: the whole set is replaced, so a renamed or removed action doesn't stay behind.
        [TestMethod]
        public void Register_ClearsFirstThenAddsEachActionInOrder()
        {
            Register(
                new MqttActionSetting("Light", "home/light", "on"),
                new MqttActionSetting("Climate", "home/climate", "toggle"));

            CollectionAssert.AreEqual(new[] { "clear", "add Light", "add Climate" }, registry.Calls);
            Assert.IsTrue(log.Contains("Info", "Registered 2 MQTT action(s)."));
        }

        [TestMethod]
        public void Register_RunningAnAction_PublishesItsMessageToItsTopic()
        {
            Register(
                new MqttActionSetting("Light", "home/light", "on"),
                new MqttActionSetting("Climate", "home/climate", "toggle"));

            registry.Actions["Climate"]();

            CollectionAssert.AreEqual(new[] { "home/climate=toggle" }, published);
        }

        // The action keeps the topic and message it was registered with.
        [TestMethod]
        public void Register_LaterChangeToTheSetting_DoesNotChangeTheRegisteredAction()
        {
            var light = new MqttActionSetting("Light", "home/light", "on");
            Register(light);

            light.Message = "off";
            registry.Actions["Light"]();

            CollectionAssert.AreEqual(new[] { "home/light=on" }, published);
        }

        [TestMethod]
        public void Register_NoActions_OnlyClears()
        {
            Register();

            CollectionAssert.AreEqual(new[] { "clear" }, registry.Calls);
        }

        [TestMethod]
        public void Register_Renamed_OldNameIsGone()
        {
            Register(new MqttActionSetting("Old", "t", "m"));

            Register(new MqttActionSetting("New", "t", "m"));

            CollectionAssert.AreEqual(new[] { "New" }, registry.Actions.Keys.ToArray());
        }
    }
}
