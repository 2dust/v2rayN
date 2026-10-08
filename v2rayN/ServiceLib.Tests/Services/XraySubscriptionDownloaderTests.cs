using System.IO.Compression;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using TUnit.Core;

namespace ServiceLib.Tests.Services;

// Set V2RAYN_TEST_XRAY_PATH to an Xray executable to run the real TLS/proxy integration cases.
[NotInParallel]
public class XraySubscriptionDownloaderTests
{
    [Test]
    [Arguments(true, 10, 0, 10240, (byte)1, true)]
    [Arguments(true, 10, 0, 17763, (byte)1, true)]
    [Arguments(true, 10, 0, 19045, (byte)1, true)]
    [Arguments(true, 10, 0, 17763, (byte)3, false)]
    [Arguments(true, 10, 0, 17763, (byte)2, false)]
    [Arguments(true, 10, 0, 20348, (byte)3, false)]
    [Arguments(true, 10, 0, 22000, (byte)1, false)]
    [Arguments(true, 10, 0, 26100, (byte)1, false)]
    [Arguments(true, 10, 0, 9200, (byte)1, false)]
    [Arguments(true, 6, 3, 9600, (byte)1, false)]
    [Arguments(true, 11, 0, 26100, (byte)1, false)]
    [Arguments(false, 10, 0, 19045, (byte)1, false)]
    public async Task BackendSelection_ShouldOnlyChangeWindows10Workstations(bool windows, int major, int minor, int build, byte productType, bool expected)
    {
        await XraySubscriptionDownloader.IsRequired(windows, new Version(major, minor, build), productType).Should().BeEqualTo(expected);
    }

