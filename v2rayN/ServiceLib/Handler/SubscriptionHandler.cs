

namespace ServiceLib.Handler;

public class SubscriptionHandler(
    Config config,
    bool blProxy,
    Func<bool, string, Task> updateFunc)
{
    private readonly Config _config = config;
    private readonly bool _blProxy = blProxy;

    public async Task UpdateProcess(string subId)
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
                if (!item.Enabled)
                {
                    await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                    continue;
                }

                using var processor = new SubscriptionHandlerProcessor(this, item);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                // Get all subscription content (main subscription + additional subscriptions)
                var result = await processor.DownloadAllSubscriptions();

                // Process download result
                if (await ProcessDownloadResult(item.Id, result, hashCode))
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

    private bool IsValidSubscription(SubItem item, string subId)
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

    private async Task<bool> ProcessDownloadResult(string id, string result, string hashCode)
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
        var ret = await ConfigDbManager.AddBatchServers(_config, result, id, true);
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

    private class SubscriptionHandlerProcessor : IDisposable
    {
        private readonly DownloadService[] _downloadServices;
        private readonly SubscriptionHandler _handler;
        private readonly SubItem _item;

        public SubscriptionHandlerProcessor(SubscriptionHandler handler, SubItem item)
        {
            _handler = handler;
            _item = item;
            _downloadServices = CreateDownloadHandler();
        }

        private DownloadService[] CreateDownloadHandler()
        {
            if (!HttpRequestHeadersHelper.TryParse(_item.RequestHeaders, out var requestHeaders))
            {
                throw new FormatException(ResUI.SubRequestHeadersInvalid);
            }

            var userAgent = _item.UserAgent.IsNullOrEmpty() ? Utils.GetVersion(false) : _item.UserAgent;
            //var proxyDownloadService = new DownloadService
            //{
            //    GetHandlerFunc = () =>
            //        HttpRequestHeadersHelper.CreateHandler(HttpClientHelper.Instance.ProxyHandler, userAgent, "*/*",
            //            requestHeaders),
            //};
            //var directDownloadService = new DownloadService
            //{
            //    GetHandlerFunc = () =>
            //        HttpRequestHeadersHelper.CreateHandler(HttpClientHelper.Instance.DirectHandler, userAgent, "*/*",
            //            requestHeaders),
            //};
            //return _handler._blProxy
            //    ? [
            //        proxyDownloadService,
            //    directDownloadService,
            //    ]
            //    : [
            //        directDownloadService,
            //    proxyDownloadService,
            //    ];
            var handler = HttpClientHelper.Instance.CreateFallbackHandler(_handler._blProxy, new());
            var headersHandler = HttpRequestHeadersHelper.CreateHandler(handler, userAgent, "*/*", requestHeaders);
            return
            [
                new DownloadService
                {
                    Handler = headersHandler,
                    DisposeHandler = true,
                },
            ];
        }

        private async Task<string> DownloadSubscriptionContent(string url)
        {
            //var result = await downloadServices[0].TryDownloadString(url);

            //// try other ways
            //if (result.IsNullOrEmpty())
            //{
            //    result = await downloadServices[1].TryDownloadString(url);
            //}
            foreach (var downloadService in _downloadServices)
            {
                var result = await downloadService.TryDownloadString(url);
                if (!result.IsNullOrEmpty())
                {
                    return result;
                }
            }

            return string.Empty;
        }

        public async Task<string> DownloadAllSubscriptions()
        {
            // Download main subscription content
            var result = await DownloadMainSubscription();

            // Process additional subscription links (if any)
            if (_item.ConvertTarget.IsNullOrEmpty() && _item.MoreUrl.TrimEx().IsNotEmpty())
            {
                result = await DownloadAdditionalSubscriptions(result);
            }

            return result;
        }

        private async Task<string> DownloadMainSubscription()
        {
            // Prepare subscription URL and download directly
            var url = Utils.GetPunycode(_item.Url.TrimEx());

            // If conversion is needed
            if (_item.ConvertTarget.IsNotEmpty())
            {
                var subConvertUrl = _handler._config.ConstItem.SubConvertUrl.IsNullOrEmpty()
                    ? Global.SubConvertUrls.FirstOrDefault()
                    : _handler._config.ConstItem.SubConvertUrl;

                url = string.Format(subConvertUrl!, Utils.UrlEncode(url));

                if (!url.Contains("target="))
                {
                    url += $"&target={_item.ConvertTarget}";
                }

                if (!url.Contains("config="))
                {
                    url += $"&config={Global.SubConvertConfig.FirstOrDefault()}";
                }
            }

            // Download and return result directly
            return await DownloadSubscriptionContent(url);
        }

        private async Task<string> DownloadAdditionalSubscriptions(string mainResult)
        {
            var result = mainResult;

            // If main subscription result is Base64 encoded, decode it first
            if (result.IsNotEmpty() && Utils.IsBase64String(result))
            {
                result = Utils.Base64Decode(result);
            }

            // Process additional URL list
            var lstUrl = _item.MoreUrl.TrimEx().Split(",") ?? [];
            foreach (var it in lstUrl)
            {
                var url2 = Utils.GetPunycode(it);
                if (url2.IsNullOrEmpty())
                {
                    continue;
                }

                var additionalResult = await DownloadSubscriptionContent(url2);

                if (additionalResult.IsNotEmpty())
                {
                    // Process additional subscription results, add to main result
                    if (Utils.IsBase64String(additionalResult))
                    {
                        result += Environment.NewLine + Utils.Base64Decode(additionalResult);
                    }
                    else
                    {
                        result += Environment.NewLine + additionalResult;
                    }
                }
            }

            return result;
        }

        public void Dispose()
        {
            foreach (var downloadService in _downloadServices)
            {
                downloadService.Dispose();
            }
        }
    }
}
