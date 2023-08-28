using Downloader;
using System.IO;
using System.Net;

namespace v2rayN.Base;

internal class DownloaderHelper
{
    private static readonly Lazy<DownloaderHelper> _instance = new(() => new());
    public static DownloaderHelper Instance => _instance.Value;

    public async Task<string?> DownloadStringAsync(IWebProxy? webProxy, string url, string? userAgent, int timeout)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        Uri uri = new(url);
        //Authorization Header
        var headers = new WebHeaderCollection();
        if (!Utils.IsNullOrEmpty(uri.UserInfo))
        {
            headers.Add(HttpRequestHeader.Authorization, "Basic " + Utils.Base64Encode(uri.UserInfo));
        }

        var downloadOpt = new DownloadConfiguration()
        {
            Timeout = timeout * 1000,
            MaxTryAgainOnFailover = 2,
            RequestConfiguration =
            {
                Headers = headers,
                UserAgent = userAgent,
                Timeout = timeout * 1000,
                Proxy = webProxy
            }
        };

        using var downloader = new DownloadService(downloadOpt);
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (value.Error != null)
            {
                throw value.Error;
            }
        };

        using var cts = new CancellationTokenSource();
        using var stream = await downloader.DownloadFileTaskAsync(address: url, cts.Token).WaitAsync(TimeSpan.FromSeconds(timeout), cts.Token);
        using StreamReader reader = new(stream);

        downloadOpt = null;

        return reader.ReadToEnd();
    }

    public async Task DownloadDataAsync4Speed(IWebProxy webProxy, string url, IProgress<string> progress, int timeout)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        var downloadOpt = new DownloadConfiguration()
        {
            Timeout = timeout * 1000,
            MaxTryAgainOnFailover = 2,
            RequestConfiguration =
            {
                Timeout= timeout * 1000,
                Proxy = webProxy
            }
        };

        DateTime totalDatetime = DateTime.Now;
        int totalSecond = 0;
        var hasValue = false;
        double maxSpeed = 0;
        using var downloader = new DownloadService(downloadOpt);
        //downloader.DownloadStarted += (sender, value) =>
        //{
        //    if (progress != null)
        //    {
        //        progress.Report("Start download data...");
        //    }
        //};
        downloader.DownloadProgressChanged += (sender, value) =>
        {
            TimeSpan ts = (DateTime.Now - totalDatetime);
            if (progress != null && ts.Seconds > totalSecond)
            {
                hasValue = true;
                totalSecond = ts.Seconds;
                if (value.BytesPerSecondSpeed > maxSpeed)
                {
                    maxSpeed = value.BytesPerSecondSpeed;
                    var speed = (maxSpeed / 1000 / 1000).ToString("#0.0");
                    progress.Report(speed);
                }
            }
        };
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (progress != null)
            {
                if (!hasValue && value.Error != null)
                {
                    progress.Report(value.Error?.Message);
                }
            }
        };
        //progress.Report("......");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout * 1000);
        using var stream = await downloader.DownloadFileTaskAsync(address: url, cts.Token);

        downloadOpt = null;
    }

    public async Task DownloadFileAsync(IWebProxy? webProxy, string url, string fileName, IProgress<double> progress, int timeout)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url));
        }
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }

        var downloadOpt = new DownloadConfiguration()
        {
            Timeout = timeout * 1000,
            MaxTryAgainOnFailover = 2,
            RequestConfiguration =
            {
                Timeout= timeout * 1000,
                Proxy = webProxy
            }
        };

        var progressPercentage = 0;
        var hasValue = false;
        using var downloader = new DownloadService(downloadOpt);
        downloader.DownloadStarted += (sender, value) =>
        {
            progress?.Report(0);
        };
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
            }
        };

        using var cts = new CancellationTokenSource();
        await downloader.DownloadFileTaskAsync(url, fileName, cts.Token).WaitAsync(TimeSpan.FromSeconds(timeout), cts.Token);

        downloadOpt = null;
    }
}