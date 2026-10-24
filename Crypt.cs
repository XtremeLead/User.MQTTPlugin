using System;
using System.Security.Cryptography;
using System.Text;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Encrypts the MQTT password for storage with DPAPI, so only the current Windows user can
    /// decrypt it.
    /// </summary>
    internal static class Crypt
    {
        private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("O40LHbuR4e0p%uC(3vZQ2S_b5o^017A(");

        /// <summary>
        /// Encrypts the password for storage. Returns "" when encryption fails, so the password
        /// is lost rather than stored in plain text; the failure is logged.
        /// </summary>
        public static string ProtectString(string value)
        {
            byte[] secret = Encoding.UTF8.GetBytes(value);
            return ToStoredValue(Protect(secret));
        }

        /// <summary>
        /// Decrypts a stored password. Returns "" for an empty stored value (no password set) and
        /// when decryption fails; never throws.
        /// </summary>
        public static string UnprotectString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            byte[] secret;
            try
            {
                secret = Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                SimHub.Logging.Current.Error("Stored MQTT password is not valid; using an empty password.");
                return "";
            }
            byte[] originalData = Unprotect(secret);
            return originalData == null ? "" : Encoding.UTF8.GetString(originalData);
        }

        internal static string ToStoredValue(byte[] encryptedSecret)
        {
            return encryptedSecret == null ? "" : Convert.ToBase64String(encryptedSecret);
        }

        private static byte[] Protect(byte[] data)
        {
            try
            {
                return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                SimHub.Logging.Current.Error("Failed to encrypt MQTT password! The password is not saved.");
                return null;
            }
        }

        private static byte[] Unprotect(byte[] data)
        {
            try
            {
                return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                SimHub.Logging.Current.Error("Failed to decrypt MQTT password! Using an empty password.");
                return null;
            }
        }
    }
}
