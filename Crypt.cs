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
                // Encrypt the data using DataProtectionScope.CurrentUser. The result can be decrypted
                // only by the same current user.
                return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("Data was not encrypted. An error occurred.");
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        private static byte[] Unprotect(byte[] data)
        {
            try
            {
                //Decrypt the data using DataProtectionScope.CurrentUser.
                return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("Data was not decrypted. An error occurred.");
                Console.WriteLine(e.ToString());
                return null;
            }
        }
    }
}
