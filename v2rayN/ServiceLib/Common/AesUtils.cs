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
		private static readonly string DefaultPassword = Utils.GetMd5(Utils.GetHomePath() + "AesUtils");

		/// <summary>
		/// Encrypt
		/// </summary>
		/// <param name="text">Plain text</param>
		/// <param name="password">Password for key derivation or direct key in ASCII bytes</param>
		/// <returns>Base64 encoded cipher text with IV</returns>
		public static string Encrypt(string text, string? password = null)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;

			var plaintext = Encoding.UTF8.GetBytes(text);
			var key = GetKey(password);
			var iv = GenerateIv();

			using var aes = Aes.Create();
			aes.Key = key;
			aes.IV = iv;

			using var ms = new MemoryStream();
			ms.Write(iv, 0, iv.Length);

			using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
			{
				cs.Write(plaintext, 0, plaintext.Length);
				cs.FlushFinalBlock();
			}

			var cipherTextWithIv = ms.ToArray();
			return Convert.ToBase64String(cipherTextWithIv);
		}

		/// <summary>
		/// Decrypt
		/// </summary>
		/// <param name="cipherTextWithIv">Base64 encoded cipher text with IV</param>
		/// <param name="password">Password for key derivation or direct key in ASCII bytes</param>
		/// <returns>Plain text</returns>
		public static string Decrypt(string cipherTextWithIv, string? password = null)
		{
			if (string.IsNullOrEmpty(cipherTextWithIv))
				return string.Empty;

			var cipherTextWithIvBytes = Convert.FromBase64String(cipherTextWithIv);
			var key = GetKey(password);

			var iv = new byte[IvSize];
			Buffer.BlockCopy(cipherTextWithIvBytes, 0, iv, 0, IvSize);

			var cipherText = new byte[cipherTextWithIvBytes.Length - IvSize];
			Buffer.BlockCopy(cipherTextWithIvBytes, IvSize, cipherText, 0, cipherText.Length);

			using var aes = Aes.Create();
			aes.Key = key;
			aes.IV = iv;

			using var ms = new MemoryStream();
			using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
			{
				cs.Write(cipherText, 0, cipherText.Length);
				cs.FlushFinalBlock();
			}

			var plainText = ms.ToArray();
			return Encoding.UTF8.GetString(plainText);
		}

		private static byte[] GetKey(string? password)
		{
			if (password.IsNullOrEmpty())
			{
				password = DefaultPassword;
			}

			using var pbkdf2 = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA256);
			return pbkdf2.GetBytes(KeySize / 8);
		}

		private static byte[] GenerateIv()
		{
			var randomNumber = new byte[IvSize];

			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(randomNumber);
			return randomNumber;
		}
	}
}
