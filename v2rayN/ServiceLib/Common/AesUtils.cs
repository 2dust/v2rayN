namespace ServiceLib.Common;

public static class AesUtils
{
    private static void GetKeyIv(out byte[] key, out byte[] iv)
    {
        var rawKey = (Utils.GetHomePath() ?? string.Empty) + "v2rayN_Pattn_Key";
        key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        iv = MD5.HashData(Encoding.UTF8.GetBytes("v2rayN_Pattn_Iv" + (Utils.GetHomePath() ?? string.Empty)));
    }

    public static string Encrypt(string? text)
    {
        if (text.IsNullOrEmpty())
        {
            return string.Empty;
        }

        try
        {
            GetKeyIv(out var key, out var iv);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            var textBytes = Encoding.UTF8.GetBytes(text);
            var encrypted = encryptor.TransformFinalBlock(textBytes, 0, textBytes.Length);
            return Convert.ToBase64String(encrypted);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AesUtils.Encrypt", ex);
            return string.Empty;
        }
    }

    public static string Decrypt(string? encryptedText)
    {
        if (encryptedText.IsNullOrEmpty())
        {
            return string.Empty;
        }

        try
        {
            GetKeyIv(out var key, out var iv);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AesUtils.Decrypt", ex);
            return string.Empty;
        }
    }
}
