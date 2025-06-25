using System.Security.Cryptography;
using System.Text;

namespace Monexia.Helpers
{
    public static class AESHelper
    {
        private static readonly string Key = "MonexiaSuperSecureKey123!"; // En az 16 karakter (AES-128 için)

        public static string Encrypt(string plainText)
        {
            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
            aes.IV = new byte[16]; // 0 IV (isteğe bağlı random da olabilir)

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return "";

            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
            aes.IV = new byte[16]; // 0 IV

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
    }
}
