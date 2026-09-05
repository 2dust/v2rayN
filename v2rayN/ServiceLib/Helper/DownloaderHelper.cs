using System.Security.Authentication;
using Downloader;

namespace ServiceLib.Helper;

public class DownloaderHelper
{
    private static readonly Lazy<DownloaderHelper> _instance = new(() => new());
    public static DownloaderHelper Instance => _instance.Value;

    public async Task<string?> DownloadStringAsync(IWebProxy? webProxy, string url, string? userAgent, int timeout,
        IReadOnlyDictionary<string, string>? requestHeaders = null, string? acceptHeader = null)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        var connectTimeout = Math.Clamp(timeout / 5, 2, 5);

        Uri uri = new(url);
        //Authorization Header
        var headers = new WebHeaderCollection();
        if (uri.UserInfo.IsNotEmpty())
        {
            headers.Add(HttpRequestHeader.Authorization, "Basic " + Utils.Base64Encode(uri.UserInfo));
        }

        var requestConfiguration = new RequestConfiguration()
        {
            Headers = headers,
            Accept = acceptHeader,
            UserAgent = userAgent,
            ConnectTimeout = connectTimeout * 1000,
            Proxy = webProxy
        };
        var downloadOpt = new DownloadConfiguration()
        {
            BlockTimeout = timeout * 1000,
            MaxTryAgainOnFailure = 2,
            RequestConfiguration = requestConfiguration,
            CustomHttpMessageHandlerFactory = () => HttpRequestHeadersHelper.CreateHandler(GetSocketsHttpHandler(requestConfiguration), requestHeaders),
        };

        await using var downloader = new Downloader.DownloadService(downloadOpt);
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (value.Error != null)
            {
                throw value.Error;
            }
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        await using var stream = await downloader.DownloadFileTaskAsync(address: url, cts.Token);
        using StreamReader reader = new(stream);

