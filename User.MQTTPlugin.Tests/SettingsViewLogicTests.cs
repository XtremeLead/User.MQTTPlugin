using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class SettingsViewLogicTests
    {
        // --- InitialConnectionStatus ---

        // B7: a fresh install has no broker and no connection, so it asks for settings.
        [TestMethod]
        public void InitialConnectionStatus_NoSettingsNotConnected_AsksForSettings()
        {
            Assert.AreEqual(SettingsViewLogic.NoSettingsMessage, SettingsViewLogic.InitialConnectionStatus(false, false));
        }

        [TestMethod]
        public void InitialConnectionStatus_NoSettingsButConnected_ShowsSuccess()
        {
            Assert.AreEqual(SettingsViewLogic.ConnectionSuccessMessage, SettingsViewLogic.InitialConnectionStatus(false, true));
        }

        [TestMethod]
        public void InitialConnectionStatus_SettingsAndConnected_ShowsSuccess()
        {
            Assert.AreEqual(SettingsViewLogic.ConnectionSuccessMessage, SettingsViewLogic.InitialConnectionStatus(true, true));
        }

        [TestMethod]
        public void InitialConnectionStatus_SettingsNotConnected_ShowsFailure()
        {
            Assert.AreEqual(SettingsViewLogic.ConnectionFailedMessage, SettingsViewLogic.InitialConnectionStatus(true, false));
        }

        [TestMethod]
        public void Messages_HaveTheirCurrentText()
        {
            Assert.AreEqual("Please provide MQTT settings.", SettingsViewLogic.NoSettingsMessage);
            Assert.AreEqual("Connection failed. Please check your settings or broker availability.", SettingsViewLogic.ConnectionFailedMessage);
            Assert.AreEqual("Successfully connected. Happy simracing!", SettingsViewLogic.ConnectionSuccessMessage);
            Assert.AreEqual("Successfully connected. Click Save to Save your settings and connect.", SettingsViewLogic.ConnectionTestSuccessMessage);
            Assert.AreEqual("Could not save the settings. Please check the SimHub log.", SettingsViewLogic.SaveFailedMessage);
        }

        // --- PortAfterTlsChange ---

        [TestMethod]
        [DataRow(true, "1883", "8883", DisplayName = "TLS on switches 1883 to 8883")]
        [DataRow(false, "8883", "1883", DisplayName = "TLS off switches 8883 to 1883")]
        [DataRow(true, "8883", "8883", DisplayName = "TLS on keeps 8883")]
        [DataRow(false, "1883", "1883", DisplayName = "TLS off keeps 1883")]
        [DataRow(true, "1234", "1234", DisplayName = "TLS on keeps a custom port")]
        [DataRow(false, "1234", "1234", DisplayName = "TLS off keeps a custom port")]
        [DataRow(true, "", "", DisplayName = "TLS on keeps an empty port")]
        public void PortAfterTlsChange_SwapsOnlyDefaultPorts(bool useTls, string port, string expected)
        {
            Assert.AreEqual(expected, SettingsViewLogic.PortAfterTlsChange(useTls, port));
        }

        [TestMethod]
        public void PortAfterTlsChange_Indeterminate_KeepsPort()
        {
            Assert.AreEqual("8883", SettingsViewLogic.PortAfterTlsChange(null, "8883"));
            Assert.AreEqual("1883", SettingsViewLogic.PortAfterTlsChange(null, "1883"));
        }

        // --- IsPortKey ---

        [TestMethod]
        [DataRow(Key.D0, DisplayName = "D0")]
        [DataRow(Key.D9, DisplayName = "D9")]
        [DataRow(Key.NumPad0, DisplayName = "NumPad0")]
        [DataRow(Key.NumPad9, DisplayName = "NumPad9")]
        public void IsPortKey_Digit_ReturnsTrue(Key key)
        {
            Assert.IsTrue(SettingsViewLogic.IsPortKey(key));
        }

        // Backspace, Delete, Tab and the arrow keys are not digits either. Whether the port box
        // still handles them depends on WPF and is checked by hand.
        [TestMethod]
        [DataRow(Key.A, DisplayName = "A")]
        [DataRow(Key.Space, DisplayName = "Space")]
        [DataRow(Key.OemMinus, DisplayName = "minus")]
        [DataRow(Key.Back, DisplayName = "Backspace")]
        [DataRow(Key.Delete, DisplayName = "Delete")]
        [DataRow(Key.Tab, DisplayName = "Tab")]
        [DataRow(Key.Left, DisplayName = "Left")]
        public void IsPortKey_OtherKey_ReturnsFalse(Key key)
        {
            Assert.IsFalse(SettingsViewLogic.IsPortKey(key));
        }

        // --- Actions ---

        [TestMethod]
        [DataRow("text", true, "", DisplayName = "placeholder counts as empty")]
        [DataRow("  name  ", false, "name", DisplayName = "real text is trimmed")]
        [DataRow(null, false, "", DisplayName = "null becomes empty")]
        public void RealText_ReturnsWhatTheUserTyped(string text, bool isPlaceholder, string expected)
        {
            Assert.AreEqual(expected, SettingsViewLogic.RealText(text, isPlaceholder));
        }

        [TestMethod]
        [DataRow("n", "t", "m", true, DisplayName = "all filled")]
        [DataRow("", "t", "m", false, DisplayName = "no name")]
        [DataRow("n", "", "m", false, DisplayName = "no topic")]
        [DataRow("n", "t", "", false, DisplayName = "no message")]
        [DataRow(null, "t", "m", false, DisplayName = "null name")]
        public void IsCompleteAction_RequiresAllThreeFields(string name, string topic, string message, bool expected)
        {
            Assert.AreEqual(expected, SettingsViewLogic.IsCompleteAction(name, topic, message));
        }

        [TestMethod]
        [DataRow(2, 0, "Saved 2 action(s).", DisplayName = "nothing skipped")]
        [DataRow(0, 0, "Saved 0 action(s).", DisplayName = "nothing at all")]
        [DataRow(1, 2, "Saved 1 action(s); 2 incomplete row(s) skipped — fill in all three fields.", DisplayName = "rows skipped")]
        public void ActionsSavedMessage_ReportsSavedAndSkipped(int saved, int skipped, string expected)
        {
            Assert.AreEqual(expected, SettingsViewLogic.ActionsSavedMessage(saved, skipped));
        }
    }
}
