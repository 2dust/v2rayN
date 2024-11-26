using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ServiceLib.Common
{
    public class AesUtils
    {
        private const int KeySize = 256; // AES-256
        private const int IvSize = 16;   // AES block size
        private const int Iterations = 10000;

        private static readonly byte[] Salt = Encoding.ASCII.GetBytes("saltysalt".PadRight(16, ' ')); // google浏览器默认盐值

        private static readonly string DefaultPassword =Utils.GetHomePath() + "AesUtils";

        /// <summary>
        /// Encrypt
        /// </summary>
        /// <param name="text">Plain text</param>
        /// <param name="password">Password for key derivation or direct key in ASCII bytes</param>
        /// <returns>Base64 encoded cipher text with IV</returns>
        public static string Encrypt(string text, string password = null)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            byte[] plaintext = Encoding.UTF8.GetBytes(text);
            byte[] key = GetKey(password);
            byte[] iv = GenerateIv();

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length);

                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(plaintext, 0, plaintext.Length);
                        cs.FlushFinalBlock();
                    }

                    byte[] cipherTextWithIv = ms.ToArray();
                    return Convert.ToBase64String(cipherTextWithIv);
                }
            }
        }

        /// <summary>
        /// Decrypt
        /// </summary>
        /// <param name="cipherTextWithIv">Base64 encoded cipher text with IV</param>
        /// <param name="password">Password for key derivation or direct key in ASCII bytes</param>
        /// <returns>Plain text</returns>
        public static string Decrypt(string cipherTextWithIv, string password = null)
        {
            if (string.IsNullOrEmpty(cipherTextWithIv))
                return string.Empty;

            byte[] cipherTextWithIvBytes = Convert.FromBase64String(cipherTextWithIv);
            byte[] key = GetKey(password);

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(cipherTextWithIvBytes, 0, iv, 0, IvSize);

            byte[] cipherText = new byte[cipherTextWithIvBytes.Length - IvSize];
            Buffer.BlockCopy(cipherTextWithIvBytes, IvSize, cipherText, 0, cipherText.Length);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherText, 0, cipherText.Length);
                        cs.FlushFinalBlock();
                    }

                    byte[] plainText = ms.ToArray();
                    return Encoding.UTF8.GetString(plainText);
                }
            }
        }

        private static byte[] GetKey(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return GetDefaultKey();
            }
            else
            {
                byte[] key = Encoding.ASCII.GetBytes(password);
                if (key.Length != KeySize / 8)
                {
                    throw new ArgumentException($"Password bytes length must be {KeySize / 8} bytes.");
                }
                return key;
            }
        }

        private static byte[] GetDefaultKey()
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(DefaultPassword, Salt, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(KeySize / 8);
            }
        }

        private static byte[] GenerateIv()
        {
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] iv = new byte[IvSize];
                rng.GetBytes(iv);
                return iv;
            }
        }
    }
}
