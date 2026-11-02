namespace ServiceLib.Handler;

public static class SubscriptionHandler
{
    public static async Task<SubscriptionRefreshResult> UpdateProcess(Config config, string subId, bool blProxy, Func<bool, string, Task> updateFunc)
    {
        await updateFunc?.Invoke(false, ResUI.MsgUpdateSubscriptionStart);
        var subItem = await AppManager.Instance.SubItems();

        if (subItem is not { Count: > 0 })
        {
            await updateFunc?.Invoke(false, ResUI.MsgNoValidSubscription);
            return new();
        }

        var updatedSubscriptionIds = new List<string>();
        var unavailableActiveProfileIds = new List<string>();
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
                var generation = SubscriptionImporter.BeginRefresh(item.Id);
                var downloadHandle = CreateDownloadHandler(item, hashCode, updateFunc);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                // Get all subscription content (main subscription + additional subscriptions)
                var result = await DownloadAllSubscriptions(config, item, blProxy, downloadHandle);

                // Process download result
                var import = await ProcessDownloadResult(config, item.Id, result, generation, hashCode, updateFunc);
                if (import.Count > 0)
                {
                    updatedSubscriptionIds.Add(item.Id);
                    if (import.UnavailableActiveProfileId is { } unavailableId)
                    {
                        unavailableActiveProfileIds.Add(unavailableId);
                    }
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

        var refreshResult = new SubscriptionRefreshResult
        {
            UpdatedSubscriptionIds = updatedSubscriptionIds.ToArray(),
            UnavailableActiveProfileIds = unavailableActiveProfileIds.ToArray(),
        };
        await updateFunc?.Invoke(refreshResult.Success, $"{ResUI.MsgUpdateSubscriptionEnd}");
        return refreshResult;
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

    private static DownloadService CreateDownloadHandler(SubItem item, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (!HttpRequestHeadersHelper.TryParse(item.RequestHeaders, out var requestHeaders))
        {
            throw new FormatException(ResUI.SubRequestHeadersInvalid);
        }

        var downloadHandle = new DownloadService
        {
            AcceptHeader = "*/*",
            RequestHeaders = requestHeaders
        };
        downloadHandle.Error += (sender2, args) =>
        {
            updateFunc?.Invoke(false, $"{hashCode}{args.GetException().Message}");
        };
        return downloadHandle;
    }

    private static async Task<string> DownloadSubscriptionContent(DownloadService downloadHandle, string url, bool blProxy, string userAgent)
    {
        var result = await downloadHandle.TryDownloadString(url, blProxy, userAgent);

        // If download with proxy fails, try direct connection
        if (blProxy && result.IsNullOrEmpty())
        {
            result = await downloadHandle.TryDownloadString(url, false, userAgent);
        }

        return result ?? string.Empty;
    }

    private static async Task<IReadOnlyList<string>> DownloadAllSubscriptions(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        // Download main subscription content
        var result = await DownloadMainSubscription(config, item, blProxy, downloadHandle);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new FormatException(ResUI.MsgSubscriptionDecodingFailed);
        }
        var sources = new List<string> { result };
        if (item.ConvertTarget.IsNotEmpty())
        {
            return sources;
        }

        // Process additional subscription links (if any)
        foreach (var additionalUrl in item.MoreUrl.TrimEx().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var url = Utils.GetPunycode(additionalUrl);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                throw new FormatException(ResUI.MsgSubscriptionDecodingFailed);
            }
            var additional = await DownloadSubscriptionContent(downloadHandle, url, blProxy, item.UserAgent);
            if (string.IsNullOrWhiteSpace(additional))
            {
                throw new FormatException(ResUI.MsgSubscriptionDecodingFailed);
            }
            sources.Add(additional);
        }

        return sources;
    }

    private static async Task<string> DownloadMainSubscription(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
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
                url += $"&target={item.ConvertTarget}";
            }

            if (!url.Contains("config="))
            {
                url += $"&config={Global.SubConvertConfig.FirstOrDefault()}";
            }
        }

        // Download and return result directly
        return await DownloadSubscriptionContent(downloadHandle, url, blProxy, item.UserAgent);
    }

    private static async Task<SubscriptionImporter.ImportResult> ProcessDownloadResult(Config config, string id, IReadOnlyList<string> sources, long generation, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (sources.Count == 0 || sources.Any(string.IsNullOrWhiteSpace))
        {
            await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
            return new(-1);
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");

        // If result is too short, display content directly
        foreach (var result in sources.Where(result => result.Length < 99))
        {
            await updateFunc?.Invoke(false, $"{hashCode}{result}");
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartParsingSubscription}");

        // Add servers to configuration
        var ret = await SubscriptionImporter.Import(config, sources, id, generation);
        if (ret.Count <= 0)
        {
            Logging.SaveLog("FailedImportSubscription");
            foreach (var source in sources)
            {
                Logging.SaveLog(source);
            }
        }

        // Update completion message
        await updateFunc?.Invoke(false, ret.Count > 0
                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");

        return ret;
    }
}
