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
        using var downloadHandle = new DownloadService
        {
            Handler = HttpClientHelper.CreateSocketsHttpHandler(true, new()),
            DisposeHandler = true,
        };
        var ipInfo = await GetIPInfo(downloadHandle);
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
            for (var i = 0; i < 2; i++)
            {
                responseTime = await GetRealPingTime(true);
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
    /// Measures response time by sending HTTP requests through proxy.
    /// </summary>
    public static async Task<int> GetRealPingTime(bool useProxy, CancellationToken cancellationToken = default)
    {
        var responseTime = -1;
        try
        {
            using var client = HttpClientHelper.Instance.GetHttpClient(useProxy);
            responseTime = await GetRealPingTime(client, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore
        }
        return responseTime;
    }

    public static async Task<int> GetRealPingTime(HttpClient client, CancellationToken cancellationToken = default)
    {
        var url = ConfigTextManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
        var responseTime = -1;
        try
        {
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(Global.LocalFetch);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var linkedToken = linkedCts.Token;

            List<int> oneTime = [];
            for (var i = 0; i < 2; i++)
            {
                var timer = Stopwatch.StartNew();
                await client.GetAsync(url, linkedToken).ConfigureAwait(false);
                timer.Stop();
                oneTime.Add((int)timer.Elapsed.TotalMilliseconds);
                await Task.Delay(100, linkedToken);
            }
            responseTime = oneTime.Where(x => x > 0).OrderBy(x => x).FirstOrDefault();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore
        }
        return responseTime;
    }

    public static async Task<IpInfoResult?> GetIPInfo(DownloadService downloadHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = ConfigTextManager.Instance.Config.SpeedTestItem.IPAPIUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var result = await downloadHandle.TryDownloadString(url, cancellationToken);
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
