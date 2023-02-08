using Downloader;
using System.Net;

namespace v2rayN.Base
{
    internal class DownloaderHelper
    {
        private static readonly Lazy<DownloaderHelper> _instance = new Lazy<DownloaderHelper>(() => new());
        public static DownloaderHelper Instance => _instance.Value;

        public DownloaderHelper()
        {
        }

        public async Task DownloadDataAsync4Speed(IWebProxy webProxy, string url, IProgress<string> progress, int timeout)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException("url");
            }

            var cancellationToken = new CancellationTokenSource();
            cancellationToken.CancelAfter(timeout * 1000);

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
            var downloader = new DownloadService(downloadOpt);
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
            progress.Report("......");

            await downloader.DownloadFileTaskAsync(address: url, cancellationToken: cancellationToken.Token);
            //var stream = await downloader.DownloadFileTaskAsync(url);

            //using (StreamReader reader = new StreamReader(stream))
            //{
            //    string text = reader.ReadToEnd();
            //    stream.Dispose();
            //}

            downloader.Dispose();
            downloader = null;
            downloadOpt = null;
        }
    }
}