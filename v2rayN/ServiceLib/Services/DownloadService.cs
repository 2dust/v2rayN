namespace ServiceLib.Services;

/// <summary>
///     Download
/// </summary>
public class DownloadService : IDisposable
{
    private static readonly string _tag = "DownloadService";

    public HttpMessageHandler Handler { get; init; } = HttpClientHelper.Instance.DirectHandler;

    public bool DisposeHandler { get; init; } = false;

    public DownloaderHelper DownloaderHelper
    {
        get
        {
            field ??= new DownloaderHelper
            {
                Handler = Handler,
                DisposeHandler = false,
            };
            return field;
        }
    }

    public event EventHandler<UpdateResult>? UpdateCompleted;

    public event ErrorEventHandler? Error;

    public static async Task<DownloadService> CreateDownloadServiceWithCheck(bool preferProxy = true)
    {
        var webProxy = await CheckProxy(preferProxy);
        var cookieContainer = new CookieContainer();
        if (webProxy is not null)
        {
            return new DownloadService
            {
                Handler = HttpClientHelper.CreateProxySocketsHttpHandler(webProxy, cookieContainer),
                DisposeHandler = true,
            };
        }
        return new DownloadService
        {
            Handler = HttpClientHelper.CreateSocketsHttpHandler(false, cookieContainer),
            DisposeHandler = true,
        };
    }

    public static DownloadService CreateDownloadServiceWithFallback(bool preferProxy = true)
    {
        return new DownloadService
        {
            Handler = HttpClientHelper.Instance.CreateFallbackHandler(preferProxy, new()),
            DisposeHandler = true,
        };
    }

    /// <summary>
    ///     Downloads data with the specified proxy and reports progress messages.
    /// </summary>
    public async Task<int> DownloadDataAsync(string url, Func<bool, string, Task> updateFunc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await DownloaderHelper.DownloadDataAsync4Speed(
                url,
                OnProgress,
                cancellationToken);

            void OnProgress(string message)
            {
                cancellationToken.ThrowIfCancellationRequested();
                updateFunc.Invoke(false, $"{message}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await updateFunc.Invoke(false, ex.Message);
            if (ex.InnerException != null)
            {
                await updateFunc.Invoke(false, ex.InnerException.Message);
            }
        }
        return 0;
    }

    /// <summary>
    ///     Downloads a file and reports progress through events.
    /// </summary>
    public async Task DownloadFileAsync(FileDownloadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateCompleted?.Invoke(this, new UpdateResult(false, $"{ResUI.Downloading}   {request.FileUrl}"));

            await DownloaderHelper.DownloadFileAsync(
                request,
                OnProgress,
                cancellationToken);

            void OnProgress(FileDownloadState state)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateCompleted?.Invoke(this,
                    new UpdateResult(state.Completed,
                        $"{Utils.HumanFy((long)state.SpeedBytesPerSecond / 1024)}/s | {Utils.HumanFy(state.DownloadedBytes / 1024)}/{Utils.HumanFy(state.TotalBytes / 1024)}"));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);

            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
    }

