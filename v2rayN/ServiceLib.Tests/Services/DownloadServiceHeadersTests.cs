namespace ServiceLib.Tests.Services;

public class DownloadServiceHeadersTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task TryDownloadString_ShouldSendCustomHeadersThroughBothDownloaders(bool useProxy, bool failFirstRequest)
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        await using var server = new SubscriptionHttpServer(failFirstRequest);
        const string json = """
            {
              "accept": "application/json",
              "user-agent": "CustomSubscriptionClient/1.0",
              "authorization": "Bearer test-token",
              "X-hwid": "test-device",
              "Cookie": "session=test",
              "Content-Type": "application/json"
            }
            """;
        await HttpRequestHeadersHelper.TryParse(json, out var headers).Should().BeTrue();
        var service = new DownloadService { AcceptHeader = "*/*", RequestHeaders = headers };
        var uri = new UriBuilder(useProxy ? "http://subscription.invalid/sub" : server.Url)
        {
            UserName = "user",
            Password = "password"
        }.Uri;
        IWebProxy? proxy = useProxy ? new WebProxy(server.Url) : null;

        var content = await service.TryDownloadString(uri.AbsoluteUri, proxy, "OriginalClient/1.0").WaitAsync(TimeSpan.FromSeconds(20));

        await content.Should().BeEqualTo(SubscriptionHttpServer.Body);
        await (server.Requests.Count >= (failFirstRequest ? 2 : 1)).Should().BeTrue();
        foreach (var request in server.Requests)
        {
            await request["Accept"].Should().BeEqualTo("application/json");
            await request["User-Agent"].Should().BeEqualTo("CustomSubscriptionClient/1.0");
            await request["Authorization"].Should().BeEqualTo("Bearer test-token");
            await request["X-hwid"].Should().BeEqualTo("test-device");
            await request["Cookie"].Should().BeEqualTo("session=test");
            await request["Content-Type"].Should().BeEqualTo("application/json");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TryDownloadString_ShouldKeepDefaultAcceptUserAgentAndBasicAuth(bool failFirstRequest)
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        await using var server = new SubscriptionHttpServer(failFirstRequest);
        var service = new DownloadService { AcceptHeader = "*/*" };
        var uri = new UriBuilder(server.Url) { UserName = "user", Password = "password" }.Uri;

        var content = await service.TryDownloadString(uri.AbsoluteUri, (IWebProxy?)null, "ExistingClient/1.0").WaitAsync(TimeSpan.FromSeconds(20));

        await content.Should().BeEqualTo(SubscriptionHttpServer.Body);
        foreach (var request in server.Requests)
        {
            await request["Accept"].Should().BeEqualTo("*/*");
            await request["User-Agent"].Should().BeEqualTo("ExistingClient/1.0");
            await request["Authorization"].Should().BeEqualTo("Basic dXNlcjpwYXNzd29yZA==");
            await request.ContainsKey("X-hwid").Should().BeFalse();
        }
    }

    [Test]
    public async Task TryDownloadString_ShouldNotShareHeadersWithOtherDownloads()
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        await using var server = new SubscriptionHttpServer();
        var subscription = new DownloadService
        {
            AcceptHeader = "*/*",
            RequestHeaders = new Dictionary<string, string> { ["X-hwid"] = "first-device" }
        };
        var ordinaryDownload = new DownloadService();

        await (await subscription.TryDownloadString(server.Url, (IWebProxy?)null, "TestClient/1.0")).Should().BeEqualTo(SubscriptionHttpServer.Body);
        await (await ordinaryDownload.TryDownloadString(server.Url, (IWebProxy?)null, "TestClient/1.0")).Should().BeEqualTo(SubscriptionHttpServer.Body);

        var requests = server.Requests.ToArray();
        await requests.Length.Should().BeEqualTo(2);
        await requests[0]["X-hwid"].Should().BeEqualTo("first-device");
        await requests[1].ContainsKey("X-hwid").Should().BeFalse();
        await requests[1].ContainsKey("Accept").Should().BeFalse();
    }

    private sealed class SubscriptionHttpServer : IAsyncDisposable
    {
        public const string Body = "subscription-test-content";
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private readonly bool _failFirstRequest;

        public string Url { get; }
        public ConcurrentQueue<Dictionary<string, string>> Requests { get; } = new();

        public SubscriptionHttpServer(bool failFirstRequest = false)
        {
            _failFirstRequest = failFirstRequest;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/subscription";
            _serverTask = ServeAsync();
        }

        private async Task ServeAsync()
        {
            var cancellationToken = _cancellation.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(cancellationToken);
                if (requestLine == null)
                {
                    continue;
                }

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
                {
                    var separator = line.IndexOf(':');
                    var name = line.Substring(0, separator);
                    var value = line.Substring(separator + 1).Trim();
                    headers[name] = headers.TryGetValue(name, out var previous) ? $"{previous}, {value}" : value;
                }
                Requests.Enqueue(headers);

                var status = _failFirstRequest && Requests.Count == 1 ? "503 Service Unavailable" : "200 OK";
                var body = requestLine.StartsWith("HEAD ") ? "" : Body;
                var response = $"HTTP/1.1 {status}\r\nContent-Length: {Body.Length}\r\nConnection: close\r\n\r\n{body}";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
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
