using System.Configuration;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    /// <summary>
    /// Pins the real settings definition (Properties/Settings.settings), which FakeSettingsStore
    /// copies. Values are only changed in memory and Reload() restores the test host's own
    /// settings; Save() is never called, so SimHub's user.config is never touched.
    /// </summary>
    [TestClass]
    public class PropertiesSettingsStoreTests
    {
        private readonly PropertiesSettingsStore store = new PropertiesSettingsStore();

        [TestCleanup]
        public void Cleanup() => Properties.Settings.Default.Reload();

        [TestMethod]
        public void Names_MatchFakeSettingsStore()
        {
            CollectionAssert.AreEquivalent(new FakeSettingsStore().Names.ToList(), store.Names.ToList());
        }

        [TestMethod]
        public void Defaults_MatchFakeSettingsStore()
        {
            var fake = new FakeSettingsStore();

            foreach (string name in fake.Names)
            {
                Assert.AreEqual(fake[name], store[name], name);
            }
        }

        [TestMethod]
        public void Indexer_SetsValueInMemory()
        {
            store["mqttserver"] = "broker.example";

            Assert.AreEqual("broker.example", Properties.Settings.Default["mqttserver"]);
        }

        [TestMethod]
        public void Indexer_TextForBooleanSetting_ThrowsWrongType()
        {
            Assert.ThrowsException<SettingsPropertyWrongTypeException>(() => store["mqtttls"] = "True");
        }

        [TestMethod]
        public void Indexer_UnknownName_ThrowsNotFound()
        {
            Assert.ThrowsException<SettingsPropertyNotFoundException>(() => store["doesNotExist"]);
        }
    }
}
