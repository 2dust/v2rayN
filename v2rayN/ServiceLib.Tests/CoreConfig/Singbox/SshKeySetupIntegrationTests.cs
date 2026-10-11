using System.Net.Http;
using ReactiveUI.Primitives;
using Renci.SshNet;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.CoreConfig.Singbox;

/// <summary>Opt-in real OpenSSH tests. Every server mutation targets a newly created disposable container.</summary>
[NotInParallel]
public class SshKeySetupIntegrationTests
{
    private sealed class InlineProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    private static async Task<string> Docker(params string[] arguments)
    {
        var info = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        catch { process.Kill(true); throw; }
        Check(process.ExitCode == 0, "Disposable fixture command failed: " + await error);
        return await output;
    }

    [Test]
    public async Task RealOpenSshInstallationRecoveryAndSingboxForwarding()
    {
        var image = Environment.GetEnvironmentVariable("SSH_SETUP_DOCKER_IMAGE");
        var singbox = Environment.GetEnvironmentVariable("SSH_TEST_SINGBOX");
        if (string.IsNullOrEmpty(image) || string.IsNullOrEmpty(singbox))
        {
            Skip.Test("Set SSH_SETUP_DOCKER_IMAGE and SSH_TEST_SINGBOX to run disposable real OpenSSH key setup tests.");
            return;
        }
        var name = "v2rayn-key-test-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), name);
        var store = new SshKeyStore(Path.Combine(directory, "sshKeys"));
        var started = false;
        try
        {
            await Docker("run", "--detach", "--rm", "--name", name, "--publish", "127.0.0.1::22", image);
            started = true;
            var binding = (await Docker("port", name, "22/tcp")).Trim();
            var port = int.Parse(binding[(binding.LastIndexOf(':') + 1)..]);
            string hostKey = "";
            for (var attempt = 0; attempt < 60; attempt++)
            {
                try
                {
                    hostKey = await Docker("exec", name, "cat", "/etc/ssh/ssh_host_ed25519_key.pub");
                    using var ready = new TcpClient();
                    await ready.ConnectAsync(IPAddress.Loopback, port);
                    var buffer = new byte[64];
                    using var timeout = new CancellationTokenSource(500);
                    var count = await ready.GetStream().ReadAsync(buffer, timeout.Token);
                    if (Encoding.ASCII.GetString(buffer, 0, count).StartsWith("SSH-")) break;
                }
                catch when (attempt < 59) { }
                await Task.Delay(100);
            }
            var target = new SshSetupTarget("127.0.0.1", port, "fixture", SshProfileService.ParseHostKey(hostKey));
            var installer = new SshKeyInstaller(store);
            const string password = "fixture-only-password";

            var wrongPassword = await installer.ConfigureAsync(target, null, true, null, "wrong", null, default);
            Check(!wrongPassword.Verified && wrongPassword.Key == null && wrongPassword.Error == "authentication", "password must succeed before generation");
            var wrongHost = target with { HostKey = SshProfileService.ParseHostKey(SshTests.TestKey) };
            var mismatch = await installer.ConfigureAsync(wrongHost, null, true, null, password, null, default);
            Check(mismatch.Error == "hostKey" && mismatch.Key == null, "host mismatch must fail before credentials/generation");

            var first = await installer.ConfigureAsync(target, null, true, null, password, null, default);
            Check(first.Verified && first.Key?.PublicKey != null, "password / generation / install / independent key login: " + first.Error);
            var key = first.Key!;
            var failedRegeneration = await installer.ConfigureAsync(target, key, true, null, "wrong", null, default);
            Check(failedRegeneration.Key == null && failedRegeneration.Error == "authentication", "failed explicit regeneration must not fall back to the highlighted existing key");
            var original = await ReadAuthorized();
            Check(original.Trim() == key.PublicKey, "exact public key appended");
            Check((await Docker("exec", name, "stat", "-c", "%a", "/home/fixture/.ssh", "/home/fixture/.ssh/authorized_keys")).Trim() == "700\n600", "remote permissions");
            Check(store.GetDefault() == null, "setup must not save a node or default key");

            var reuse = await installer.ConfigureAsync(target, store.Inspect(key.Path), false, null, null, null, default);
            Check(reuse.Verified && !reuse.RemoteMayHaveChanged, "authorized key needs no password or remote write");
            Check(await ReadAuthorized() == original, "reuse preserves authorization file");
            await TestForwarding(null, null);
            await TestForwarding(key, null);

            var another = await store.GenerateAsync("key-passphrase", default);
            var needsPassword = await installer.ConfigureAsync(target, another, false, "key-passphrase", null, null, default);
            Check(needsPassword.NeedsPassword && !needsPassword.Verified, "uninstalled local key prompts for account password");
            var wrongPassphrase = await installer.ConfigureAsync(target, another, false, "wrong-key-passphrase", password, null, default);
            Check(wrongPassphrase.Error == "invalidKey" && !wrongPassphrase.RemoteMayHaveChanged, "bad local passphrase never writes remotely");
            var installed = await installer.ConfigureAsync(target, another, false, "key-passphrase", password, null, default);
            Check(installed.Verified, "encrypted key installation: " + installed.Error);
            await TestForwarding(another, "key-passphrase");

            // Keep an unrelated key, options, comments, and a missing final newline byte-for-byte.
            var restricted = "# untouched\ncommand=\"echo preserved comment\",no-pty " + key.PublicKey + " note";
            await WriteAuthorized(restricted);
            using (var client = PasswordClient())
            {
                client.Connect();
                var writes = 0;
                await SshAuthorizedKeys.InstallAsync(client, Guid.NewGuid().ToString(), key.PublicKey!, () => writes++, default);
                Check(writes == 0 && await ReadAuthorized() == restricted, "existing restricted keys must not acquire an unrestricted duplicate");
            }
            var appended = await installer.ConfigureAsync(target, another, false, "key-passphrase", password, null, default);
            Check(appended.Verified, "append after missing final newline");
            Check(await ReadAuthorized() == restricted + "\n" + another.PublicKey + "\n", "old prefix and restrictions preserved");

            // Read/write conflict detection operates inside a single helper session.
            var conflictKey = await store.GenerateAsync(null, default);
            using (var client = PasswordClient())
            {
                client.Connect();
                try
                {
                    await SshAuthorizedKeys.InstallAsync(client, Guid.NewGuid().ToString(), conflictKey.PublicKey!, () =>
                    {
                        Task.Run(() => Docker("exec", name, "sh", "-c", "printf '\\n# concurrent edit\\n' >> /home/fixture/.ssh/authorized_keys")).GetAwaiter().GetResult();
                    }, default);
                    throw new Exception("concurrent remote edit was overwritten");
                }
                catch (SshSetupException e) { Check(e.Code == "remoteConflict", "conflict result"); }
            }
            Check(!(await ReadAuthorized()).Contains(conflictKey.PublicKey!), "conflict never appends");

            await Docker("exec", name, "sh", "-c", "chmod 666 /home/fixture/.ssh/authorized_keys");
            var denied = await installer.ConfigureAsync(target, conflictKey, false, null, password, null, default);
            Check(!denied.Verified && denied.Error == "remoteUnsupported", "unsafe remote permissions refused");
            await Docker("exec", name, "chmod", "600", "/home/fixture/.ssh/authorized_keys");

            // Public-key auth disabled: installation can succeed but must not become a verified selection.
            await Docker("exec", name, "sh", "-c", "sed -i 's/PubkeyAuthentication yes/PubkeyAuthentication no/' /etc/ssh/sshd_config && python3 /fixture-reload.py");
            var unverified = await installer.ConfigureAsync(target, conflictKey, false, null, password, null, default);
            Check(!unverified.Verified && unverified.RemoteMayHaveChanged && unverified.Error == "verifyFailed", "installation alone is not success");
            var installedBeforeRetry = await ReadAuthorized();
            await Docker("exec", name, "sh", "-c", "sed -i 's/PubkeyAuthentication no/PubkeyAuthentication yes/' /etc/ssh/sshd_config && python3 /fixture-reload.py");
            var recovered = await installer.ConfigureAsync(target, conflictKey, false, null, null, null, default);
            Check(recovered.Verified && await ReadAuthorized() == installedBeforeRetry, "retry recovers same key without duplicate authorization");

            // Symlinks are rejected, and the foreign target remains untouched.
            await Docker("exec", name, "sh", "-c", "mv /home/fixture/.ssh/authorized_keys /home/fixture/saved-keys; ln -s /home/fixture/saved-keys /home/fixture/.ssh/authorized_keys");
            var symlinkKey = await store.GenerateAsync(null, default);
            var symlink = await installer.ConfigureAsync(target, symlinkKey, false, null, password, null, default);
            Check(symlink.Error == "remoteUnsupported", "symlink rejected");
            Check(await Docker("exec", name, "cat", "/home/fixture/saved-keys") == installedBeforeRetry, "symlink target unchanged");
            await Docker("exec", name, "sh", "-c", "rm /home/fixture/.ssh/authorized_keys; mv /home/fixture/saved-keys /home/fixture/.ssh/authorized_keys");

            // An interrupted append may have happened. Rechecking never generates a replacement key.
            using (var client = PasswordClient())
            using (var stop = new CancellationTokenSource())
            {
                client.Connect();
                try
                {
                    await SshAuthorizedKeys.InstallAsync(client, Guid.NewGuid().ToString(), symlinkKey.PublicKey!, () => stop.Cancel(), stop.Token);
                    throw new Exception("cancelled write proceeded");
                }
                catch (OperationCanceledException) { }
            }
            var retry = await installer.ConfigureAsync(target, symlinkKey, false, null, password, null, default);
            Check(retry.Verified && retry.Key!.Path == symlinkKey.Path, "cancelled installation can reuse the same saved key");
            var occurrences = (await ReadAuthorized()).Split(symlinkKey.PublicKey!).Length - 1;
            Check(occurrences == 1, "retry installs at most one copy");

            var disconnectedKey = await store.GenerateAsync(null, default);
            using (var client = PasswordClient())
            using (var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                client.Connect();
                try
                {
                    await SshAuthorizedKeys.InstallAsync(client, Guid.NewGuid().ToString(), disconnectedKey.PublicKey!, client.Disconnect, limit.Token);
                    throw new Exception("disconnected write reported success");
                }
                catch (Exception e) when (e is Renci.SshNet.Common.SshException or ObjectDisposedException or IOException) { }
            }
            var afterDisconnect = await installer.ConfigureAsync(target, disconnectedKey, false, null, password, null, default);
            Check(afterDisconnect.Verified && (await ReadAuthorized()).Split(disconnectedKey.PublicKey!).Length == 2, "disconnect retry preserves key and deduplicates");

            _ = new SshTests();
            var wizard = new SshKeySetupViewModel(target, [], store) { InstallationPassword = password };
            var beforeDoubleClick = Directory.GetFiles(store.DirectoryPath, "id_ed25519_*").Length;
            await Task.WhenAll(wizard.RunCmd.Execute().ToTask(), wizard.RunCmd.Execute().ToTask());
            Check(wizard.Result != null && !wizard.GenerateNew && wizard.InstallationPassword == "", "successful wizard clears credentials and returns verified draft");
            Check(Directory.GetFiles(store.DirectoryPath, "id_ed25519_*").Length == beforeDoubleClick + 1, "duplicate submit generates exactly one key");
            Check(store.GetDefault() == null, "successful setup alone never saves preferred selection");

            using (var cancelledSetup = new CancellationTokenSource())
            {
                var result = await installer.ConfigureAsync(target, null, true, null, password,
                    new InlineProgress(stage => { if (stage == "installing") cancelledSetup.Cancel(); }), cancelledSetup.Token);
                Check(result.Error == "cancelled" && !result.Verified && File.Exists(result.Key?.Path), "cancel after generation preserves key and reports cancellation");
                var resumed = await installer.ConfigureAsync(target, result.Key, false, null, password, null, default);
                Check(resumed.Verified && resumed.Key?.Path == result.Key?.Path, "service cancellation retries the retained key");
            }

            foreach (var metadata in Directory.GetFiles(store.DirectoryPath, "*.json"))
            {
                var text = File.ReadAllText(metadata);
                Check(!text.Contains(password) && !text.Contains("key-passphrase") && !text.Contains("PRIVATE KEY"), "no secrets in journal/default metadata");
            }

            Task<string> ReadAuthorized() => Docker("exec", name, "cat", "/home/fixture/.ssh/authorized_keys");
            Task<string> WriteAuthorized(string contents) => Docker("exec", "--user", "fixture", name, "sh", "-c",
                "printf '%s' '" + contents.Replace("'", "'\\''") + "' > ~/.ssh/authorized_keys");
            SshClient PasswordClient()
            {
                var client = new SshClient("127.0.0.1", port, "fixture", password);
                client.HostKeyReceived += (_, e) => e.CanTrust = Convert.ToBase64String(e.HostKey) == target.HostKey.PublicKey.Split(' ')[1];
                return client;
            }
            async Task TestForwarding(SshLocalKey? current, string? passphrase)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var localPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                var node = SshTests.Node();
                node.Address = "127.0.0.1"; node.Port = port; node.Username = "fixture"; node.Password = current == null ? password : "";
                node.SetProtocolExtra(node.GetProtocolExtra() with
                {
                    SshPrivateKeyAuth = current != null, SshPrivateKeyPath = current?.Path, SshPrivateKeyPassphrase = passphrase,
                    SshHostKey = target.HostKey.PublicKey, SshHostKeyAlgorithm = target.HostKey.Algorithm,
                    SshTrustedAddress = node.Address, SshTrustedPort = port,
                });
                var config = CoreConfigTestFactory.CreateConfig();
                CoreConfigTestFactory.BindAppManagerConfig(config);
                var generated = new CoreConfigSingboxService(CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box))
                    .GenerateClientSpeedtestConfig(localPort);
                Check(generated.Success, "sing-box config generation");
                var configPath = Path.Combine(directory, "forwarding.json");
                File.WriteAllText(configPath, generated.Data!.ToString());
                var info = new ProcessStartInfo(singbox) { RedirectStandardError = true, RedirectStandardOutput = true };
                info.ArgumentList.Add("run"); info.ArgumentList.Add("-c"); info.ArgumentList.Add(configPath);
                using var core = Process.Start(info)!;
                var errors = core.StandardError.ReadToEndAsync();
                try
                {
                    for (var i = 0; i < 60 && !core.HasExited; i++)
                    {
                        try { using var socket = new TcpClient(); await socket.ConnectAsync(IPAddress.Loopback, localPort); break; }
                        catch (SocketException) { await Task.Delay(50); }
                    }
                    Check(!core.HasExited, "sing-box exited");
                    using var client = new HttpClient(new HttpClientHandler
                    {
                        Proxy = new WebProxy($"socks5://127.0.0.1:{localPort}"),
                        // Only the disposable loopback TLS target uses a test certificate.
                        ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri?.Host == "127.0.0.1",
                    }) { Timeout = TimeSpan.FromSeconds(8) };
                    foreach (var url in new[] { "http://127.0.0.1:18080", "https://127.0.0.1:18443" })
                        Check(await client.GetStringAsync(url) == "ssh-key-setup-ok", "real SSH HTTP/HTTPS forwarding");
                }
                finally
                {
                    if (!core.HasExited) core.Kill(true);
                    await core.WaitForExitAsync();
                    await errors;
                }
            }
        }
        finally
        {
            if (started) await Docker("rm", "--force", name);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
