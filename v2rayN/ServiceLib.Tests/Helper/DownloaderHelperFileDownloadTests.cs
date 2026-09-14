namespace ServiceLib.Tests.Helper;

public class DownloaderHelperFileDownloadTests
{
    [Test]
    public async Task StallTimeout_ShouldOutlastRecoveryFromStalledTransfer()
    {
        // Downloader fails a stalled read after BlockTimeout and retries after a delay of up to BlockTimeout.
        // A retry that gets no response fails after the HttpClient timeout, and the next retry waits up to 10 s.
        var configuration = new Downloader.DownloadConfiguration();
        var blockTimeout = TimeSpan.FromMilliseconds(configuration.BlockTimeout);
        var recovery = blockTimeout + blockTimeout + TimeSpan.FromMilliseconds(configuration.HttpClientTimeout) + TimeSpan.FromSeconds(10);

        await (DownloaderHelper.Instance.StallTimeout > recovery).Should().BeTrue();
    }

    [Test]
    [Arguments(ServerFailure.ResetConnection, 64 * 1024)]
    [Arguments(ServerFailure.ResetConnection, 48 * 1024 * 1024)]
    [Arguments(ServerFailure.CloseWithoutResponse, 64 * 1024)]
    [Arguments(ServerFailure.ServiceUnavailable, 64 * 1024)]
    [Arguments(ServerFailure.StopListening, 64 * 1024)]
    [Arguments(ServerFailure.NoResponse, 64 * 1024)]
    public async Task DownloadFileAsync_ShouldReportTimeoutWhenRequestsAfterFirstResponseFail(ServerFailure failure, int fileLength)
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        var stallTimeout = TimeSpan.FromSeconds(3);
        await using var server = new RangeHttpServer(fileLength, failure);
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var state = await DownloadFileAsync(stallTimeout, server.Url, filePath);

