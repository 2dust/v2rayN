using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ServiceLib.Tests.CoreConfig.Singbox;

[NotInParallel]
public class SshConnectionIntegrationTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private sealed class Progress(Action<SshConnectionTestResult> report) : IProgress<SshConnectionTestResult>
    {
        public void Report(SshConnectionTestResult value) => report(value);
    }
    private static async Task<string> Docker(params string[] args)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        catch { process.Kill(true); throw; }
        Check(process.ExitCode == 0, "Disposable Docker fixture failed: " + await error);
        return await output;
    }

    [Test]
    public async Task RealDraftAuthenticationForwardingFailuresAndCleanup()
    {
        var image = Environment.GetEnvironmentVariable("SSH_SETUP_DOCKER_IMAGE");
        var core = Environment.GetEnvironmentVariable("SSH_TEST_SINGBOX");
        if (string.IsNullOrEmpty(image) || string.IsNullOrEmpty(core))
        { Skip.Test("Set SSH_SETUP_DOCKER_IMAGE and SSH_TEST_SINGBOX for isolated draft connection tests."); return; }
        var name = "v2rayn-connection-test-" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), name);
        var started = false;
        var baseline = Process.GetProcessesByName("sing-box").Select(p => { using (p) return p.Id; }).ToHashSet();
        try
        {
            await Docker("run", "--detach", "--rm", "--name", name, "--publish", "127.0.0.1::22", image);
            started = true;
            var binding = (await Docker("port", name, "22/tcp")).Trim();
            var port = int.Parse(binding[(binding.LastIndexOf(':') + 1)..]);
            SshHostKey? host = null;
            for (var i = 0; i < 60; i++)
            {
                try { host = await SshProfileService.FetchHostKeyAsync("127.0.0.1", port, default); break; }
                catch when (i < 59) { await Task.Delay(100); }
            }
            Check(host != null, "fixture host key ready");
            var node = SshTests.Node();
            node.IndexId = ""; node.Port = port; node.Username = "fixture"; node.Password = "fixture-only-password";
            node.SetProtocolExtra(node.GetProtocolExtra() with
            {
                SshHostKey = host!.PublicKey, SshHostKeyAlgorithm = host.Algorithm,
                SshTrustedPort = port,
            });
            var service = new SshConnectionTestService(core, root);
            const string http = "http://127.0.0.1:18080/";
            var before = JsonUtils.Serialize(node);
            var events = new List<SshConnectionTestResult>();
            var good = await service.TestAsync(node, http, new Progress(events.Add), default);
            Check(good.Login.State == SshTestState.Passed && good.Proxy.State == SshTestState.Passed, "password + real proxy: " + JsonUtils.Serialize(good));
            Check(events.Any(e => e.Login.State == SshTestState.Passed && e.Proxy.State == SshTestState.Running), "login visible before forwarding");
            Check(JsonUtils.Serialize(node) == before, "draft unchanged");
            Clean();

            var denied = await service.TestAsync(node, http + "denied", null, default);
            Check(denied.Proxy.State == SshTestState.Responded && denied.Proxy.HttpStatus == 403, "HTTP denial is target response, not authentication error");
            var tls = await service.TestAsync(node, "https://127.0.0.1:18443/", null, default);
            Check(tls.Login.State == SshTestState.Passed && tls.Proxy.Error == "tls", "self-signed TLS rejected by default");
            using var certificate = X509Certificate2.CreateFromPem(await Docker("exec", name, "cat", "/tmp/cert.pem"));
            // Trust only this fixture certificate in this handler; retain hostname and chain validation.
            var trustedTls = new SshConnectionTestService(core, root)
            {
                CreateHttpHandler = () => new SocketsHttpHandler
                {
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        CertificateChainPolicy = new X509ChainPolicy
                        {
                            TrustMode = X509ChainTrustMode.CustomRootTrust, CustomTrustStore = { certificate },
                            RevocationMode = X509RevocationMode.NoCheck,
                        },
                    },
                },
            };
            var secure = await trustedTls.TestAsync(node, "https://127.0.0.1:18443/", null, default);
            Check(secure.Proxy.State == SshTestState.Passed, "HTTPS via SSH with trusted fixture CA: " + JsonUtils.Serialize(secure));

            var wrong = JsonUtils.DeepCopy(node); wrong.Password = "wrong-test-secret";
            var refused = await service.TestAsync(wrong, http, null, default);
            Check(refused.Login.Error == "authentication" && refused.Proxy.State == SshTestState.Skipped, "wrong password skips core");
            Check(!JsonUtils.Serialize(refused).Contains(wrong.Password), "no credential in result");
            wrong = JsonUtils.DeepCopy(node);
            wrong.SetProtocolExtra(wrong.GetProtocolExtra() with { SshHostKey = SshTests.TestKey });
            var mismatch = await service.TestAsync(wrong, http, null, default);
            Check(mismatch.Login.Error == "hostKey" && mismatch.Proxy.State == SshTestState.Skipped, "changed pin fails closed");
            var missing = await new SshConnectionTestService(Path.Combine(root, "missing"), root).TestAsync(node, http, null, default);
            Check(missing.Login.State == SshTestState.Passed && missing.Proxy.Error == "coreMissing", "missing core preserves login result");
            if (!OperatingSystem.IsWindows())
            {
                var exited = await new SshConnectionTestService("/usr/bin/false", root).TestAsync(node, http, null, default);
                Check(exited.Login.State == SshTestState.Passed && exited.Proxy.Error == "coreStart", "exited/incompatible core is distinct");
            }

            var store = new SshKeyStore(Path.Combine(root, "keys"));
            var target = new SshSetupTarget(node.Address, node.Port, node.Username, host);
            var installed = await new SshKeyInstaller(store).ConfigureAsync(target, null, true, "fixture-passphrase", node.Password, null, default);
            Check(installed.Verified, "fixture key installed");
            var keyNode = JsonUtils.DeepCopy(node);
            keyNode.Password = "must-not-fallback";
            keyNode.SetProtocolExtra(keyNode.GetProtocolExtra() with
            {
                SshPrivateKeyAuth = true, SshPrivateKeyPath = installed.Key!.Path, SshPrivateKeyPassphrase = "fixture-passphrase",
            });
            var authorized = await Docker("exec", name, "cat", "/home/fixture/.ssh/authorized_keys");
            var keyResult = await service.TestAsync(keyNode, http, null, default);
            Check(keyResult.Login.State == SshTestState.Passed && keyResult.Proxy.State == SshTestState.Passed, "encrypted private key both stages");
            keyNode.SetProtocolExtra(keyNode.GetProtocolExtra() with { SshPrivateKeyPassphrase = "wrong-passphrase" });
            var locked = await service.TestAsync(keyNode, http, null, default);
            Check(locked.Login.Error == "invalidKey" && locked.Proxy.State == SshTestState.Skipped, "wrong passphrase never uses password");
            Check(await Docker("exec", name, "cat", "/home/fixture/.ssh/authorized_keys") == authorized, "testing never changes authorized_keys");
            Check(store.GetDefault() == null, "testing never saves shared default");

            var timeout = await service.TestAsync(node, http + "slow", null, default);
            Check(timeout.Login.State == SshTestState.Passed && timeout.Proxy.Error == "timeout", "HTTP deadline");
            Clean();
            using (var stop = new CancellationTokenSource())
            {
                var test = service.TestAsync(node, http + "slow", null, stop.Token);
                string? file = null;
                for (var i = 0; i < 100 && !test.IsCompleted; i++)
                {
                    file = Directory.Exists(root) ? Directory.GetFiles(root, "config.json", SearchOption.AllDirectories).FirstOrDefault() : null;
                    if (file != null) break;
                    await Task.Delay(50);
                }
                Check(file != null, "test creates private temporary config");
                if (!OperatingSystem.IsWindows())
                {
                    Check(File.GetUnixFileMode(file!) == (UnixFileMode)384, "temporary config 0600");
                    Check(File.GetUnixFileMode(Path.GetDirectoryName(file!)!) == (UnixFileMode)448, "temporary directory 0700");
                }
                await Task.Delay(200);
                stop.Cancel();
                var stopped = await test.WaitAsync(TimeSpan.FromSeconds(3));
                Check(stopped.Proxy.State == SshTestState.Cancelled && stopped.Login.State == SshTestState.Passed, "stop during proxy request retains login stage");
            }
            Clean();
            Check((await service.TestAsync(node, http, null, default)).Proxy.State == SshTestState.Passed, "retry after cancellation");
            await Docker("exec", name, "sh", "-c", "sed -i 's/AllowTcpForwarding yes/AllowTcpForwarding no/' /etc/ssh/sshd_config; pkill -HUP -x sshd");
            var noForward = await service.TestAsync(node, http, null, default);
            // SIGHUP briefly closes the listening socket while sshd re-executes.
            // Retry only that fixture startup failure; forwarding assertions still run once login succeeds.
            for (var i = 0; i < 50 && noForward.Login.Error == "connection"; i++)
            {
                await Task.Delay(100);
                noForward = await service.TestAsync(node, http, null, default);
            }
            Check(noForward.Login.State == SshTestState.Passed && noForward.Proxy.Error == "forwarding", "authentication success does not imply forwarding permission: " + JsonUtils.Serialize(noForward));
            Clean();

            void Clean()
            {
                Check(!Directory.Exists(root) || Directory.GetDirectories(root, "v2rayn-ssh-test-*").Length == 0, "no temporary credential files left");
                var after = Process.GetProcessesByName("sing-box").Select(p => { using (p) return p.Id; }).ToHashSet();
                Check(after.SetEquals(baseline), "only test process stopped; running core preserved");
            }
        }
        finally
        {
            if (started) await Docker("rm", "--force", name);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
