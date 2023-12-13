using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace User.MQTTPlugin
{
    class Crypt
    {
        private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("O40LHbuR4e0p%uC(3vZQ2S_b5o^017A(");

        public static string ProtectString(string value)
        {
            byte[] secret = Encoding.ASCII.GetBytes(value);
            byte[] encryptedSecret = Protect(secret);
            return Convert.ToBase64String(encryptedSecret);
        }
        public static string UnprotectString(string value)
        {
            byte[] secret = Convert.FromBase64String(value);
            byte[] originalData = Unprotect(secret);
            return Encoding.ASCII.GetString(originalData);
        }
        private static byte[] Protect(byte[] data)
        {
            try
            {
                return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                SimHub.Logging.Current.Info("Failed to encrypt MQTT password!");
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
                SimHub.Logging.Current.Info("Failed to decrypt MQTT password!");
                return null;
            }
        }
    }
}
