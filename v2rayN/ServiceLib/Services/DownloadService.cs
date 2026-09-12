using System.Net.Http.Headers;

namespace ServiceLib.Services;

/// <summary>
/// Download
/// </summary>
public class DownloadService
{
    public event EventHandler<UpdateResult>? UpdateCompleted;

    public event ErrorEventHandler? Error;

    public string? AcceptHeader { get; init; }

    public IReadOnlyDictionary<string, string>? RequestHeaders { get; init; }

    private static readonly string _tag = "DownloadService";

    /// <summary>
    /// Downloads data with the specified proxy and reports progress messages.
    /// </summary>
    public async Task<int> DownloadDataAsync(string url, IWebProxy webProxy, Func<bool, string, Task> updateFunc, CancellationToken cancellationToken = default)
    {
        try
        {
            await DownloaderHelper.Instance.DownloadDataAsync4Speed(webProxy,
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
    /// Downloads a file and reports progress through events.
    /// </summary>
    public async Task DownloadFileAsync(FileDownloadRequest request, bool blProxy, CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateCompleted?.Invoke(this, new UpdateResult(false, $"{ResUI.Downloading}   {request.FileUrl}"));

            var webProxy = await GetWebProxy(blProxy, cancellationToken);
            await DownloaderHelper.Instance.DownloadFileAsync(webProxy,
                request,
                OnProgress,
                cancellationToken);

            void OnProgress(FileDownloadState state)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateCompleted?.Invoke(this, new UpdateResult(state.Completed, $"{Utils.HumanFy((long)state.SpeedBytesPerSecond / 1024)}/s | {Utils.HumanFy(state.DownloadedBytes / 1024)}/{Utils.HumanFy(state.TotalBytes / 1024)}"));
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

    public async Task DownloadSmallFilesAsync(List<FileDownloadRequest> requests, bool blProxy, CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateCompleted?.Invoke(this, new UpdateResult(false, $"{ResUI.Downloading} 0/{requests.Count}"));

            var webProxy = await GetWebProxy(blProxy, cancellationToken);
            await DownloaderHelper.Instance.DownloadSmallFilesAsync(webProxy,
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
                UpdateCompleted?.Invoke(this, new UpdateResult(allCompleted, $"{completedCount}/{span.Length} | {Utils.HumanFy((long)totalSpeed / 1024)}/s {Utils.HumanFy(totalDownloadedBytes / 1024)}/{Utils.HumanFy(totalTotalBytes / 1024)} {downloadingFileName}"));
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
    /// Gets redirect target URL without following redirects automatically.
    /// </summary>
    public async Task<string?> UrlRedirectAsync(string url, bool blProxy, CancellationToken cancellationToken = default)
    {
        var webRequestHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            Proxy = await GetWebProxy(blProxy, cancellationToken)
        };
        var certificateChainPolicy = CertPemManager.Instance.BuildCertificateChainPolicy();
        if (certificateChainPolicy != null)
        {
            webRequestHandler.SslOptions.CertificateChainPolicy = certificateChainPolicy;
            webRequestHandler.SslOptions.RemoteCertificateValidationCallback = null;
        }
        using var client = new HttpClient(webRequestHandler);

        var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is not null)
        {
            return response.Headers.Location.ToString();
        }
        else
        {
            Error?.Invoke(this, new ErrorEventArgs(new Exception("StatusCode error: " + response.StatusCode)));
            Logging.SaveLog("StatusCode error: " + url);
            return null;
        }
    }

    /// <summary>
    /// Tries to download string content using proxy switch setting.
    /// </summary>
    public async Task<string?> TryDownloadString(string url, bool blProxy, string userAgent, CancellationToken cancellationToken = default)
    {
        var webProxy = await GetWebProxy(blProxy, cancellationToken);
        return await TryDownloadString(url, webProxy, userAgent, cancellationToken);
    }

    /// <summary>
    /// Tries to download string content with a specified proxy.
    /// </summary>
    public async Task<string?> TryDownloadString(string url, IWebProxy? webProxy, string userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            var result1 = await DownloadStringAsync(url, webProxy, userAgent, cancellationToken);
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
            var result2 = await DownloadStringViaDownloader(url, webProxy, userAgent, cancellationToken);
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
    /// Downloads string content via HttpClient.
    /// </summary>
    private async Task<string?> DownloadStringAsync(string url, IWebProxy? webProxy, string userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            var handler = new SocketsHttpHandler
            {
                Proxy = webProxy,
                UseProxy = webProxy != null,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = webProxy is null ? Global.DirectDownloadConnect : Global.ProxyDownloadConnect,
            };
            var certificateChainPolicy = CertPemManager.Instance.BuildCertificateChainPolicy();
            if (certificateChainPolicy != null)
            {
                handler.SslOptions.CertificateChainPolicy = certificateChainPolicy;
                handler.SslOptions.RemoteCertificateValidationCallback = null;
            }

            using var client = new HttpClient(HttpRequestHeadersHelper.CreateHandler(handler, RequestHeaders));
            client.Timeout = Timeout.InfiniteTimeSpan;

            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);
            if (AcceptHeader.IsNotEmpty())
            {
                client.DefaultRequestHeaders.Accept.ParseAdd(AcceptHeader);
            }

            Uri uri = new(url);
            //Authorization Header
            if (uri.UserInfo.IsNotEmpty())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
            }

            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(webProxy is null ? Global.DirectFetch : Global.ProxyFetch);
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
    /// Downloads string content via DownloaderHelper.
    /// </summary>
    private async Task<string?> DownloadStringViaDownloader(string url, IWebProxy? webProxy, string userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            var result = await DownloaderHelper.Instance.DownloadStringAsync(webProxy, url, userAgent, RequestHeaders, AcceptHeader, cancellationToken);
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
    /// Creates local SOCKS proxy when proxy switch is enabled.
    /// </summary>
    private async Task<WebProxy?> GetWebProxy(bool blProxy, CancellationToken cancellationToken = default)
    {
        if (!blProxy)
        {
            return null;
        }
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        if (await SocksPortCheck(Global.Loopback, port, cancellationToken) == false)
        {
            return null;
        }

        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    /// <summary>
    /// Checks whether the specified TCP endpoint is reachable.
    /// </summary>
    private async Task<bool> SocksPortCheck(string ip, int port, CancellationToken cancellationToken = default)
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
}
