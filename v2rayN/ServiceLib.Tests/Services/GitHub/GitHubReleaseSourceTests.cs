using ServiceLib.Services.GitHub;

namespace ServiceLib.Tests.Services.GitHub;

public class GitHubReleaseSourceTests
{
    [Test]
    public async Task GetLatestTag_ReportsLatestRedirectStage()
    {
        await using var server = new RouteServer();
        server.Map("/latest", 302, location: "/tag/v1.0.0");
        server.Map("/tag/v1.0.0", 200);

        var stages = new List<EGitHubReleaseStage>();
        var tag = await GetLatestTag(server, stages, preRelease: false, lockedMaxVersion: null);

        await tag.Should().BeEqualTo("v1.0.0");
        await stages.Count.Should().BeEqualTo(1);
        await stages[0].Should().BeEqualTo(EGitHubReleaseStage.LatestRedirect);
    }

    [Test]
    public async Task GetLatestTag_ReportsReleasesApiStageWhenVersionIsLocked()
    {
        await using var server = new RouteServer();
        server.Map("/releases", 200, """
            [
              { "tag_name": "v1.2.0", "prerelease": false },
              { "tag_name": "v1.1.0", "prerelease": false }
            ]
            """);

        var stages = new List<EGitHubReleaseStage>();
        var tag = await GetLatestTag(server, stages, preRelease: false, lockedMaxVersion: new SemanticVersion(1, 2, 0));

        await tag.Should().BeEqualTo("v1.2.0");
        await stages.Count.Should().BeEqualTo(1);
        await stages[0].Should().BeEqualTo(EGitHubReleaseStage.ReleasesApi);
    }

    [Test]
    public async Task GetLatestTag_FallsBackToGitRefsWhenApiFails()
    {
        await using var server = new RouteServer();
        server.Map("/releases", 500);
        server.Map("/info/refs", 200,
            "001e# service=git-upload-pack\n"
            + "0000"
            + "0044aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa refs/tags/v2.0.0\n");
        server.Map("/tag/v2.0.0", 200);

        var stages = new List<EGitHubReleaseStage>();
        var tag = await GetLatestTag(server, stages, preRelease: true, lockedMaxVersion: null);

        await tag.Should().BeEqualTo("v2.0.0");
        await string.Join(",", stages).Should().BeEqualTo("ReleasesApi,ReleasesApiFailed,GitRefs");
    }

    [Test]
    public async Task GetLatestTag_ReportsGitRefsFailureWhenEverythingFails()
    {
        await using var server = new RouteServer();
        server.Map("/releases", 500);
        server.Map("/info/refs", 500);

        var stages = new List<EGitHubReleaseStage>();
        var tag = await GetLatestTag(server, stages, preRelease: true, lockedMaxVersion: null);

        await tag.Should().BeNull();
        await string.Join(",", stages).Should().BeEqualTo("ReleasesApi,ReleasesApiFailed,GitRefs,GitRefsFailed");
    }

    [Test]
    public async Task GetLatestTag_DoesNotFallBackToGitRefsWithoutPrerelease()
    {
        await using var server = new RouteServer();
        server.Map("/releases", 500);
        server.Map("/info/refs", 200);

        var stages = new List<EGitHubReleaseStage>();
        var tag = await GetLatestTag(server, stages, preRelease: false, lockedMaxVersion: new SemanticVersion(1, 0, 0));

        await tag.Should().BeNull();
        await string.Join(",", stages).Should().BeEqualTo("ReleasesApi,ReleasesApiFailed");
    }

    private static async Task<string?> GetLatestTag(RouteServer server, List<EGitHubReleaseStage> stages,
        bool preRelease, SemanticVersion? lockedMaxVersion)
    {
        await CertPemManager.Instance.Init(new Config { GuiItem = new GUIItem() });
        using var downloadService = new DownloadService();
        var source = new GitHubReleaseSource(downloadService, progress =>
        {
            stages.Add(progress.Stage);
            return Task.CompletedTask;
        });
        var coreInfo = new CoreInfo
        {
            CoreType = ECoreType.Xray,
            Url = server.Url,
            ReleaseApiUrl = $"{server.Url}/releases",
            GitInfoRefsUrl = $"{server.Url}/info/refs",
            LockedMaxVersion = lockedMaxVersion,
        };

        return await source.GetLatestTag(coreInfo, preRelease).WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class RouteServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private readonly Dictionary<string, (int Status, string Body, string? Location)> _routes = new(StringComparer.Ordinal);

        public RouteServer()
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _serverTask = ServeAsync();
        }

        public string Url { get; }

        public void Map(string path, int status, string body = "", string? location = null)
        {
            _routes[path] = (status, body, location);
        }

        private async Task ServeAsync()
        {
            var cancellationToken = _cancellation.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(cancellationToken);
                if (requestLine == null)
                {
                    continue;
                }
                while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
                {
                }

                var parts = requestLine.Split(' ');
                var method = parts[0];
                var path = parts.Length > 1 ? parts[1] : "/";
                var queryIndex = path.IndexOf('?');
                if (queryIndex >= 0)
                {
                    path = path[..queryIndex];
                }

                var route = _routes.TryGetValue(path, out var mapped)
                    ? mapped
                    : (Status: 404, Body: string.Empty, Location: (string?)null);
                var payload = Encoding.UTF8.GetBytes(method == "HEAD" ? string.Empty : route.Body);
                var builder = new StringBuilder()
                    .Append($"HTTP/1.1 {route.Status} Status\r\n")
                    .Append($"Content-Length: {Encoding.UTF8.GetByteCount(route.Body)}\r\n");
                if (route.Location != null)
                {
                    builder.Append($"Location: {route.Location}\r\n");
                }
                builder.Append("Connection: close\r\n\r\n");

                await stream.WriteAsync(Encoding.UTF8.GetBytes(builder.ToString()), cancellationToken);
                await stream.WriteAsync(payload, cancellationToken);
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
