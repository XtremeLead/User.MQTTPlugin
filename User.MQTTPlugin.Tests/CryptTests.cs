using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class CryptTests
    {
        [TestMethod]
        [DataRow("secret", DisplayName = "ascii")]
        [DataRow("wachtwoord met spaties", DisplayName = "spaces")]
        [DataRow("ünïcødé €", DisplayName = "unicode")]
        [DataRow("", DisplayName = "empty")]
        public void ProtectString_ThenUnprotectString_ReturnsOriginal(string password)
        {
            string protectedValue = Crypt.ProtectString(password);
            string unprotectedValue = Crypt.UnprotectString(protectedValue);

            Assert.AreEqual(password, unprotectedValue);
        }

        [TestMethod]
        public void ProtectString_DoesNotReturnPlainText()
        {
            string protectedValue = Crypt.ProtectString("secret");

            Assert.AreNotEqual("secret", protectedValue);
        }

        [TestMethod]
        public void ProtectString_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Crypt.ProtectString(null));
        }

        // B9: the stored default password is "". It means "no password", so there is nothing
        // to decrypt and nothing to log.
        [TestMethod]
        public void UnprotectString_EmptyStoredValue_ReturnsEmptyWithoutLogging()
        {
            using (var log = new LogCapture())
            {
                Assert.AreEqual("", Crypt.UnprotectString(""));
                Assert.AreEqual(0, log.Events.Count);
            }
        }

        [TestMethod]
        public void UnprotectString_Null_ReturnsEmpty()
        {
            Assert.AreEqual("", Crypt.UnprotectString(null));
        }

        [TestMethod]
        public void UnprotectString_ValueNotProtectedByThisUser_ReturnsEmptyAndLogsError()
        {
            string notProtected = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            using (var log = new LogCapture())
            {
                Assert.AreEqual("", Crypt.UnprotectString(notProtected));
                Assert.IsTrue(log.Contains(log4net.Core.Level.Error, "Failed to decrypt MQTT password"));
            }
        }

        [TestMethod]
        public void UnprotectString_InvalidBase64_ReturnsEmptyAndLogsError()
        {
            using (var log = new LogCapture())
            {
                Assert.AreEqual("", Crypt.UnprotectString("not base64!"));
                Assert.IsTrue(log.Contains(log4net.Core.Level.Error, "not valid"));
            }
        }

        // B9: when encryption fails, Protect returns null; that is stored as "" (no password)
        // instead of throwing in the PasswordChanged handler.
        [TestMethod]
        public void ToStoredValue_EncryptionFailed_ReturnsEmpty()
        {
            Assert.AreEqual("", Crypt.ToStoredValue(null));
        }

        [TestMethod]
        public void ToStoredValue_EncryptedBytes_ReturnsBase64()
        {
            Assert.AreEqual("AQID", Crypt.ToStoredValue(new byte[] { 1, 2, 3 }));
        }
    }
}
