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

        // Current behaviour (B9): the stored default password is "", decryption fails and
        // UnprotectString throws instead of returning an empty password.
        [TestMethod]
        public void UnprotectString_EmptyStoredValue_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Crypt.UnprotectString(""));
        }

        [TestMethod]
        public void UnprotectString_ValueNotProtectedByThisUser_ThrowsArgumentNullException()
        {
            string notProtected = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            Assert.ThrowsException<ArgumentNullException>(() => Crypt.UnprotectString(notProtected));
        }

        [TestMethod]
        public void UnprotectString_InvalidBase64_ThrowsFormatException()
        {
            Assert.ThrowsException<FormatException>(() => Crypt.UnprotectString("not base64!"));
        }
    }
}
