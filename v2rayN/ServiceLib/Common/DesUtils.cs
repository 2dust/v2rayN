using System.Security.Cryptography;
using System.Text;

namespace ServiceLib.Common;

public class DesUtils
{
    /// <summary>
    /// Encrypt
    /// </summary>
    /// <param name="text"></param>
    /// /// <param name="key"></param>
    /// <returns></returns>
    public static string Encrypt(string? text, string? key = null)
    {
        if (text.IsNullOrEmpty())
        {
            return string.Empty;
        }
        GetKeyIv(key ?? GetDefaultKey(), out var rgbKey, out var rgbIv);
        var dsp = DES.Create();
        using var memStream = new MemoryStream();
        using var cryStream = new CryptoStream(memStream, dsp.CreateEncryptor(rgbKey, rgbIv), CryptoStreamMode.Write);
        using var sWriter = new StreamWriter(cryStream);
        sWriter.Write(text);
        sWriter.Flush();
        cryStream.FlushFinalBlock();
        memStream.Flush();
        return Convert.ToBase64String(memStream.GetBuffer(), 0, (int)memStream.Length);
    }

    /// <summary>
    /// Decrypt
    /// </summary>
    /// <param name="encryptText"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string Decrypt(string? encryptText, string? key = null)
    {
        if (encryptText.IsNullOrEmpty())
        {
            return string.Empty;
        }
        GetKeyIv(key ?? GetDefaultKey(), out var rgbKey, out var rgbIv);
        var dsp = DES.Create();
        var buffer = Convert.FromBase64String(encryptText);

        using var memStream = new MemoryStream();
        using var cryStream = new CryptoStream(memStream, dsp.CreateDecryptor(rgbKey, rgbIv), CryptoStreamMode.Write);
        cryStream.Write(buffer, 0, buffer.Length);
        cryStream.FlushFinalBlock();
        return Encoding.UTF8.GetString(memStream.ToArray());
    }

    private static void GetKeyIv(string key, out byte[] rgbKey, out byte[] rgbIv)
    {
        if (key.IsNullOrEmpty())
        {
            throw new ArgumentNullException("The key cannot be null");
        }
        if (key.Length <= 8)
        {
            throw new ArgumentNullException("The key length cannot be less than 8 characters.");
        }

        rgbKey = Encoding.ASCII.GetBytes(key.Substring(0, 8));
        rgbIv = Encoding.ASCII.GetBytes(key.Insert(0, "w").Substring(0, 8));
    }

    private static string GetDefaultKey()
    {
        return Utils.GetMd5(Utils.GetHomePath() + "DesUtils");
    }
}
