namespace ServiceLib.Handler;

public class SubscriptionHandler
{
    public static async Task UpdateProcess(Config config, string subId, bool blProxy, Action<bool, string> updateFunc)
    {
        updateFunc?.Invoke(false, ResUI.MsgUpdateSubscriptionStart);
        var subItem = await AppHandler.Instance.SubItems();

        if (subItem is not { Count: > 0 })
        {
            updateFunc?.Invoke(false, ResUI.MsgNoValidSubscription);
            return;
        }

        foreach (var item in subItem)
        {
            var id = item.Id.TrimEx();
            var url = item.Url.TrimEx();
            var userAgent = item.UserAgent.TrimEx();
            var hashCode = $"{item.Remarks}->";
            if (id.IsNullOrEmpty() || url.IsNullOrEmpty() || (subId.IsNotEmpty() && item.Id != subId))
            {
                //_updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgNoValidSubscription}");
                continue;
            }
            if (!url.StartsWith(Global.HttpsProtocol) && !url.StartsWith(Global.HttpProtocol))
            {
                continue;
            }
            if (item.Enabled == false)
            {
                updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                continue;
            }

            var downloadHandle = new DownloadService();
            downloadHandle.Error += (sender2, args) =>
            {
                updateFunc?.Invoke(false, $"{hashCode}{args.GetException().Message}");
            };

            updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

            //one url
            url = Utils.GetPunycode(url);
            //convert
            if (item.ConvertTarget.IsNotEmpty())
            {
                var subConvertUrl = config.ConstItem.SubConvertUrl.IsNullOrEmpty() ? Global.SubConvertUrls.FirstOrDefault() : config.ConstItem.SubConvertUrl;
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
            var result = await downloadHandle.TryDownloadString(url, blProxy, userAgent);
            if (blProxy && result.IsNullOrEmpty())
            {
                result = await downloadHandle.TryDownloadString(url, false, userAgent);
            }

            //more url
            if (item.ConvertTarget.IsNullOrEmpty() && item.MoreUrl.TrimEx().IsNotEmpty())
            {
                if (result.IsNotEmpty() && Utils.IsBase64String(result))
                {
                    result = Utils.Base64Decode(result);
                }

                var lstUrl = item.MoreUrl.TrimEx().Split(",") ?? [];
                foreach (var it in lstUrl)
                {
                    var url2 = Utils.GetPunycode(it);
                    if (url2.IsNullOrEmpty())
                    {
                        continue;
                    }

                    var result2 = await downloadHandle.TryDownloadString(url2, blProxy, userAgent);
                    if (blProxy && result2.IsNullOrEmpty())
                    {
                        result2 = await downloadHandle.TryDownloadString(url2, false, userAgent);
                    }
                    if (result2.IsNotEmpty())
                    {
                        if (Utils.IsBase64String(result2))
                        {
                            result += Environment.NewLine + Utils.Base64Decode(result2);
                        }
                        else
                        {
                            result += Environment.NewLine + result2;
                        }
                    }
                }
            }

            if (result.IsNullOrEmpty())
            {
                updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
            }
            else
            {
                updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");
                if (result?.Length < 99)
                {
                    updateFunc?.Invoke(false, $"{hashCode}{result}");
                }

                var ret = await ConfigHandler.AddBatchServers(config, result, id, true);
                if (ret <= 0)
                {
                    Logging.SaveLog("FailedImportSubscription");
                    Logging.SaveLog(result);
                }
                updateFunc?.Invoke(false,
                    ret > 0
                        ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                        : $"{hashCode}{ResUI.MsgFailedImportSubscription}");
            }
            updateFunc?.Invoke(false, "-------------------------------------------------------");

            //await ConfigHandler.DedupServerList(config, id);
        }

        updateFunc?.Invoke(true, $"{ResUI.MsgUpdateSubscriptionEnd}");
    }
}
