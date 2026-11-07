using Downloader;
using DownloadService = Downloader.DownloadService;

namespace ServiceLib.Helper;

public class DownloaderHelper : IDisposable
{
    public HttpMessageHandler Handler { get; init; } = HttpClientHelper.Instance.DirectHandler;

    public bool DisposeHandler { get; init; } = false;

    public async Task<string?> DownloadStringAsync(string url,
        CancellationToken cancellationToken = default)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        var headers = new WebHeaderCollection();
        var uri = new Uri(url);
        if (uri.UserInfo.IsNotEmpty())
        {
            headers.Add(HttpRequestHeader.Authorization, "Basic " + Utils.Base64Encode(uri.UserInfo));
        }

        var requestConfiguration = new RequestConfiguration
        {
            Headers = headers,
        };
        var downloadOpt = new DownloadConfiguration
        {
            MaxTryAgainOnFailure = 2,
            RequestConfiguration = requestConfiguration,
            CustomHttpMessageHandlerFactory = GetHandler,
        };

        await using var downloader = new DownloadService(downloadOpt);
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (value.Error != null)
            {
                throw value.Error;
            }
        };

        await using var stream = await downloader.DownloadFileTaskAsync(url, cancellationToken);
        using StreamReader reader = new(stream);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task DownloadDataAsync4Speed(string url, Action<string> onProgress,
        CancellationToken cancellationToken = default)
    {
        if (url.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(url));
        }

        var downloadOpt = new DownloadConfiguration
        {
            MaxTryAgainOnFailure = 2,
            CustomHttpMessageHandlerFactory = GetHandler,
        };

        var lastUpdateTime = DateTime.Now;
        var hasValue = false;
        double maxSpeed = 0;
        await using var downloader = new DownloadService(downloadOpt);

        downloader.DownloadProgressChanged += (sender, value) =>
        {
            if (!(value.BytesPerSecondSpeed > 0))
            {
                return;
            }
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
                onProgress.Invoke(speed);
            }
        };
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (hasValue && maxSpeed > 0)
            {
                var finalSpeed = (maxSpeed / 1000 / 1000).ToString("#0.0");
                onProgress.Invoke(finalSpeed);
            }
            else if (value.Error != null)
            {
                onProgress.Invoke(value.Error?.Message);
            }
            else
            {
                onProgress.Invoke("0");
            }
        };
        // progress.Invoke("......");
        await using var stream = await downloader.DownloadFileTaskAsync(url, cancellationToken);
    }

    public async Task DownloadFileAsync(FileDownloadRequest request, Action<FileDownloadState> onProgress,
        CancellationToken cancellationToken = default)
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

        var downloadOpt = new DownloadConfiguration
        {
            ChunkCount = 16,
            ParallelDownload = true,
            ParallelCount = 4,
            MinimumChunkSize = 2 * 1024 * 1024, // 2 MB
            MinimumSizeOfChunking = 4 * 1024 * 1024, // 4 MB

            CustomHttpMessageHandlerFactory = GetHandler,
        };

        await using var downloader = new DownloadService(downloadOpt);
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

    public async Task DownloadSmallFilesAsync(List<FileDownloadRequest> requests,
        Action<ReadOnlyMemory<FileDownloadState>> onProgress, CancellationToken cancellationToken = default)
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

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, requests.Count), parallelOptions,
            async (index, parallelCancellationToken) =>
            {
                var request = requests[index];
                var downloadOpt = new DownloadConfiguration
                {
                    CustomHttpMessageHandlerFactory = GetHandler,
                };
                await using var downloader = new DownloadService(downloadOpt);
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
