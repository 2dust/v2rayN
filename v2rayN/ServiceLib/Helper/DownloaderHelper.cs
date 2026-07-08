using System.Security.Authentication;
using Downloader;

namespace ServiceLib.Helper;

public class DownloaderHelper
{
    private static readonly Lazy<DownloaderHelper> _instance = new(() => new());
    public static DownloaderHelper Instance => _instance.Value;

    public async Task<string?> DownloadStringAsync(IWebProxy? webProxy, string url, string? userAgent, int timeout)
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
            UserAgent = userAgent,
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

        downloadOpt = null;

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

        downloadOpt = null;
    }

    public async Task DownloadFileAsync(IWebProxy? webProxy, string url, string fileName, IProgress<double> progress, int timeout)
    {
        if (url.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(url));
        }
        if (fileName.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
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

        var progressPercentage = 0;
        var hasValue = false;
        await using var downloader = new Downloader.DownloadService(downloadOpt);
        downloader.DownloadStarted += (sender, value) => progress?.Report(0);
        downloader.DownloadProgressChanged += (sender, value) =>
        {
            hasValue = true;
            var percent = (int)value.ProgressPercentage;//   Convert.ToInt32((totalRead * 1d) / (total * 1d) * 100);
            if (progressPercentage != percent && percent % 10 == 0)
            {
                progressPercentage = percent;
                progress.Report(percent);
            }
        };
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (progress != null)
            {
                if (hasValue && value.Error == null)
                {
                    progress.Report(101);
                }
                else if (value.Error != null)
                {
                    throw value.Error;
                }
            }
        };

        using var cts = new CancellationTokenSource();
        await downloader.DownloadFileTaskAsync(url, fileName, cts.Token);

        downloadOpt = null;
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
            ConnectTimeout = TimeSpan.FromMilliseconds(config.ConnectTimeout)
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