            await (stopwatch.Elapsed < stallTimeout + TimeSpan.FromSeconds(5)).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error is TimeoutException).Should().BeTrue();
            await File.Exists(filePath).Should().BeFalse();
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldReportTimeoutWhenServerNeverResponds()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        var stallTimeout = TimeSpan.FromSeconds(3);
        await using var server = new RangeHttpServer(64 * 1024, ServerFailure.NoResponse, firstFailingConnection: 0);
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var state = await DownloadFileAsync(stallTimeout, server.Url, filePath);

            await (stopwatch.Elapsed < stallTimeout + TimeSpan.FromSeconds(5)).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error is TimeoutException).Should().BeTrue();
            await File.Exists(filePath).Should().BeFalse();
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldReportErrorThatIsNotRetried()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        var stallTimeout = TimeSpan.FromSeconds(30);
        await using var server = new RangeHttpServer(64 * 1024, ServerFailure.NotFound);
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var state = await DownloadFileAsync(stallTimeout, server.Url, filePath);

            await (stopwatch.Elapsed < stallTimeout).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error is HttpRequestException).Should().BeTrue();
            await File.Exists(filePath).Should().BeFalse();
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldRecoverWhenOneRequestAfterFirstResponseFails()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        const int fileLength = 64 * 1024;
        await using var server = new RangeHttpServer(fileLength, ServerFailure.ResetConnection, failingConnections: 1);
        var filePath = GetTempFilePath();
        try
        {
            var state = await DownloadFileAsync(TimeSpan.FromSeconds(30), server.Url, filePath);

            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error == null).Should().BeTrue();
            await AssertFileContentAsync(filePath, fileLength);
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldResumeAfterTransferIsCutAndNextRequestsFail()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        const int fileLength = 256 * 1024;
        // The cut and two responses without body are three failed attempts in a row: more than a retry limit of 2 allows, well within the stall timeout
        await using var server = new RangeHttpServer(fileLength, ServerFailure.CutTransfer, failingConnections: 1, failuresAfterCut: 2);
        var filePath = GetTempFilePath();
        try
        {
            var state = await DownloadFileAsync(TimeSpan.FromSeconds(45), server.Url, filePath);

            await server.RequestsFailedAfterCut.Should().BeEqualTo(2);
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error == null).Should().BeTrue();
            await AssertFileContentAsync(filePath, fileLength);
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldReportTimeoutWhenTransferIsCutAndAllNextRequestsFail()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        // Longer than Downloader's longest delay before a retry (10 s), so that the responses to the retries cannot keep the download alive
        var stallTimeout = TimeSpan.FromSeconds(12);
        // 16 blocks of 4 KiB, one every 250 ms, arrive before the cut, so that the timeout has to count from the last block
        await using var server = new RangeHttpServer(256 * 1024, ServerFailure.CutTransfer, failingConnections: 1, failuresAfterCut: int.MaxValue,
            writeDelay: TimeSpan.FromMilliseconds(250));
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            long receivedBytes = 0;
            var lastDataReceived = TimeSpan.Zero;
            var state = await DownloadFileAsync(stallTimeout, server.Url, filePath, onProgress: value =>
            {
                if (value.DownloadedBytes > receivedBytes)
                {
                    receivedBytes = value.DownloadedBytes;
                    lastDataReceived = stopwatch.Elapsed;
                }
            });
            var noDataReceivedFor = stopwatch.Elapsed - lastDataReceived;

            await (receivedBytes > 0).Should().BeTrue();
            // The stall timeout counts from the last data received; a timer can fire up to a system clock tick early
            await (noDataReceivedFor > stallTimeout - TimeSpan.FromMilliseconds(100)).Should().BeTrue();
            await (noDataReceivedFor < stallTimeout + TimeSpan.FromSeconds(5)).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error is TimeoutException).Should().BeTrue();
            await File.Exists(filePath).Should().BeFalse();
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldNotTimeOutWhileDataKeepsArriving()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        const int fileLength = 64 * 1024;
        var stallTimeout = TimeSpan.FromSeconds(2);
        // 16 blocks of 4 KiB, one every 250 ms: the transfer takes about twice the stall timeout
        await using var server = new RangeHttpServer(fileLength, writeDelay: TimeSpan.FromMilliseconds(250));
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var state = await DownloadFileAsync(stallTimeout, server.Url, filePath);

            await (stopwatch.Elapsed > stallTimeout).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error == null).Should().BeTrue();
            await AssertFileContentAsync(filePath, fileLength);
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldNotTimeOutWhenEachResponseArrivesWithinStallTimeout()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        const int fileLength = 64 * 1024;
        var stallTimeout = TimeSpan.FromSeconds(5);
        // The initial range request and the download request are answered after 3 s each
        await using var server = new RangeHttpServer(fileLength, responseDelay: TimeSpan.FromSeconds(3));
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var state = await DownloadFileAsync(stallTimeout, server.Url, filePath);

            await (stopwatch.Elapsed > stallTimeout).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error == null).Should().BeTrue();
            await AssertFileContentAsync(filePath, fileLength);
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldNotReportTimeoutWhenCallerCancels()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        const int fileLength = 64 * 1024;
        await using var server = new RangeHttpServer(fileLength, ServerFailure.NoResponse);
        var filePath = GetTempFilePath();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var state = await DownloadFileAsync(TimeSpan.FromSeconds(30), server.Url, filePath, cancelAfter: TimeSpan.FromSeconds(1));

            await (stopwatch.Elapsed < TimeSpan.FromSeconds(10)).Should().BeTrue();
            await (state?.Completed).Should().BeEqualTo(true);
            await (state?.Error is TimeoutException).Should().BeFalse();
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    private static async Task<FileDownloadState?> DownloadFileAsync(TimeSpan stallTimeout, string url, string filePath, TimeSpan? cancelAfter = null,
        Action<FileDownloadState>? onProgress = null)
    {
        FileDownloadState? lastState = null;
        var downloaderHelper = new DownloaderHelper { StallTimeout = stallTimeout };
        using var cancellation = new CancellationTokenSource();
        if (cancelAfter != null)
        {
            cancellation.CancelAfter(cancelAfter.Value);
        }
        try
        {
            await downloaderHelper.DownloadFileAsync(null, new FileDownloadRequest { FileUrl = url, FilePath = filePath },
                    state =>
                    {
                        lastState = state;
                        onProgress?.Invoke(state);
                    }, TimeSpan.FromSeconds(5), cancellation.Token)
                .WaitAsync(stallTimeout + TimeSpan.FromSeconds(60));
        }
        finally
        {
            // Stop a download that is still running so that it does not outlive the test
            await cancellation.CancelAsync();
        }
        return lastState;
    }

    private static async Task AssertFileContentAsync(string filePath, int fileLength)
    {
        var content = await File.ReadAllBytesAsync(filePath);
        await content.Length.Should().BeEqualTo(fileLength);
        await content.Select((value, position) => value == RangeHttpServer.GetByte(position)).All(x => x).Should().BeTrue();
    }

    private static string GetTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"v2rayN-downloader-test-{Guid.NewGuid():N}.bin");
    }

    private static void DeleteTempFiles(string filePath)
    {
        foreach (var path in new[] { filePath, filePath + ".download" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }

    public enum ServerFailure
    {
        ResetConnection,
        CloseWithoutResponse,
        ServiceUnavailable,
        NotFound,
        StopListening,
        NoResponse,
        CutTransfer,
        HeadersWithoutBody,
    }

    private sealed class RangeHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private readonly long _length;
        private readonly ServerFailure? _failure;
        private readonly int _firstFailingConnection;
        private readonly int _failingConnections;
        private readonly int _failuresAfterCut;
        private readonly TimeSpan _writeDelay;
        private readonly TimeSpan _responseDelay;

        public string Url { get; }
        public int RequestsFailedAfterCut { get; private set; }

        public RangeHttpServer(long length, ServerFailure? failure = null, int firstFailingConnection = 1, int failingConnections = int.MaxValue,
            int failuresAfterCut = 0, TimeSpan writeDelay = default, TimeSpan responseDelay = default)
        {
            _length = length;
            _failure = failure;
            _firstFailingConnection = firstFailingConnection;
            _failingConnections = failingConnections;
            _failuresAfterCut = failuresAfterCut;
            _writeDelay = writeDelay;
            _responseDelay = responseDelay;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v2rayN.zip";
            _serverTask = ServeAsync();
        }

        public static byte GetByte(long position)
        {
            return (byte)(position % 251);
        }

        private async Task ServeAsync()
        {
            var cancellationToken = _cancellation.Token;
            var pendingFailures = 0;
            // By default the first connection answers the downloader's initial range request and the following ones fail
            for (var connection = 0; !cancellationToken.IsCancellationRequested; connection++)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var failing = connection >= _firstFailingConnection && connection - _firstFailingConnection < _failingConnections;
                var failure = failing ? _failure : null;
                if (pendingFailures > 0)
                {
                    // Answer with headers but no body: HttpClient itself retries a request whose connection is reset before the response, so a reset is not always a failed attempt for the downloader
                    pendingFailures--;
                    RequestsFailedAfterCut++;
                    failure = ServerFailure.HeadersWithoutBody;
                }
                if (failure == ServerFailure.ResetConnection)
                {
                    // A zero linger timeout makes closing the socket send RST
                    client.Client.LingerState = new LingerOption(true, 0);
                    continue;
                }

                try
                {
                    await RespondAsync(client.GetStream(), failure, cancellationToken);
                }
                catch (IOException)
                {
                    // The downloader may close the connection once it has read what it needs
                }

                if (failure == ServerFailure.CutTransfer)
                {
                    // Close the cut transfer normally, so that the downloader receives all data sent before the cut
                    pendingFailures = _failuresAfterCut;
                }
                if (_failure == ServerFailure.StopListening)
                {
                    _listener.Stop();
                    return;
                }
            }
        }

        private async Task RespondAsync(NetworkStream stream, ServerFailure? failure, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken);
            string? range = null;
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
            {
                if (line.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    range = line["Range: bytes=".Length..];
                }
            }
            if (requestLine == null || failure == ServerFailure.CloseWithoutResponse)
            {
                return;
            }
            if (failure == ServerFailure.NoResponse)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (_responseDelay > TimeSpan.Zero)
            {
                await Task.Delay(_responseDelay, cancellationToken);
            }
            if (failure is ServerFailure.ServiceUnavailable or ServerFailure.NotFound)
            {
                var status = failure == ServerFailure.NotFound ? "404 Not Found" : "503 Service Unavailable";
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), cancellationToken);
                return;
            }

            long from = 0;
            var to = _length - 1;
            var header = "HTTP/1.1 200 OK\r\n";
            if (range != null)
            {
                var bounds = range.Split('-');
                from = long.Parse(bounds[0]);
                if (bounds[1].Length > 0)
                {
                    to = Math.Min(long.Parse(bounds[1]), to);
                }
                header = $"HTTP/1.1 206 Partial Content\r\nContent-Range: bytes {from}-{to}/{_length}\r\n";
            }
            header += $"Accept-Ranges: bytes\r\nContent-Length: {to - from + 1}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header), cancellationToken);

            // A cut transfer stops after the first quarter of the announced body, a response without body right after the headers
            var end = failure switch
            {
                ServerFailure.CutTransfer => from + (to - from + 1) / 4 - 1,
                ServerFailure.HeadersWithoutBody => from - 1,
                _ => to,
            };
            var buffer = new byte[_writeDelay > TimeSpan.Zero ? 4 * 1024 : 64 * 1024];
            for (var position = from; position <= end; position += buffer.Length)
            {
                var count = (int)Math.Min(buffer.Length, end - position + 1);
                for (var i = 0; i < count; i++)
                {
                    buffer[i] = GetByte(position + i);
                }
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                if (_writeDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_writeDelay, cancellationToken);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                // Stop listening only after the server loop has ended, so that it never sees a disposed listener
                _listener.Stop();
                _cancellation.Dispose();
            }
        }
    }
}
