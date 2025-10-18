using System;
using System.Text.RegularExpressions;
using ServiceLib.Common;
using ServiceLib.Models;

namespace ServiceLib.Manager;

public sealed class SubscriptionInfoManager
{
    private static readonly Lazy<SubscriptionInfoManager> _instance = new(() => new());
    public static SubscriptionInfoManager Instance => _instance.Value;

    private readonly Dictionary<string, SubscriptionUsageInfo> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storeFile = Utils.GetConfigPath("SubUsage.json");
    private readonly object _saveLock = new();
    private readonly object _mapLock = new();

    private SubscriptionInfoManager()
    {
        try
        {
            if (File.Exists(_storeFile))
            {
                var txt = File.ReadAllText(_storeFile);
                var data = JsonUtils.Deserialize<Dictionary<string, SubscriptionUsageInfo>>(txt);
                if (data != null)
                {
                    foreach (var kv in data)
                    {
                        if (kv.Value != null)
                        {
                            _map[kv.Key] = kv.Value;
                        }
                    }
                }
            }
        }
        catch { }
    }

    public SubscriptionUsageInfo? Get(string subId)
    {
        if (subId.IsNullOrEmpty()) return null;
        lock (_mapLock)
        {
            _map.TryGetValue(subId, out var info);
            return info;
        }
    }

    public void Update(string subId, SubscriptionUsageInfo info)
    {
        if (subId.IsNullOrEmpty() || info == null) return;
        info.SubId = subId;
        lock (_mapLock)
        {
            _map[subId] = info;
        }
        AppEvents.SubscriptionInfoUpdated.Publish(info);
        SaveCopy();
    }

    public void Clear(string subId)
    {
        if (subId.IsNullOrEmpty()) return;
        lock (_mapLock)
        {
            _map.Remove(subId);
        }
        SaveCopy();
    }

    // Common subscription header: "Subscription-Userinfo: upload=123; download=456; total=789; expire=1700000000"
    public void UpdateFromHeader(string subId, IEnumerable<string>? headerValues)
    {
        if (headerValues == null) return;
        var raw = headerValues.FirstOrDefault();
        if (raw.IsNullOrEmpty()) return;

        var info = ParseUserinfo(raw);
        if (info != null)
        {
            Update(subId, info);
        }
    }

    private static SubscriptionUsageInfo? ParseUserinfo(string raw)
    {
        try
        {
            var info = new SubscriptionUsageInfo();

            var rx = new Regex(@"(?i)(upload|download|total|expire)\s*=\s*([0-9]+)", RegexOptions.Compiled);
            foreach (Match m in rx.Matches(raw))
            {
                var key = m.Groups[1].Value.ToLowerInvariant();
                if (!long.TryParse(m.Groups[2].Value, out var val)) continue;
                switch (key)
                {
                    case "upload": info.Upload = val; break;
                    case "download": info.Download = val; break;
                    case "total": info.Total = val; break;
                    case "expire": info.ExpireEpoch = val; break;
                }
            }

            if (info.Total == 0 && info.Upload == 0 && info.Download == 0 && info.ExpireEpoch == 0)
            {
                return null;
            }
            return info;
        }
        catch
        {
            return null;
        }
    }

    private void SaveCopy()
    {
        try
        {
            Dictionary<string, SubscriptionUsageInfo> snapshot;
            lock (_mapLock)
            {
                snapshot = new(_map);
            }

            var txt = JsonUtils.Serialize(snapshot, true, true);
            var tmp = _storeFile + ".tmp";

            lock (_saveLock)
            {
                File.WriteAllText(tmp, txt);

                try
                {
                    if (File.Exists(_storeFile))
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            try
                            {
                                File.Replace(tmp, _storeFile, null);
                                return;
                            }
                            catch (IOException)
                            {
                                // Fallback to cross-platform strategy below.
                            }
                            catch (PlatformNotSupportedException)
                            {
                                // Fallback to cross-platform strategy below.
                            }
                        }

                        try
                        {
                            File.Move(tmp, _storeFile, true);
                            return;
                        }
                        catch (IOException)
                        {
                            File.Copy(tmp, _storeFile, true);
                            return;
                        }
                    }
                    else
                    {
                        File.Move(tmp, _storeFile);
                        return;
                    }
                }
                finally
                {
                    if (File.Exists(tmp))
                    {
                        try
                        {
                            File.Delete(tmp);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    public async Task FetchHeadersForAll(Config config)
    {
        try
        {
            if (config == null)
            {
                return;
            }
            var subs = await AppManager.Instance.SubItems();
            if (subs is not { Count: > 0 }) return;
            foreach (var s in subs)
            {
                await FetchHeaderForSub(config, s);
            }
        }
        catch { }
    }

    public async Task FetchHeaderForSub(Config config, SubItem s)
    {
        try
        {
            if (config == null || s == null)
            {
                return;
            }
            var originalUrl = Utils.GetPunycode(s.Url.TrimEx());
            if (originalUrl.IsNullOrEmpty()) return;

            string url = originalUrl;
            if (s.ConvertTarget.IsNotEmpty())
            {
                var subConvertUrl = config.ConstItem.SubConvertUrl.IsNullOrEmpty()
                    ? Global.SubConvertUrls.FirstOrDefault()
                    : config.ConstItem.SubConvertUrl;
                if (subConvertUrl.IsNotEmpty())
                {
                    url = string.Format(subConvertUrl!, Utils.UrlEncode(originalUrl));
                    if (!url.Contains("target=")) url += string.Format("&target={0}", s.ConvertTarget);
                    if (!url.Contains("config=")) url += string.Format("&config={0}", Global.SubConvertConfig.FirstOrDefault());
                }
            }

            var dl = new DownloadService();
            // via proxy then direct
            foreach (var blProxy in new[] { true, false })
            {
                var (userHeader, _) = await dl.TryGetWithHeaders(url, blProxy, s.UserAgent, timeoutSeconds: 8);
                if (userHeader.IsNotEmpty())
                {
                    UpdateFromHeader(s.Id, new[] { userHeader });
                    return;
                }
            }

            // fallback to original url if converted
            if (s.ConvertTarget.IsNotEmpty())
            {
                foreach (var blProxy in new[] { true, false })
                {
                    var (userHeader2, _) = await dl.TryGetWithHeaders(originalUrl, blProxy, s.UserAgent, timeoutSeconds: 6);
                    if (userHeader2.IsNotEmpty())
                    {
                        UpdateFromHeader(s.Id, new[] { userHeader2 });
                        return;
                    }
                }
            }
        }
        catch { }
    }
}
