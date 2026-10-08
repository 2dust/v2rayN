using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace ServiceLib.Services;

/// <summary>
/// Keeps HTTP in .NET while a separate Xray process encrypts HTTPS connections.
/// The bridge accepts only local SOCKS connections and never receives a subscription URL in its arguments or configuration.
/// </summary>
internal sealed class XraySubscriptionDownloader(Func<string> getCoreExecutable, X509ChainPolicy? certificateChainPolicy)
{
    private const int MaxRedirects = 50;

    internal static bool IsRequired()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        var info = new WindowsVersionInfo { Size = Marshal.SizeOf<WindowsVersionInfo>(), ServicePack = "" };
        return RtlGetVersion(ref info) == 0
            && IsRequired(true, new Version(info.Major, info.Minor, info.Build), info.ProductType);
    }

    internal static bool IsRequired(bool isWindows, Version version, byte productType)
    {
        // VER_NT_WORKSTATION excludes Server and domain controllers that share Windows 10's version number.
        return isWindows && productType == 1 && version.Major == 10 && version.Minor == 0
            && version.Build >= 10240 && version.Build < 22000;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowsVersionInfo
    {
        public int Size;
        public int Major;
        public int Minor;
        public int Build;
        public int PlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ServicePack;
        public ushort ServicePackMajor;
        public ushort ServicePackMinor;
        public ushort SuiteMask;
        public byte ProductType;
        public byte Reserved;
    }

    [DllImport("ntdll.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RtlGetVersion(ref WindowsVersionInfo version);

    internal async Task<string> DownloadStringAsync(string url, WebProxy? proxy, string userAgent,
        IReadOnlyDictionary<string, string>? headers, string? acceptHeader, CancellationToken cancellationToken)
    {
        var originalUri = new Uri(url);
        var target = originalUri;
        var cookies = new CookieContainer();
        XrayTlsBridge? bridge = null;
        HttpClient? tlsClient = null;
        using var plainClient = CreateClient(proxy);
        try
        {
            for (var redirectCount = 0; ; redirectCount++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
                {
                    throw new HttpRequestException("Unsupported subscription URL scheme.");
                }

                var client = plainClient;
                var requestUri = target;
                if (target.Scheme == Uri.UriSchemeHttps)
                {
                    bridge ??= await XrayTlsBridge.StartAsync(getCoreExecutable(), proxy, certificateChainPolicy, cancellationToken);
                    tlsClient ??= CreateClient(new WebProxy($"socks5://127.0.0.1:{bridge.Port}"));
                    client = tlsClient;
                    // Keep the destination host and HTTPS port for SOCKS; Xray supplies the TLS layer.
                    requestUri = new UriBuilder(target) { Scheme = Uri.UriSchemeHttp, Port = target.Port, UserName = "", Password = "" }.Uri;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri)
                {
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact
                };
                request.Headers.Host = target.Authority;
                request.Headers.UserAgent.TryParseAdd(userAgent);
                if (acceptHeader.IsNotEmpty())
                {
                    request.Headers.Accept.ParseAdd(acceptHeader);
                }
                if (redirectCount == 0 && originalUri.UserInfo.IsNotEmpty())
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(originalUri.UserInfo));
                }
                var cookieHeader = cookies.GetCookieHeader(target);
                if (cookieHeader.IsNotEmpty())
                {
                    request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                }
                HttpRequestHeadersHelper.ApplyHeaders(request, headers);
                // Match HttpClient's redirect handling: explicit Authorization is cleared after a redirect.
                if (redirectCount > 0)
                {
                    request.Headers.Authorization = null;
                }
                if (!SameOrigin(originalUri, target))
                {
                    request.Headers.Host = target.Authority;
                    request.Headers.Remove("Cookie");
                    if (cookieHeader.IsNotEmpty())
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                    }
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    foreach (var cookie in setCookies)
                    {
                        try
                        {
                            // Use the real HTTPS origin so Secure cookies survive the local HTTP bridge.
                            cookies.SetCookies(target, cookie);
                        }
                        catch (CookieException)
                        {
                            // HttpClient also ignores malformed response cookies.
                        }
                    }
                }
                if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
                {
                    if (redirectCount >= MaxRedirects)
                    {
                        throw new HttpRequestException("Too many subscription redirects.");
                    }
                    var next = new Uri(target, location);
                    if (target.Scheme == Uri.UriSchemeHttps && next.Scheme == Uri.UriSchemeHttp)
                    {
                        throw new HttpRequestException("HTTPS subscription redirected to HTTP.");
                    }
                    target = next;
                    continue;
                }
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null && bridge != null)
        {
            var failedBridge = bridge;
            bridge = null;
            await failedBridge.DisposeAsync();
            throw new HttpRequestException("Xray subscription transport failed: " + failedBridge.Diagnostics, ex);
        }
        finally
        {
            tlsClient?.Dispose();
            if (bridge != null)
            {
                await bridge.DisposeAsync();
            }
        }
    }

    private static HttpClient CreateClient(IWebProxy? proxy)
    {
        return new HttpClient(new SocketsHttpHandler
        {
            Proxy = proxy,
            UseProxy = proxy != null,
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = proxy == null ? Global.DirectDownloadConnect : Global.ProxyDownloadConnect
        }) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static bool SameOrigin(Uri left, Uri right)
    {
        return left.Scheme == right.Scheme && left.IdnHost.Equals(right.IdnHost, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;
    }

    private static bool IsRedirect(HttpStatusCode status)
    {
        return status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect or HttpStatusCode.MultipleChoices;
    }

    private sealed class XrayTlsBridge : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _directory;
        private Task<string>? _standardOutput;
        private Task<string>? _standardError;
        internal int Port { get; }
        internal string Diagnostics => string.Join(Environment.NewLine,
            ((_standardError?.Result ?? "") + (_standardOutput?.Result ?? ""))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.Contains("proxy/socks: failed to read request", StringComparison.Ordinal))
                .TakeLast(3));

        private XrayTlsBridge(string executable)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            _directory = Directory.CreateTempSubdirectory("v2rayN-subscription-").FullName;
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = _directory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
            _process.StartInfo.ArgumentList.Add("run");
            _process.StartInfo.ArgumentList.Add("-c");
            _process.StartInfo.ArgumentList.Add(Path.Combine(_directory, "config.json"));
        }

        internal static async Task<XrayTlsBridge> StartAsync(string executable, WebProxy? proxy,
            X509ChainPolicy? chainPolicy, CancellationToken cancellationToken)
        {
            var bridge = new XrayTlsBridge(executable);
            try
            {
                var config = CreateConfiguration(bridge.Port, proxy, chainPolicy);
                await File.WriteAllTextAsync(Path.Combine(bridge._directory, "config.json"), config.ToJsonString(), cancellationToken);
                bridge._process.Start();
                bridge._standardOutput = bridge._process.StandardOutput.ReadToEndAsync();
                bridge._standardError = bridge._process.StandardError.ReadToEndAsync();
                using var startupCts = new CancellationTokenSource(Global.LocalFetch);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, startupCts.Token);
                while (true)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    if (bridge._process.HasExited)
                    {
                        throw new InvalidOperationException("Xray subscription bridge exited: "
                            + await bridge._standardError + await bridge._standardOutput);
                    }
                    using var socket = new TcpClient();
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                    attemptCts.CancelAfter(TimeSpan.FromMilliseconds(100));
                    try
                    {
                        await socket.ConnectAsync(IPAddress.Loopback, bridge.Port, attemptCts.Token);
                        var stream = socket.GetStream();
                        await stream.WriteAsync(new byte[] { 5, 1, 0 }, attemptCts.Token);
                        var reply = new byte[2];
                        await stream.ReadExactlyAsync(reply, attemptCts.Token);
                        if (reply[0] == 5 && reply[1] == 0 && !bridge._process.HasExited)
                        {
                            return bridge;
                        }
                    }
                    catch (SocketException) { }
                    catch (OperationCanceledException) when (!linkedCts.IsCancellationRequested) { }
                    await Task.Delay(25, linkedCts.Token);
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                await bridge.DisposeAsync();
                throw new TimeoutException("Timed out waiting for the Xray subscription bridge.", ex);
            }
            catch
            {
                await bridge.DisposeAsync();
                throw;
            }
        }

        private static JsonObject CreateConfiguration(int port, WebProxy? proxy, X509ChainPolicy? chainPolicy)
        {
            var tls = new JsonObject
            {
                ["allowInsecure"] = false,
                ["alpn"] = new JsonArray("http/1.1"),
                ["minVersion"] = "1.2"
                // SNI and certificate hostname validation are inferred from the SOCKS destination.
            };
            if (chainPolicy?.TrustMode == X509ChainTrustMode.CustomRootTrust)
            {
                tls["disableSystemRoot"] = true;
                var certificates = new JsonArray();
                foreach (var certificate in chainPolicy.CustomTrustStore)
                {
                    certificates.Add(new JsonObject
                    {
                        ["usage"] = "verify",
                        ["certificate"] = JsonSerializer.SerializeToNode(CertPemManager.ExportCertToPem(certificate).Split('\n'))
                    });
                }
                tls["certificates"] = certificates;
            }
            var streamSettings = new JsonObject { ["network"] = "tcp", ["security"] = "tls", ["tlsSettings"] = tls };
            var outbounds = new JsonArray(new JsonObject
            {
                ["tag"] = "subscription-tls", ["protocol"] = "freedom", ["settings"] = new JsonObject(), ["streamSettings"] = streamSettings
            });
            if (proxy?.Address is { } address)
            {
                if (address.Scheme != "socks5" && address.Scheme != "http")
                {
                    throw new NotSupportedException("Unsupported subscription proxy scheme.");
                }
                streamSettings["sockopt"] = new JsonObject { ["dialerProxy"] = "subscription-proxy" };
                var server = new JsonObject { ["address"] = address.IdnHost, ["port"] = address.Port };
                if (proxy.Credentials?.GetCredential(address, "Basic") is { } credentials)
                {
                    server["users"] = new JsonArray(new JsonObject { ["user"] = credentials.UserName, ["pass"] = credentials.Password });
                }
                outbounds.Add(new JsonObject
                {
                    ["tag"] = "subscription-proxy", ["protocol"] = address.Scheme == "socks5" ? "socks" : "http",
                    ["settings"] = new JsonObject { ["servers"] = new JsonArray(server) }
                });
            }
            return new JsonObject
            {
                ["log"] = new JsonObject { ["loglevel"] = "info" },
                ["inbounds"] = new JsonArray(new JsonObject
                {
                    ["listen"] = "127.0.0.1", ["port"] = port, ["protocol"] = "socks",
                    ["settings"] = new JsonObject { ["auth"] = "noauth", ["udp"] = false }
                }),
                ["outbounds"] = outbounds
            };
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_standardOutput != null && !_process.HasExited)
                {
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (_process.HasExited)
                    {
                        // The core can exit between the state check and Kill.
                    }
                    await _process.WaitForExitAsync();
                }
                if (_standardOutput != null)
                {
                    await _standardOutput;
                    await _standardError!;
                }
            }
            finally
            {
                _process.Dispose();
                File.Delete(Path.Combine(_directory, "config.json"));
                Directory.Delete(_directory);
            }
        }
    }
}