        return await reader.ReadToEndAsync(cts.Token);
    }

    public async Task DownloadDataAsync4Speed(IWebProxy webProxy, string url, IProgress<string> progress, int timeout)
    {
        if (url.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(url));
        }

        var connectTimeout = Math.Clamp(timeout / 5, 2, 5);
        var requestConfiguration = new RequestConfiguration()
        {
            ConnectTimeout = connectTimeout * 1000,
            Proxy = webProxy
        };
        var downloadOpt = new DownloadConfiguration()
        {
            BlockTimeout = timeout * 1000,
            MaxTryAgainOnFailure = 2,
            RequestConfiguration = requestConfiguration,
            CustomHttpMessageHandlerFactory = () => GetSocketsHttpHandler(requestConfiguration),
        };

        var lastUpdateTime = DateTime.Now;
        var hasValue = false;
        double maxSpeed = 0;
        await using var downloader = new Downloader.DownloadService(downloadOpt);

        downloader.DownloadProgressChanged += (sender, value) =>
        {
            if (progress != null && value.BytesPerSecondSpeed > 0)
            {
                hasValue = true;
                if (value.BytesPerSecondSpeed > maxSpeed)
                {
                    maxSpeed = value.BytesPerSecondSpeed;
                }

                var ts = DateTime.Now - lastUpdateTime;
                if (ts.TotalMilliseconds >= 1000)
                {
                    lastUpdateTime = DateTime.Now;
                    var speed = (maxSpeed / 1000 / 1000).ToString("#0.0");
                    progress.Report(speed);
                }
            }
        };
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (progress != null)
            {
                if (hasValue && maxSpeed > 0)
                {
                    var finalSpeed = (maxSpeed / 1000 / 1000).ToString("#0.0");
                    progress.Report(finalSpeed);
                }
                else if (value.Error != null)
                {
                    progress.Report(value.Error?.Message);
                }
                else
                {
                    progress.Report("0");
                }
            }
        };
        //progress.Report("......");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));
        await using var stream = await downloader.DownloadFileTaskAsync(address: url, cts.Token);
    }

    public async Task DownloadFileAsync(IWebProxy? webProxy, FileDownloadRequest request, Action<FileDownloadState> onProgress, TimeSpan connectTimeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FilePath.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(request.FilePath));
        }
        if (File.Exists(request.FilePath))
        {
            File.Delete(request.FilePath);
        }

        var state = new FileDownloadState
        {
            Request = request,
        };

        var requestConfiguration = new RequestConfiguration()
        {
            ConnectTimeout = (int)connectTimeout.TotalMilliseconds,
            Proxy = webProxy,
        };
        var downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 100,
            MinimumChunkSize = 8 * 1024 * 1024, // 8 MB
            MinimumSizeOfChunking = 8 * 1024 * 1024, // 8 MB
            ParallelDownload = true,
            ParallelCount = 4,

            RequestConfiguration = requestConfiguration,
            CustomHttpMessageHandlerFactory = () => GetSocketsHttpHandler(requestConfiguration),
        };

        await using var downloader = new Downloader.DownloadService(downloadOpt);
        downloader.DownloadStarted += (sender, value) =>
        {
            state = state with
            {
                TotalBytes = value.TotalBytesToReceive,
            };
            onProgress.Invoke(state);
        };
        downloader.DownloadProgressChanged += (sender, value) =>
        {
            state = state with
            {
                DownloadedBytes = value.ReceivedBytesSize,
                TotalBytes = value.TotalBytesToReceive,
                SpeedBytesPerSecond = value.BytesPerSecondSpeed,
            };
            onProgress.Invoke(state);
        };
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            state = state with
            {
                Completed = true,
                Error = value.Error,
            };
            onProgress.Invoke(state);
        };

        await downloader.DownloadFileTaskAsync(request.FileUrl, request.FilePath, cancellationToken);
    }

    public async Task DownloadSmallFilesAsync(IWebProxy? webProxy, List<FileDownloadRequest> requests, Action<ReadOnlyMemory<FileDownloadState>> onProgress, TimeSpan connectTimeout, CancellationToken cancellationToken = default)
    {
        if (requests is not { Count: > 0 })
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var states = new FileDownloadState[requests.Count];
        for (var i = 0; i < requests.Count; i++)
        {
            states[i] = new FileDownloadState
            {
                Request = requests[i],
            };
        }
        var readOnlyStates = new ReadOnlyMemory<FileDownloadState>(states);

        var requestConfiguration = new RequestConfiguration()
        {
            ConnectTimeout = (int)connectTimeout.TotalMilliseconds,
            Proxy = webProxy,

            KeepAlive = true,
        };
        using var socketsHttpHandler = GetSocketsHttpHandler(requestConfiguration);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            //CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, requests.Count), parallelOptions, async (index, parallelCancellationToken) =>
        {
            var request = requests[index];
            var downloadOpt = new DownloadConfiguration()
            {
                RequestConfiguration = requestConfiguration,
                // ReSharper disable once AccessToDisposedClosure
                CustomHttpMessageHandlerFactory = () => socketsHttpHandler,
            };
            await using var downloader = new Downloader.DownloadService(downloadOpt);
            downloader.DownloadStarted += (sender, value) =>
            {
                states[index] = states[index] with
                {
                    DownloadedBytes = 0,
                    TotalBytes = value.TotalBytesToReceive,
                    SpeedBytesPerSecond = 0,
                    Completed = false,
                };
                onProgress.Invoke(readOnlyStates);
            };
            downloader.DownloadProgressChanged += (sender, value) =>
            {
                states[index] = states[index] with
                {
                    DownloadedBytes = value.ReceivedBytesSize,
                    TotalBytes = value.TotalBytesToReceive,
                    SpeedBytesPerSecond = value.BytesPerSecondSpeed,
                    Completed = false,
                };
                onProgress.Invoke(readOnlyStates);
            };
            downloader.DownloadFileCompleted += (sender, value) =>
            {
                var newState = states[index] with { Completed = true };
                if (value.Error != null)
                {
                    newState = newState with { Error = value.Error };
                }
                states[index] = newState;
                onProgress.Invoke(readOnlyStates);
            };
            await downloader.DownloadFileTaskAsync(request.FileUrl, request.FilePath, parallelCancellationToken);
        });
    }

    // https://github.com/bezzad/Downloader/blob/a75a6e431acd6cbba6293f7afdcf676544a09174/src/Downloader/SocketClient.cs#L45
    // There is a risk of MITM attacks
    // https://github.com/bezzad/Downloader/blob/a75a6e431acd6cbba6293f7afdcf676544a09174/src/Downloader/Extensions/ExceptionHelper.cs#L111
    private static SocketsHttpHandler GetSocketsHttpHandler(RequestConfiguration config)
    {
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = config.AllowAutoRedirect,
            MaxAutomaticRedirections = config.MaximumAutomaticRedirections,
            AutomaticDecompression = config.AutomaticDecompression,
            PreAuthenticate = config.PreAuthenticate,
            UseCookies = config.CookieContainer != null,
            UseProxy = config.Proxy != null,
            MaxConnectionsPerServer = 1000,
            PooledConnectionIdleTimeout = config.KeepAliveTimeout,
            PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromMilliseconds(config.ConnectTimeout),
        };

        // Set up the SslClientAuthenticationOptions for custom certificate validation
        if (config.ClientCertificates?.Count > 0)
        {
            handler.SslOptions.ClientCertificates = config.ClientCertificates;
        }

        handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
        //handler.SslOptions.RemoteCertificateValidationCallback = ExceptionHelper.CertificateValidationCallBack;

        var certificateChainPolicy = CertPemManager.Instance.BuildCertificateChainPolicy();
        if (certificateChainPolicy != null)
        {
            handler.SslOptions.CertificateChainPolicy = certificateChainPolicy;
            handler.SslOptions.RemoteCertificateValidationCallback = null;
        }

        // Configure keep-alive
        if (config.KeepAlive)
        {
            handler.KeepAlivePingTimeout = config.KeepAliveTimeout;
            handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests;
        }

        // Configure credentials
        if (config.Credentials != null)
        {
            handler.Credentials = config.Credentials;
            handler.PreAuthenticate = config.PreAuthenticate;
        }

        // Configure cookies
        if (handler.UseCookies && config.CookieContainer != null)
        {
            handler.CookieContainer = config.CookieContainer;
        }

        // Configure proxy
        if (handler.UseProxy && config.Proxy != null)
        {
            handler.Proxy = config.Proxy;
        }

        // Add expect header
        if (!string.IsNullOrWhiteSpace(config.Expect))
        {
            handler.Expect100ContinueTimeout = TimeSpan.FromSeconds(1);
        }

        return handler;
    }
}