    [Test]
    public async Task RuntimeSelection_ShouldKeepWindows11AndOtherPlatformsOnExistingTransport()
    {
        if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            Skip.Test("This assertion applies to Windows 11 and non-Windows test hosts.");
        }
        await XraySubscriptionDownloader.IsRequired().Should().BeFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Download_ShouldUseTls13AndPreserveHeadersPathAndDecompression(bool useProxy)
    {
        var executable = GetExecutable();
        await using var server = new TlsSubscriptionServer();
        await using var proxy = new SocksProxy();
        var downloader = new XraySubscriptionDownloader(() => executable, server.TrustPolicy());
        var uri = new UriBuilder(server.Url + "/subscription?format=test%2Bvalue") { UserName = "user", Password = "password" }.Uri;
        var headers = new Dictionary<string, string>
        {
            ["User-Agent"] = "CustomSubscription/1.0", ["Accept"] = "application/json",
            ["Authorization"] = "Bearer test-token", ["X-hwid"] = "test-device", ["Content-Type"] = "application/json"
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var result = await downloader.DownloadStringAsync(uri.AbsoluteUri, useProxy ? new WebProxy(proxy.Url) : null,
            "OriginalClient/1.0", headers, "*/*", cts.Token);

        await result.Should().BeEqualTo(TlsSubscriptionServer.Body);
        var request = server.Requests.Single();
        await request.Protocol.Should().BeEqualTo(SslProtocols.Tls13);
        await request.Path.Should().BeEqualTo("/subscription?format=test%2Bvalue");
        await request.Headers["Host"].Should().BeEqualTo(new Uri(server.Url).Authority);
        await request.Headers["User-Agent"].Should().BeEqualTo("CustomSubscription/1.0");
        await request.Headers["Accept"].Should().BeEqualTo("application/json");
        await request.Headers["Authorization"].Should().BeEqualTo("Bearer test-token");
        await request.Headers["X-hwid"].Should().BeEqualTo("test-device");
        await request.Headers["Content-Type"].Should().BeEqualTo("application/json");
        await (proxy.Connections > 0).Should().BeEqualTo(useProxy);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Redirect_ShouldKeepSecureCookiesAndClearAuthorization(bool crossOrigin)
    {
        var executable = GetExecutable();
        await using var second = new TlsSubscriptionServer();
        await using var first = new TlsSubscriptionServer(path => path == "/start"
            ? new Response(302, Location: crossOrigin ? second.Url + "/subscription" : "/subscription", Cookie: "session=secure; Secure; Path=/")
            : new Response(200));
        var policy = first.TrustPolicy();
        policy.CustomTrustStore.Add(second.Root);
        var downloader = new XraySubscriptionDownloader(() => executable, policy);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var result = await downloader.DownloadStringAsync(first.Url + "/start", null, "RedirectClient/1.0",
            new Dictionary<string, string> { ["Authorization"] = "Bearer secret", ["X-hwid"] = "test-device" }, "*/*", cts.Token);

        await result.Should().BeEqualTo(TlsSubscriptionServer.Body);
        var request = (crossOrigin ? second : first).Requests.Last();
        await request.Headers.ContainsKey("Authorization").Should().BeFalse();
        await request.Headers["Cookie"].Should().BeEqualTo("session=secure");
        await request.Headers["User-Agent"].Should().BeEqualTo("RedirectClient/1.0");
        await request.Headers["X-hwid"].Should().BeEqualTo("test-device");
        await request.Headers["Host"].Should().BeEqualTo(new Uri((crossOrigin ? second : first).Url).Authority);
    }

    [Test]
    [Arguments(401)]
    [Arguments(503)]
    public async Task Download_ShouldRejectHttpErrors(int status)
    {
        var executable = GetExecutable();
        await using var server = new TlsSubscriptionServer(_ => new Response(status));
        var downloader = new XraySubscriptionDownloader(() => executable, server.TrustPolicy());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        HttpRequestException? failure = null;
        try
        {
            await downloader.DownloadStringAsync(server.Url + "/subscription", null, "TestClient/1.0", null, null, cts.Token);
        }
        catch (HttpRequestException ex)
        {
            failure = ex;
        }
        await failure.Should().NotBeNull();
        await failure!.StatusCode.Should().BeEqualTo((HttpStatusCode)status);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Download_ShouldRejectUntrustedCertificatesAndWrongHostnames(bool wrongHostname)
    {
        var executable = GetExecutable();
        await using var server = new TlsSubscriptionServer();
        await using var otherAuthority = new TlsSubscriptionServer();
        var downloader = new XraySubscriptionDownloader(() => executable, wrongHostname ? server.TrustPolicy() : otherAuthority.TrustPolicy());
        var url = wrongHostname ? server.Url.Replace("localhost", "127.0.0.1") : server.Url;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var rejected = false;
        try
        {
            await downloader.DownloadStringAsync(url + "/subscription", null, "TestClient/1.0", null, null, cts.Token);
        }
        catch (HttpRequestException)
        {
            rejected = true;
        }
        await rejected.Should().BeTrue();
        await server.Requests.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Cancellation_ShouldStopTheOwnedCoreProcess()
    {
        var executable = GetExecutable();
        var existingProcesses = CoreProcessIds(executable);
        await using var server = new TlsSubscriptionServer(_ => new Response(200, Wait: true));
        var downloader = new XraySubscriptionDownloader(() => executable, server.TrustPolicy());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var download = downloader.DownloadStringAsync(server.Url + "/subscription", null, "TestClient/1.0", null, null, cts.Token);
        await server.FirstRequest.Task.WaitAsync(cts.Token);
        await cts.CancelAsync();
        var cancelled = false;
        try
        {
            await download;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        await cancelled.Should().BeTrue();
        await CoreProcessIds(executable).Except(existingProcesses).Any().Should().BeFalse();
    }

    private static string GetExecutable()
    {
        var path = Environment.GetEnvironmentVariable("V2RAYN_TEST_XRAY_PATH");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Skip.Test("Set V2RAYN_TEST_XRAY_PATH to run Xray TLS integration tests.");
        }
        return Path.GetFullPath(path!);
    }

    [Test]
    public async Task PlainHttp_ShouldKeepBasicAuthAndWorkWithoutXray()
    {
        await using var server = new TlsSubscriptionServer(useTls: false);
        var downloader = new XraySubscriptionDownloader(() => throw new InvalidOperationException("HTTP must not start Xray."), null);
        var uri = new UriBuilder(server.Url + "/subscription") { UserName = "user", Password = "password" }.Uri;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await downloader.DownloadStringAsync(uri.AbsoluteUri, null, "TestClient/1.0", null, "*/*", cts.Token);

        await result.Should().BeEqualTo(TlsSubscriptionServer.Body);
        await server.Requests.Single().Headers["Authorization"].Should().BeEqualTo("Basic dXNlcjpwYXNzd29yZA==");
    }

    [Test]
    public async Task HttpRedirect_ShouldStartTlsBridgeForHttpsTarget()
    {
        var executable = GetExecutable();
        await using var secure = new TlsSubscriptionServer();
        await using var plain = new TlsSubscriptionServer(_ => new Response(302, Location: secure.Url + "/subscription"), useTls: false);
        var downloader = new XraySubscriptionDownloader(() => executable, secure.TrustPolicy());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var result = await downloader.DownloadStringAsync(plain.Url + "/start", null, "TestClient/1.0", null, null, cts.Token);

        await result.Should().BeEqualTo(TlsSubscriptionServer.Body);
        await secure.Requests.Single().Protocol.Should().BeEqualTo(SslProtocols.Tls13);
    }

    [Test]
    public async Task HttpsRedirect_ShouldRejectDowngradingToHttp()
    {
        var executable = GetExecutable();
        await using var plain = new TlsSubscriptionServer(useTls: false);
        await using var secure = new TlsSubscriptionServer(_ => new Response(302, Location: plain.Url + "/subscription"));
        var downloader = new XraySubscriptionDownloader(() => executable, secure.TrustPolicy());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var rejected = false;
        try
        {
            await downloader.DownloadStringAsync(secure.Url + "/start", null, "TestClient/1.0", null, null, cts.Token);
        }
        catch (HttpRequestException)
        {
            rejected = true;
        }
        await rejected.Should().BeTrue();
        await plain.Requests.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task FailedCoreStartup_ShouldRemoveTemporaryConfiguration()
    {
        var existing = Directory.GetDirectories(Path.GetTempPath(), "v2rayN-subscription-*");
        var missingExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "xray");
        var downloader = new XraySubscriptionDownloader(() => missingExecutable, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var failed = false;
        try
        {
            await downloader.DownloadStringAsync("https://localhost/subscription", null, "TestClient/1.0", null, null, cts.Token);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            failed = true;
        }
        await failed.Should().BeTrue();
        await Directory.GetDirectories(Path.GetTempPath(), "v2rayN-subscription-*").Except(existing).Any().Should().BeFalse();
    }

    private static HashSet<int> CoreProcessIds(string executable)
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using (process)
            {
                ids.Add(process.Id);
            }
        }
        return ids;
    }

    private sealed record Request(string Path, Dictionary<string, string> Headers, SslProtocols Protocol);
    private sealed record Response(int Status, string? Location = null, string? Cookie = null, bool Wait = false);

    private sealed class TlsSubscriptionServer : IAsyncDisposable
    {
        internal const string Body = "subscription-test-content";
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentBag<Task> _connections = new();
        private readonly Func<string, Response> _respond;
        private readonly bool _useTls;
        private readonly X509Certificate2 _certificate;
        private readonly Task _serverTask;
        internal X509Certificate2 Root { get; }
        internal string Url { get; }
        internal ConcurrentQueue<Request> Requests { get; } = new();
        internal TaskCompletionSource FirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TlsSubscriptionServer(Func<string, Response>? respond = null, bool useTls = true)
        {
            _respond = respond ?? (_ => new Response(200));
            _useTls = useTls;
            using var rootKey = RSA.Create(2048);
            var rootRequest = new CertificateRequest("CN=Subscription Test CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            Root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
            using var leafKey = RSA.Create(2048);
            var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            leafRequest.CertificateExtensions.Add(names.Build());
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            using var leaf = leafRequest.Create(Root, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            using var withKey = leaf.CopyWithPrivateKey(leafKey);
            // Schannel's server credentials need a PKCS#12 import rather than the generated ephemeral key.
            _certificate = X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
            _listener.Start();
            Url = $"{(useTls ? "https" : "http")}://localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _serverTask = ServeAsync();
        }

        internal X509ChainPolicy TrustPolicy()
        {
            var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust };
            policy.CustomTrustStore.Add(Root);
            return policy;
        }

        private async Task ServeAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                _connections.Add(HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                await using Stream stream = _useTls ? new System.Net.Security.SslStream(client.GetStream()) : client.GetStream();
                try
                {
                    var token = _cancellation.Token;
                    if (stream is System.Net.Security.SslStream ssl)
                    {
                        await ssl.AuthenticateAsServerAsync(new System.Net.Security.SslServerAuthenticationOptions
                        {
                            ServerCertificate = _certificate, EnabledSslProtocols = SslProtocols.Tls13,
                            ApplicationProtocols = [System.Net.Security.SslApplicationProtocol.Http11]
                        }, token);
                    }
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var line = await reader.ReadLineAsync(token);
                    if (line == null)
                    {
                        return;
                    }
                    var path = line.Split(' ')[1];
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadLineAsync(token) is { Length: > 0 } header)
                    {
                        var colon = header.IndexOf(':');
                        headers[header[..colon]] = header[(colon + 1)..].Trim();
                    }
                    Requests.Enqueue(new Request(path, headers, (stream as System.Net.Security.SslStream)?.SslProtocol ?? SslProtocols.None));
                    FirstRequest.TrySetResult();
                    var response = _respond(path);
                    if (response.Wait)
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }
                    using var bytes = new MemoryStream();
                    using (var gzip = new GZipStream(bytes, CompressionMode.Compress, leaveOpen: true))
                    {
                        await gzip.WriteAsync(Encoding.UTF8.GetBytes(Body), token);
                    }
                    var extra = (response.Location == null ? "" : $"Location: {response.Location}\r\n")
                        + (response.Cookie == null ? "" : $"Set-Cookie: {response.Cookie}\r\n");
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {response.Status} Test\r\n{extra}Content-Encoding: gzip\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), token);
                    await stream.WriteAsync(bytes.ToArray(), token);
                }
                catch (Exception ex) when (ex is AuthenticationException or IOException or OperationCanceledException)
                {
                    // Rejected certificates and cancelled requests close the connection before HTTP completes.
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try { await _serverTask; } catch (OperationCanceledException) { }
            await Task.WhenAll(_connections);
            _certificate.Dispose();
            Root.Dispose();
            _cancellation.Dispose();
        }
    }

    private sealed class SocksProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentBag<Task> _connections = new();
        private readonly Task _serverTask;
        private int _connectionCount;
        internal int Connections => _connectionCount;
        internal string Url { get; }

        internal SocksProxy()
        {
            _listener.Start();
            Url = $"socks5://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _serverTask = ServeAsync();
        }

        private async Task ServeAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                _connections.Add(HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            using (var upstream = new TcpClient())
            {
                try
                {
                    var token = _cancellation.Token;
                    var stream = client.GetStream();
                    var greeting = new byte[2];
                    await stream.ReadExactlyAsync(greeting, token);
                    await stream.ReadExactlyAsync(new byte[greeting[1]], token);
                    await stream.WriteAsync(new byte[] { 5, 0 }, token);
                    var request = new byte[4];
                    await stream.ReadExactlyAsync(request, token);
                    var address = new byte[request[3] == 1 ? 4 : request[3] == 4 ? 16 : 1];
                    await stream.ReadExactlyAsync(address, token);
                    string host;
                    if (request[3] == 3)
                    {
                        var domain = new byte[address[0]];
                        await stream.ReadExactlyAsync(domain, token);
                        host = Encoding.ASCII.GetString(domain);
                    }
                    else
                    {
                        host = new IPAddress(address).ToString();
                    }
                    var port = new byte[2];
                    await stream.ReadExactlyAsync(port, token);
                    await upstream.ConnectAsync(host, (port[0] << 8) | port[1], token);
                    Interlocked.Increment(ref _connectionCount);
                    await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, token);
                    var remote = upstream.GetStream();
                    using var relayCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var send = stream.CopyToAsync(remote, relayCts.Token);
                    var receive = remote.CopyToAsync(stream, relayCts.Token);
                    await Task.WhenAny(send, receive);
                    await relayCts.CancelAsync();
                    await Task.WhenAll(send, receive);
                }
                catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
                {
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try { await _serverTask; } catch (OperationCanceledException) { }
            await Task.WhenAll(_connections);
            _cancellation.Dispose();
        }
    }
}
