namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";

    /// <summary>
    /// Runs ping and IP checks.
    /// </summary>
    public static async Task<AvailabilityCheckResult> RunAvailabilityCheck()
    {
        var time = await GetRealPingTimeInfo();
        var ip = time > 0 ? await GetIPInfo() : Global.None;

        return new AvailabilityCheckResult(time, ip);
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
                responseTime = await GetRealPingTime(webProxy);
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
    public static async Task<int> GetRealPingTime(IWebProxy? webProxy, CancellationToken cancellationToken = default)
    {
        var result = await GetRealPingResult(webProxy, null, cancellationToken);
        return result.Delay;
    }

    /// <summary>
    /// Measures response time by sending HTTP requests through proxy. When the test url
    /// is a Cloudflare trace endpoint, the exit ip and country are read from that same
    /// response, so no additional request is needed.
    /// </summary>
    public static async Task<RealPingResult> GetRealPingResult(IWebProxy? webProxy, string? url = null, CancellationToken cancellationToken = default)
    {
        url = url.IsNotEmpty() ? url : AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
        var isTrace = IsCloudflareTraceUrl(url);
        var delay = 0;
        var completed = false;
        string? traceContent = null;
        try
        {
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(Global.LocalFetch);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var linkedToken = linkedCts.Token;
            using var client = new HttpClient(new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null,
                ConnectTimeout = Global.LocalFetch,
            });

            for (var i = 0; i < 2; i++)
            {
                var timer = Stopwatch.StartNew();
                var response = await client.GetAsync(url, linkedToken).ConfigureAwait(false);
                var content = isTrace ? await response.Content.ReadAsStringAsync(linkedToken).ConfigureAwait(false) : null;
                timer.Stop();
                completed = true;
                var elapsed = (int)timer.Elapsed.TotalMilliseconds;
                if (elapsed > 0 && (delay <= 0 || elapsed < delay))
                {
                    delay = elapsed;
                    traceContent = content;
                }
                await Task.Delay(100, linkedToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore
        }
        return new RealPingResult(completed ? delay : -1, ParseCloudflareTrace(traceContent));
    }

    /// <summary>
    /// Checks whether the url points to the Cloudflare trace endpoint.
    /// </summary>
    private static bool IsCloudflareTraceUrl(string? url)
    {
        if (url.IsNullOrEmpty() || Uri.TryCreate(url, UriKind.Absolute, out var uri) == false)
        {
            return false;
        }
        return uri.Host is "1.1.1.1" or "1.0.0.1"
            && uri.AbsolutePath.TrimEnd('/').Equals("/cdn-cgi/trace", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the key=value lines of a Cloudflare trace response.
    /// </summary>
    private static IpInfoResult? ParseCloudflareTrace(string? content)
    {
        if (content.IsNullOrEmpty())
        {
            return null;
        }

        string? ip = null;
        string? country = null;
        foreach (var line in content.Split('\n'))
        {
            var keyValue = line.Split('=', 2);
            if (keyValue.Length != 2)
            {
                continue;
            }
            switch (keyValue[0].Trim())
            {
                case "ip":
                    ip = keyValue[1].Trim();
                    break;

                case "loc":
                    country = keyValue[1].Trim();
                    break;
            }
        }

        if (ip.IsNullOrEmpty())
        {
            return null;
        }
        return new IpInfoResult(country.IsNullOrEmpty() ? Global.None : country, ip);
    }

    /// <summary>
    /// Gets IP and country information through specified proxy.
    /// </summary>
    public static async Task<IpInfoResult?> GetIPInfo(IWebProxy? webProxy, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, webProxy, "", cancellationToken);
            if (result == null)
            {
                return null;
            }

            var ipInfo = JsonUtils.Deserialize<IPAPIInfo>(result);
            if (ipInfo == null)
            {
                return null;
            }

            var ip = ipInfo.ip ?? ipInfo.clientIp ?? ipInfo.ip_addr ?? ipInfo.query;
            var country = ipInfo.country_code ?? ipInfo.country ?? ipInfo.countryCode ?? ipInfo.location?.country_code ?? "unknown";

            return new IpInfoResult(country, ip);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
