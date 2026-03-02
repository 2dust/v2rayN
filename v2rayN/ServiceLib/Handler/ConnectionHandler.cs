namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";
    private static readonly string[] _speedtestIpApiUrls =
    [
        "https://api.ipapi.is",
        "https://api.ip.sb/geoip"
    ];

    public static async Task<string> RunAvailabilityCheck()
    {
        var time = await GetRealPingTimeInfo();
        var ip = time > 0 ? await GetIPInfo() ?? Global.None : Global.None;

        return string.Format(ResUI.TestMeOutput, time, ip);
    }

    private static async Task<string?> GetIPInfo()
    {
        var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        var webProxy = new WebProxy($"socks5://{Global.Loopback}:{port}");
        var ipInfo = await GetIpApiInfo(url, webProxy, 10);
        return FormatCountryAndIp(ipInfo, false);
    }

    public static async Task<string?> GetCountryCodeAndIP(IWebProxy? webProxy, int downloadTimeout = 10)
    {
        foreach (var url in _speedtestIpApiUrls)
        {
            var ipInfo = await GetIpApiInfo(url, webProxy, downloadTimeout);
            var compact = FormatCountryAndIp(ipInfo, true);
            if (compact.IsNotEmpty())
            {
                return compact;
            }
        }

        return null;
    }

    private static async Task<IPAPIInfo?> GetIpApiInfo(string url, IWebProxy? webProxy, int downloadTimeout)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(downloadTimeout));
            using var client = new HttpClient(new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null
            });
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(Utils.GetVersion(false));

            using var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            return JsonUtils.Deserialize<IPAPIInfo>(result);
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatCountryAndIp(IPAPIInfo? ipInfo, bool compact)
    {
        if (ipInfo == null)
        {
            return null;
        }

        var ip = (ipInfo.ip ?? ipInfo.clientIp ?? ipInfo.ip_addr ?? ipInfo.query)?.Trim();
        if (ip.IsNullOrEmpty())
        {
            return null;
        }

        var country = (ipInfo.country_code ?? ipInfo.countryCode ?? ipInfo.location?.country_code ?? ipInfo.country)?.Trim();
        if (country.IsNullOrEmpty())
        {
            country = "unknown";
        }
        else
        {
            country = country.ToUpperInvariant();
        }

        return compact ? $"{country}{ip}" : $"({country}) {ip}";
    }

    private static async Task<int> GetRealPingTimeInfo()
    {
        var responseTime = -1;
        try
        {
            var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            var webProxy = new WebProxy($"socks5://{Global.Loopback}:{port}");
            var url = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;

            for (var i = 0; i < 2; i++)
            {
                responseTime = await GetRealPingTime(url, webProxy, 10);
                if (responseTime > 0)
                {
                    break;
                }
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }
        return responseTime;
    }

    public static async Task<int> GetRealPingTime(string url, IWebProxy? webProxy, int downloadTimeout)
    {
        var responseTime = -1;
        try
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(downloadTimeout));
            using var client = new HttpClient(new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null
            });

            List<int> oneTime = new();
            for (var i = 0; i < 2; i++)
            {
                var timer = Stopwatch.StartNew();
                await client.GetAsync(url, cts.Token).ConfigureAwait(false);
                timer.Stop();
                oneTime.Add((int)timer.Elapsed.TotalMilliseconds);
                await Task.Delay(100);
            }
            responseTime = oneTime.Where(x => x > 0).OrderBy(x => x).FirstOrDefault();
        }
        catch
        {
        }
        return responseTime;
    }
}
