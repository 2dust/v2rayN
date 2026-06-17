namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";

    /// <summary>
    /// Runs ping and IP checks and returns a formatted result string.
    /// </summary>
    public static async Task<string> RunAvailabilityCheck()
    {
        return (await RunAvailabilityCheckResult()).ToString();
    }

    /// <summary>
    /// Runs ping and IP checks and returns a structured result.
    /// </summary>
    public static async Task<AvailabilityCheckResult> RunAvailabilityCheckResult()
    {
        var time = await GetRealPingTimeInfo();
        var ip = time > 0 ? await GetIPInfo() : Global.None;

        return new(time, ip);
    }

    /// <summary>
    /// Gets IP information using the default local proxy.
    /// </summary>
    private static async Task<string?> GetIPInfo()
    {
        var webProxy = await GetWebProxy();

        var ipInfo = await GetIPInfo(webProxy);
        return ipInfo?.ToString() ?? Global.None;
    }

    /// <summary>
    /// Measures real ping time using configured test URL.
    /// </summary>
    private static async Task<int> GetRealPingTimeInfo()
    {
        var responseTime = -1;
        try
        {
            var webProxy = await GetWebProxy();

            for (var i = 0; i < 2; i++)
            {
                responseTime = await GetRealPingTime(webProxy, 10);
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

    /// <summary>
    /// Creates local SOCKS proxy instance.
    /// </summary>
    private static async Task<WebProxy?> GetWebProxy()
    {
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    /// <summary>
    /// Measures response time by sending HTTP requests through proxy.
    /// </summary>
    public static async Task<int> GetRealPingTime(IWebProxy? webProxy, int downloadTimeout)
    {
        var url = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
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

            List<int> oneTime = [];
            for (var i = 0; i < 2; i++)
            {
                var timer = Stopwatch.StartNew();
                await client.GetAsync(url, cts.Token).ConfigureAwait(false);
                timer.Stop();
                oneTime.Add((int)timer.Elapsed.TotalMilliseconds);
                await Task.Delay(100, cts.Token);
            }
            responseTime = oneTime.Where(x => x > 0).OrderBy(x => x).FirstOrDefault();
        }
        catch
        {
        }
        return responseTime;
    }

    /// <summary>
    /// Gets IP and geolocation information through specified proxy.
    /// </summary>
    public static async Task<IpInfoResult?> GetIPInfo(IWebProxy? webProxy)
    {
        try
        {
            var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, webProxy, "");
            if (result == null)
            {
                return null;
            }

            return ParseIPInfo(result);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets IP and geolocation information for a server address.
    /// </summary>
    public static async Task<IpInfoResult?> GetIPInfoForAddress(string? address, bool blProxy)
    {
        try
        {
            var ip = await ResolveAddressForIPInfo(address);
            if (ip.IsNullOrEmpty())
            {
                return null;
            }

            var url = BuildIPInfoUrlForAddress(AppManager.Instance.Config.SpeedTestItem.IPAPIUrl, ip);
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, blProxy, Global.AppName);
            if (result == null)
            {
                return null;
            }

            return ParseIPInfo(result);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds an IP API URL that targets a specific server IP.
    /// </summary>
    public static string? BuildIPInfoUrlForAddress(string? apiUrl, string? ip)
    {
        if (apiUrl.IsNullOrEmpty() || ip.IsNullOrEmpty())
        {
            return null;
        }

        var escapedIp = Uri.EscapeDataString(ip.TrimEx());
        if (apiUrl.Contains("{0}", StringComparison.Ordinal))
        {
            return string.Format(apiUrl, escapedIp);
        }
        if (apiUrl.Contains("{ip}", StringComparison.OrdinalIgnoreCase))
        {
            return apiUrl.Replace("{ip}", escapedIp, StringComparison.OrdinalIgnoreCase);
        }

        var normalizedUrl = apiUrl.TrimEx().TrimEnd('/');
        if (normalizedUrl.Contains("api.ipapi.is", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedUrl.Contains('?', StringComparison.Ordinal)
                ? $"{normalizedUrl}&q={escapedIp}"
                : $"{normalizedUrl}/?q={escapedIp}";
        }

        return $"{normalizedUrl}/{escapedIp}";
    }

    /// <summary>
    /// Resolves a server address to a public IP address for geolocation lookup.
    /// </summary>
    public static async Task<string?> ResolveAddressForIPInfo(string? address)
    {
        var host = NormalizeAddressForIPInfo(address);
        if (host.IsNullOrEmpty())
        {
            return null;
        }

        if (IPAddress.TryParse(host, out var parsedAddress))
        {
            return Utils.IsPrivateNetwork(parsedAddress.ToString()) ? null : parsedAddress.ToString();
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            return addresses
                .Select(t => t.ToString())
                .FirstOrDefault(t => !Utils.IsPrivateNetwork(t));
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeAddressForIPInfo(string? address)
    {
        var host = address?.Trim();
        if (host.IsNullOrEmpty())
        {
            return null;
        }

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }

        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            host = host[1..^1];
        }

        return host;
    }

    /// <summary>
    /// Parses IP API responses from supported providers.
    /// </summary>
    public static IpInfoResult? ParseIPInfo(string? result)
    {
        var ipInfo = JsonUtils.Deserialize<IPAPIInfo>(result);
        if (ipInfo == null)
        {
            return null;
        }

        var ip = FirstNonEmpty(ipInfo.ip, ipInfo.clientIp, ipInfo.ip_addr, ipInfo.query);
        var country = FirstNonEmpty(
            ipInfo.country_code,
            ipInfo.countryCode,
            ipInfo.location?.country_code,
            ipInfo.country,
            ipInfo.country_name,
            ipInfo.location?.country) ?? "unknown";
        var region = FirstNonEmpty(
            ipInfo.region,
            ipInfo.regionName,
            ipInfo.region_name,
            ipInfo.location?.state,
            ipInfo.location?.region,
            ipInfo.location?.region_name);
        var city = FirstNonEmpty(ipInfo.city, ipInfo.location?.city);

        return new IpInfoResult(country, ip, region, city);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.Select(t => t?.Trim()).FirstOrDefault(t => t.IsNotEmpty());
    }
}
