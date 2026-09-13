namespace ServiceLib.Tests.Services;

public class DownloadServiceFileTests
{
    [Test]
    [Arguments("404 Not Found")]
    [Arguments("500 Internal Server Error")]
    [Arguments(null)]
    public async Task DownloadFileAsync_ShouldReportFailedDownloadThroughErrorEvent(string? status)
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        await using var server = new FileHttpServer(status);
        var filePath = GetTempFilePath();
        try
        {
            var (results, errors) = await DownloadFileAsync(server.Url, filePath);

            await results.Any(x => x.Success).Should().BeFalse();
            await (errors.Count > 0).Should().BeTrue();
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ShouldReportSuccessOnceWhenFileIsDownloaded()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        await using var server = new FileHttpServer("200 OK");
        var filePath = GetTempFilePath();
        try
        {
            var (results, errors) = await DownloadFileAsync(server.Url, filePath);

            await errors.Count.Should().BeEqualTo(0);
            await results.Count(x => x.Success).Should().BeEqualTo(1);
            await (await File.ReadAllTextAsync(filePath)).Should().BeEqualTo(FileHttpServer.Body);
        }
        finally
        {
            DeleteTempFiles(filePath);
        }
    }

    private static async Task<(List<UpdateResult> Results, List<Exception> Errors)> DownloadFileAsync(string url, string filePath)
    {
        var results = new ConcurrentQueue<UpdateResult>();
        var errors = new ConcurrentQueue<Exception>();
        var service = new DownloadService();
        service.UpdateCompleted += (_, result) => results.Enqueue(result);
        service.Error += (_, args) => errors.Enqueue(args.GetException());

        await service.DownloadFileAsync(new FileDownloadRequest { FileUrl = url, FilePath = filePath }, false, TimeSpan.FromSeconds(5))
            .WaitAsync(TimeSpan.FromSeconds(20));

        return (results.ToList(), errors.ToList());
    }

    private static string GetTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"v2rayN-download-test-{Guid.NewGuid():N}.bin");
    }

    private static void DeleteTempFiles(string filePath)
    {
        File.Delete(filePath);
        File.Delete(filePath + ".download");
    }

    private sealed class FileHttpServer : IAsyncDisposable
    {
        public const string Body = "file-download-test-content";
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private readonly string? _status;

        public string Url { get; }

        /// <param name="status">HTTP status line to answer every request with; null closes the connection without a response.</param>
        public FileHttpServer(string? status)
        {
            _status = status;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/file.bin";
            _serverTask = ServeAsync();
        }

        private async Task ServeAsync()
        {
            var cancellationToken = _cancellation.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                try
                {
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var requestLine = await reader.ReadLineAsync(cancellationToken);
                    while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
                    {
                    }
                    if (requestLine == null || _status == null)
                    {
                        continue;
                    }

                    var content = _status.StartsWith("200 ") ? Body : _status;
                    var body = requestLine.StartsWith("HEAD ") ? "" : content;
                    var response = $"HTTP/1.1 {_status}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n{body}";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
                }
                catch (IOException)
                {
                    // The client may close the connection before the response is written.
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _cancellation.Dispose();
            }
        }
    }
}
