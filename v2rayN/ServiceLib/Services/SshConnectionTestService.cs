using System.Net.Http;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace ServiceLib.Services;

public enum SshTestState { Waiting, Running, Passed, Responded, Failed, Cancelled, Skipped }
public record SshTestStage(SshTestState State = SshTestState.Waiting, long ElapsedMs = 0, string? Error = null, int? HttpStatus = null);
public record SshConnectionTestResult(string Url, SshTestStage Login, SshTestStage Proxy);

public interface ISshConnectionTestService
{
    Task<SshConnectionTestResult> TestAsync(ProfileItem snapshot, string url,
        IProgress<SshConnectionTestResult>? progress, CancellationToken token);
}

/// <summary>Never saves a profile or touches CoreManager/the running proxy. Diagnostics contain only bounded error codes.</summary>
public sealed class SshConnectionTestService(string? corePath = null, string? temporaryRoot = null) : ISshConnectionTestService
{
    internal Func<SocketsHttpHandler> CreateHttpHandler { get; init; } = () => new();
    public async Task<SshConnectionTestResult> TestAsync(ProfileItem snapshot, string url,
        IProgress<SshConnectionTestResult>? progress, CancellationToken token)
    {
        // A caller may keep editing its object: this service owns its own frozen copy.
        var node = JsonUtils.DeepCopy(snapshot);
        var result = new SshConnectionTestResult(DisplayUrl(url), new(SshTestState.Running), new());
        var watch = Stopwatch.StartNew();
        var proxyStage = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        void Report() => progress?.Report(result);
        Report();
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUrl) || targetUrl.Scheme is not ("http" or "https")
                || targetUrl.UserInfo.Length != 0) throw new SshSetupException("testUrl");
            if (SshProfileService.Validate(node) != null) throw new SshSetupException("configuration");
            var extra = node.GetProtocolExtra();
            var target = new SshSetupTarget(node.Address, node.Port, node.Username,
                SshProfileService.ParseHostKey(extra.SshHostKey!, extra.SshHostKeyAlgorithm));
            if (extra.SshPrivateKeyAuth == true)
            {
                var store = new SshKeyStore();
                using var key = store.Open(new(extra.SshPrivateKeyPath!, null, null, null, false), extra.SshPrivateKeyPassphrase);
                using var authentication = new PrivateKeyAuthenticationMethod(node.Username, key);
                using var client = await SshPinnedConnection.ConnectAsync(target, authentication, deadline.Token);
            }
            else
            {
                using var authentication = new PasswordAuthenticationMethod(node.Username, node.Password);
                using var client = await SshPinnedConnection.ConnectAsync(target, authentication, deadline.Token);
            }
            result = result with { Login = new(SshTestState.Passed, watch.ElapsedMilliseconds), Proxy = new(SshTestState.Running) };
            proxyStage = true;
            watch.Restart();
            Report();
            var status = await TestProxyAsync(node, targetUrl, deadline.Token);
            result = result with { Proxy = new(status is >= 200 and <= 299 ? SshTestState.Passed : SshTestState.Responded,
                watch.ElapsedMilliseconds, HttpStatus: status) };
        }
        catch (Exception error)
        {
            var code = token.IsCancellationRequested ? "cancelled" : deadline.IsCancellationRequested ? "timeout" : error switch
            {
                SshSetupException known => known.Code,
                OperationCanceledException or SshOperationTimeoutException => "timeout",
                SshAuthenticationException => "authentication",
                HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => "tls",
                HttpRequestException => "forwarding",
                IOException or UnauthorizedAccessException => "storage",
                _ => proxyStage ? "coreStart" : "connection",
            };
            var stage = new SshTestStage(code == "cancelled" ? SshTestState.Cancelled : SshTestState.Failed,
                watch.ElapsedMilliseconds, code);
            result = proxyStage ? result with { Proxy = stage }
                : result with { Login = stage, Proxy = new(SshTestState.Skipped) };
        }
        Report();
        return result;
    }

    private async Task<int> TestProxyAsync(ProfileItem node, Uri url, CancellationToken token)
    {
        var executable = corePath ?? CoreInfoManager.Instance.GetCoreExecFile(CoreInfoManager.Instance.GetCoreInfo(ECoreType.sing_box), out _);
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable)) throw new SshSetupException("coreMissing");
        var directory = Path.Combine(temporaryRoot ?? Path.GetTempPath(), "v2rayn-ssh-test-" + Guid.NewGuid().ToString("N"));
        Process? process = null;
        Task? output = null, errors = null;
        try
        {
            new SshKeyStore(directory).EnsureDirectory();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var password = Guid.NewGuid().ToString("N");
            var config = new CoreConfigSingboxService(new() { Node = node, RunCoreType = ECoreType.sing_box })
                .GenerateSshConnectionTestConfig(port, password);
            var path = Path.Combine(directory, "config.json");
            using (var stream = SshKeyStore.CreatePrivateFile(path))
                await stream.WriteAsync(Encoding.UTF8.GetBytes(config), token);
            token.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = directory, RedirectStandardError = true, RedirectStandardOutput = true,
            };
            info.ArgumentList.Add("run"); info.ArgumentList.Add("-c"); info.ArgumentList.Add(path);
            process = Process.Start(info) ?? throw new SshSetupException("coreStart");
            // Drain bounded buffers; raw core diagnostics may echo credentials/configuration, so never retain or log them.
            output = DrainAsync(process.StandardOutput);
            errors = DrainAsync(process.StandardError);
            using (var startup = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                startup.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    while (true)
                    {
                        if (process.HasExited) throw new SshSetupException("coreStart");
                        try
                        {
                            using var socket = new TcpClient();
                            await socket.ConnectAsync(IPAddress.Loopback, port, startup.Token);
                            break;
                        }
                        catch (SocketException) { await Task.Delay(50, startup.Token); }
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new SshSetupException("coreStart"); }
            }
            using var handler = CreateHttpHandler();
            handler.UseProxy = true;
            handler.Proxy = new WebProxy($"socks5://127.0.0.1:{port}") { Credentials = new NetworkCredential("probe", password) };
            handler.UseCookies = false;
            handler.AllowAutoRedirect = false;
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                return (int)response.StatusCode;
            }
            catch (HttpRequestException) when (process.HasExited) { throw new SshSetupException("coreStart"); }
        }
        finally
        {
            try
            {
                if (process != null)
                {
                    if (!process.HasExited) process.Kill(true);
                    await process.WaitForExitAsync();
                    if (output != null) await output;
                    if (errors != null) await errors;
                }
            }
            finally
            {
                process?.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[1024];
        while (await reader.ReadAsync(buffer) != 0) { }
    }

    private static string DisplayUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return "";
        // Never echo embedded credentials or query tokens into the result/diagnostics.
        return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
    }
}
