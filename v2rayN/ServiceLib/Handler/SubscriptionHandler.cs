using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace ServiceLib.Handler;

public static class SubscriptionHandler
{
    public static async Task UpdateProcess(
        Config config,
        string subId,
        bool blProxy,
        Func<bool, string, Task> updateFunc,
        Func<SubItem, Task>? decryptFailedFunc = null)
    {
        await updateFunc?.Invoke(false, ResUI.MsgUpdateSubscriptionStart);
        var subItem = await AppManager.Instance.SubItems();

        if (subItem is not { Count: > 0 })
        {
            await updateFunc?.Invoke(false, ResUI.MsgNoValidSubscription);
            return;
        }

        var successCount = 0;
        foreach (var item in subItem)
        {
            try
            {
                if (!IsValidSubscription(item, subId))
                {
                    continue;
                }

                var hashCode = $"{item.Remarks}->";
                if (item.Enabled == false)
                {
                    await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                    continue;
                }

                // Create download handler
                var downloadHandle = CreateDownloadHandler(hashCode, updateFunc);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                // Get all subscription content (main subscription + additional subscriptions)
                var (result, decryptFailed) = await DownloadAllSubscriptions(config, item, blProxy, downloadHandle);
                if (decryptFailed)
                {
                    await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSubscriptionDecryptFailed}");
                    await decryptFailedFunc?.Invoke(item);
                    continue;
                }

                // Process download result
                if (await ProcessDownloadResult(config, item.Id, result, hashCode, updateFunc))
                {
                    successCount++;
                }

                await updateFunc?.Invoke(false, "-------------------------------------------------------");
            }
            catch (Exception ex)
            {
                var hashCode = $"{item.Remarks}->";
                Logging.SaveLog("UpdateSubscription", ex);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgFailedImportSubscription}: {ex.Message}");
                await updateFunc?.Invoke(false, "-------------------------------------------------------");
            }
        }

        await updateFunc?.Invoke(successCount > 0, $"{ResUI.MsgUpdateSubscriptionEnd}");
    }

    private static bool IsValidSubscription(SubItem item, string subId)
    {
        var id = item.Id.TrimEx();
        var url = item.Url.TrimEx();

        if (id.IsNullOrEmpty() || url.IsNullOrEmpty())
        {
            return false;
        }

        if (subId.IsNotEmpty() && item.Id != subId)
        {
            return false;
        }

        if (!url.StartsWith(Global.HttpsProtocol) && !url.StartsWith(Global.HttpProtocol))
        {
            return false;
        }

        return true;
    }

    private static DownloadService CreateDownloadHandler(string hashCode, Func<bool, string, Task> updateFunc)
    {
        var downloadHandle = new DownloadService();
        downloadHandle.Error += (sender2, args) =>
        {
            updateFunc?.Invoke(false, $"{hashCode}{args.GetException().Message}");
        };
        return downloadHandle;
    }

    private const string SubscriptionEncryptionHeader = "Subscription-Encryption";
    private const string AesEncryptionValue = "true";

    private static bool IsAesEncrypted(HttpHeaders? headers)
    {
        if (headers == null || !headers.TryGetValues(SubscriptionEncryptionHeader, out var values))
            return false;
        return values?.Any(v => string.Equals(v.Trim(), AesEncryptionValue, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static async Task<(string Content, HttpHeaders? Headers)> DownloadSubscriptionContentWithHeaders(DownloadService downloadHandle, string url, bool blProxy, string userAgent)
    {
        var (content, headers) = await downloadHandle.TryDownloadStringWithHeaders(url, blProxy, userAgent);

        // If download with proxy fails, try direct connection
        if (blProxy && content.IsNullOrEmpty())
        {
            (content, headers) = await downloadHandle.TryDownloadStringWithHeaders(url, false, userAgent);
        }

        return (content ?? string.Empty, headers);
    }

    private static async Task<(string Result, bool DecryptFailed)> DownloadAllSubscriptions(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        var decryptFailed = false;
        // Download main subscription content (with headers for encryption detection)
        var (result, mainHeaders) = await DownloadMainSubscriptionWithHeaders(config, item, blProxy, downloadHandle);
        if (IsAesEncrypted(mainHeaders))
        {
            if (item.LoginPassword.IsNullOrEmpty())
            {
                return (string.Empty, true);
            }
            if (!TryDecryptSubscription(item, result, out var decrypted))
            {
                return (string.Empty, true);
            }
            result = decrypted;
        }
        else
        {
            if (result.IsNotEmpty() && Utils.IsBase64String(result))
            {
                result = Utils.Base64Decode(result);
            }
        }

        // Process additional subscription links (if any)
        if (item.ConvertTarget.IsNullOrEmpty() && item.MoreUrl.TrimEx().IsNotEmpty())
        {
            var additional = await DownloadAdditionalSubscriptions(item, result, blProxy, downloadHandle);
            if (additional.DecryptFailed)
            {
                return (string.Empty, true);
            }
            result = additional.Result;
        }

        return (result, decryptFailed);
    }

    private static async Task<(string Content, HttpHeaders? Headers)> DownloadMainSubscriptionWithHeaders(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        // Prepare subscription URL and download directly
        var url = Utils.GetPunycode(item.Url.TrimEx());

        // If conversion is needed
        if (item.ConvertTarget.IsNotEmpty())
        {
            var subConvertUrl = config.ConstItem.SubConvertUrl.IsNullOrEmpty()
                ? Global.SubConvertUrls.FirstOrDefault()
                : config.ConstItem.SubConvertUrl;

            url = string.Format(subConvertUrl!, Utils.UrlEncode(url));

            if (!url.Contains("target="))
            {
                url += string.Format("&target={0}", item.ConvertTarget);
            }

            if (!url.Contains("config="))
            {
                url += string.Format("&config={0}", Global.SubConvertConfig.FirstOrDefault());
            }
        }

        // Download and return result with headers
        return await DownloadSubscriptionContentWithHeaders(downloadHandle, url, blProxy, item.UserAgent);
    }

    private static async Task<(string Result, bool DecryptFailed)> DownloadAdditionalSubscriptions(SubItem item, string mainResult, bool blProxy, DownloadService downloadHandle)
    {
        var result = mainResult;

        // Process additional URL list
        var lstUrl = item.MoreUrl.TrimEx().Split(",") ?? [];
        foreach (var it in lstUrl)
        {
            var url2 = Utils.GetPunycode(it);
            if (url2.IsNullOrEmpty())
            {
                continue;
            }

            var (additionalContent, additionalHeaders) = await DownloadSubscriptionContentWithHeaders(downloadHandle, url2, blProxy, item.UserAgent);

            if (additionalContent.IsNotEmpty())
            {
                // Check header for encryption; decrypt only when Subscription-Encryption present
                if (IsAesEncrypted(additionalHeaders))
                {
                    if (item.LoginPassword.IsNullOrEmpty())
                    {
                        return (string.Empty, true);
                    }
                    if (!TryDecryptSubscription(item, additionalContent, out var decrypted))
                    {
                        return (string.Empty, true);
                    }
                    result += Environment.NewLine + decrypted;
                }
                else if (Utils.IsBase64String(additionalContent))
                {
                    result += Environment.NewLine + Utils.Base64Decode(additionalContent);
                }
                else
                {
                    result += Environment.NewLine + additionalContent;
                }
            }
        }

        return (result, false);
    }

    private static bool TryDecryptSubscription(SubItem item, string base64Data, out string decrypted)
    {
        decrypted = string.Empty;
        if (base64Data.IsNullOrEmpty())
        {
            return false;
        }

        var pass = Utils.GetMd5(item.LoginPassword ?? string.Empty);
        if (pass.Length != 32)
        {
            return false;
        }

        try
        {
            var key = Convert.FromHexString(pass);
            var raw = TryBase64DecodeBytes(base64Data);
            if (raw == null || raw.Length <= 16)
            {
                return false;
            }

            var iv = raw[..16];
            var cipher = raw[16..];

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            decrypted = Encoding.UTF8.GetString(plainBytes);
            return decrypted.IsNotEmpty();
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SubscriptionDecryptFailed", ex);
            return false;
        }
    }

    private static byte[]? TryBase64DecodeBytes(string plainText)
    {
        try
        {
            if (plainText.IsNullOrEmpty())
            {
                return null;
            }

            plainText = plainText.Trim()
                .Replace(Environment.NewLine, "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace('_', '/')
                .Replace('-', '+')
                .Replace(" ", "");

            if (plainText.Length % 4 > 0)
            {
                plainText = plainText.PadRight(plainText.Length + 4 - (plainText.Length % 4), '=');
            }

            return Convert.FromBase64String(plainText);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SubscriptionBase64DecodeFailed", ex);
            return null;
        }
    }

    private static async Task<bool> ProcessDownloadResult(Config config, string id, string result, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (result.IsNullOrEmpty())
        {
            await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
            return false;
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");

        // If result is too short, display content directly
        if (result.Length < 99)
        {
            await updateFunc?.Invoke(false, $"{hashCode}{result}");
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartParsingSubscription}");

        // Add servers to configuration
        var ret = await ConfigHandler.AddBatchServers(config, result, id, true);
        if (ret <= 0)
        {
            Logging.SaveLog("FailedImportSubscription");
            Logging.SaveLog(result);
        }

        // Update completion message
        await updateFunc?.Invoke(false, ret > 0
                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");

        return ret > 0;
    }
}