    public async Task DownloadSmallFilesAsync(List<FileDownloadRequest> requests,
        CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateCompleted?.Invoke(this, new UpdateResult(false, $"{ResUI.Downloading} 0/{requests.Count}"));

            await DownloaderHelper.DownloadSmallFilesAsync(
                requests,
                OnProgress,
                cancellationToken);

            void OnProgress(ReadOnlyMemory<FileDownloadState> states)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var span = states.Span;
                var completedCount = 0;
                var downloadingStates = new List<FileDownloadState>();
                foreach (ref readonly var item in span)
                {
                    if (item.Completed)
                    {
                        completedCount++;
                    }
                    else if (item.TotalBytes > 0)
                    {
                        downloadingStates.Add(item);
                    }
                }
                var totalSpeed = downloadingStates.Sum(x => x.SpeedBytesPerSecond);
                var totalDownloadedBytes = downloadingStates.Sum(x => x.DownloadedBytes);
                var totalTotalBytes = downloadingStates.Sum(x => x.TotalBytes);
                var downloadingFileName = string.Join(", ", downloadingStates.Select(x => x.Request.FileName));
                var allCompleted = completedCount == span.Length;
                if (allCompleted)
                {
                    // check and throw errors if any
                    FileDownloadState? failedState = null;
                    foreach (ref readonly var item in span)
                    {
                        if (!item.IsFailed)
                        {
                            continue;
                        }
                        failedState = item;
                        break;
                    }
                    if (failedState?.Error != null)
                    {
                        throw failedState.Error;
                    }
                }
                UpdateCompleted?.Invoke(this,
                    new UpdateResult(allCompleted,
                        $"{completedCount}/{span.Length} | {Utils.HumanFy((long)totalSpeed / 1024)}/s {Utils.HumanFy(totalDownloadedBytes / 1024)}/{Utils.HumanFy(totalTotalBytes / 1024)} {downloadingFileName}"));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);

            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
    }

    /// <summary>
    ///     Gets redirect target URL without following redirects automatically.
    /// </summary>
    public async Task<string?> UrlRedirectAsync(string url, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(GetHandler(), false);

        var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is not null)
        {
            return response.Headers.Location.ToString();
        }
        Error?.Invoke(this, new ErrorEventArgs(new Exception("StatusCode error: " + response.StatusCode)));
        Logging.SaveLog("StatusCode error: " + url);
        return null;
    }

    /// <summary>
    ///     Tries to download string content with a specified proxy.
    /// </summary>
    public async Task<string?> TryDownloadString(string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result1 = await DownloadStringAsync(url, cancellationToken);
            if (result1.IsNotEmpty())
            {
                return result1;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        try
        {
            var result2 = await DownloadStringViaDownloader(url, cancellationToken);
            if (result2.IsNotEmpty())
            {
                return result2;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        return null;
    }

    /// <summary>
    ///     Downloads string content via HttpClient.
    /// </summary>
    private async Task<string?> DownloadStringAsync(string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient(GetHandler(), false);
            client.Timeout = Timeout.InfiniteTimeSpan;

            var uri = new Uri(url);
            if (uri.UserInfo.IsNotEmpty())
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
            }

            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(Global.ProxyFetch);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            return await client.GetStringAsync(url, linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        return null;
    }

    /// <summary>
    ///     Downloads string content via DownloaderHelper.
    /// </summary>
    private async Task<string?> DownloadStringViaDownloader(string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result =
                await DownloaderHelper.DownloadStringAsync(url,
                    cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
        return null;
    }

    /// <summary>
    ///     Creates local SOCKS proxy when proxy switch is enabled.
    /// </summary>
    public static async Task<WebProxy?> CheckProxy(bool blProxy, CancellationToken cancellationToken = default)
    {
        if (!blProxy)
        {
            return null;
        }
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        if (!await SocksPortCheck(Global.Loopback, port, cancellationToken))
        {
            return null;
        }

        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    /// <summary>
    ///     Checks whether the specified TCP endpoint is reachable.
    /// </summary>
    private static async Task<bool> SocksPortCheck(string ip, int port, CancellationToken cancellationToken = default)
    {
        using var rootTimeOutCts = new CancellationTokenSource(Global.LocalFetch);
        using var rootCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, rootTimeOutCts.Token);
        var rootToken = rootCts.Token;

        // SOCKS5 client greeting: VER=5, NMETHODS=1, METHOD=0x00 (no auth)
        ReadOnlyMemory<byte> greeting = new byte[] { 0x05, 0x01, 0x00 };
        var buf = new byte[2];

        while (!rootToken.IsCancellationRequested)
        {
            using var tcp = new TcpClient();
            using var attemptCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(rootToken, attemptCts.Token);
            var linkedToken = linkedCts.Token;
            try
            {
                await tcp.ConnectAsync(ip, port, linkedToken);
                var stream = tcp.GetStream();

                await stream.WriteAsync(greeting, linkedToken);

                var read = await stream.ReadAsync(buf.AsMemory(0, 2), linkedToken);

                // Server selection: VER=5, METHOD=0x00 — proxy is fully ready
                if (read == 2 && buf[0] == 0x05)
                {
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                if (!rootToken.IsCancellationRequested)
                {
                    continue;
                }
                Logging.SaveLog($"SocksPortCheck Timeout waiting for proxy port {port} to be ready.");
                return false;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                // Connection refused, proxy not ready yet, wait 50ms before retrying
                try
                {
                    await Task.Delay(50, rootToken);
                }
                catch (OperationCanceledException)
                {
                    Logging.SaveLog($"SocksPortCheck Timeout waiting for proxy port {port} to be ready.");
                    return false;
                }
            }
            catch
            {
                // Ignore other exceptions and continue
            }
        }
        return false;
    }

    private HttpMessageHandler GetHandler()
    {
        return Handler;
    }

    public void Dispose()
    {
        if (!DisposeHandler)
        {
            return;
        }
        if (Handler is IDisposable disposableHandler)
        {
            disposableHandler.Dispose();
        }
    }
}
