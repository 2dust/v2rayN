using ServiceLib.ViewModels;

namespace ServiceLib.Tests.CoreConfig.Singbox;

[NotInParallel]
public class SshKeySetupTests
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    private sealed class Sandbox : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssh-key-setup-" + Guid.NewGuid());
        public SshKeyStore Store { get; }
        public Sandbox() => Store = new(System.IO.Path.Combine(Path, "sshKeys"));
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }

    [Test]
    public async Task GenerationReuseAndExplicitRegenerationPreserveOldKeys()
    {
        using var sandbox = new Sandbox();
        var store = sandbox.Store;
        var first = await store.GenerateAsync(null, default);
        var original = File.ReadAllBytes(first.Path);
        Check(first.Algorithm == "ssh-ed25519" && first.Fingerprint!.StartsWith("SHA256:"), "identity");
        Check(store.GetDefault() == null, "generation must not change default before node save");
        store.SaveDefault(first);
        var restarted = new SshKeyStore(store.DirectoryPath);
        Check(restarted.GetDefault()?.Fingerprint == first.Fingerprint, "default survives restart");
        var second = await store.GenerateAsync(null, default);
        Check(second.Path != first.Path && second.Fingerprint != first.Fingerprint, "explicit generation must be new");
        Check(File.ReadAllBytes(first.Path).SequenceEqual(original), "never overwrite old private key");
        Check(restarted.GetDefault()?.Path == first.Path, "generation alone must not replace preferred key");
        var candidates = restarted.Discover([first.Path, second.Path, first.Path], null);
        Check(candidates.Count == 2 && candidates[0].Path == first.Path, "preferred ordering / deduplication");
        using var opened = restarted.Open(first, null);
        Check(SshKeyStore.PublicIdentity(opened).PublicKey == first.PublicKey, "private to public derivation");
        if (!OperatingSystem.IsWindows())
        {
            Check(File.GetUnixFileMode(first.Path) == (UnixFileMode)384, "private file mode 0600");
            Check(File.GetUnixFileMode(store.DirectoryPath) == (UnixFileMode)448, "directory mode 0700");
        }
        Check(!File.ReadAllText(System.IO.Path.Combine(store.DirectoryPath, "default.json")).Contains("PRIVATE KEY"), "metadata must be public");
    }

    [Test]
    public async Task EncryptedKeysRequirePassphraseAndKeepItOutOfMetadata()
    {
        using var sandbox = new Sandbox();
        const string secret = "test-key-passphrase";
        var key = await sandbox.Store.GenerateAsync(secret, default);
        var locked = sandbox.Store.Inspect(key.Path)!;
        Check(locked.Encrypted && locked.PublicKey == key.PublicKey && locked.Error == null, "locked OpenSSH keys show public identity but still require unlocking");
        Check(sandbox.Store.Inspect(key.Path, "wrong")?.Error == "invalidKey", "wrong passphrase");
        Check(sandbox.Store.Inspect(key.Path, secret)?.Fingerprint == key.Fingerprint, "correct passphrase");
        using var opened = sandbox.Store.Open(locked, secret);
        Check(SshKeyStore.PublicIdentity(opened).PublicKey == key.PublicKey, "unlock");
        sandbox.Store.SaveDefault(key);
        Check(!File.ReadAllText(System.IO.Path.Combine(sandbox.Store.DirectoryPath, "default.json")).Contains(secret), "no global passphrase");
    }

    [Test]
    public async Task DiscoveryIsShallowExcludesPublicFilesAndDoesNotChangeExternalKeys()
    {
        using var sandbox = new Sandbox();
        var key = await sandbox.Store.GenerateAsync(null, default);
        var ssh = System.IO.Path.Combine(sandbox.Path, ".ssh");
        Directory.CreateDirectory(System.IO.Path.Combine(ssh, "nested"));
        var external = System.IO.Path.Combine(ssh, "my-key");
        File.Copy(key.Path, external);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(external, (UnixFileMode)384);
        File.Copy(key.Path, System.IO.Path.Combine(ssh, "nested", "hidden-key"));
        File.WriteAllText(System.IO.Path.Combine(ssh, "my-key.pub"), key.PublicKey);
        File.WriteAllText(System.IO.Path.Combine(ssh, "config"), "Host test\nHostName localhost\n");
        File.WriteAllText(System.IO.Path.Combine(ssh, "known_hosts"), "localhost " + key.PublicKey);
        var before = File.ReadAllBytes(external);
        var candidates = sandbox.Store.Discover([external, external], sandbox.Path);
        Check(candidates.Count == 2 && candidates.Count(c => c.Duplicate) == 1, "only shallow private candidates with same-identity label");
        Check(File.ReadAllBytes(external).SequenceEqual(before), "external key unchanged");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(external, (UnixFileMode)420);
            Check(sandbox.Store.Inspect(external)?.Error == "keyPermissions", "broad permissions rejected");
            Check(File.GetUnixFileMode(external) == (UnixFileMode)420, "never repair external permissions silently");
        }
    }

    [Test]
    public async Task ChangedFilesAndCancelledConcurrentGenerationAreSafe()
    {
        using var sandbox = new Sandbox();
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => sandbox.Store.GenerateAsync(null, default)));
        Check(results.Select(k => k.Path).Distinct().Count() == 3, "parallel explicit generations are distinct");
        sandbox.Store.SaveDefault(results[0]);
        File.Copy(results[1].Path, results[0].Path, true);
        Check(sandbox.Store.Discover([], null).First().Error == "keyChanged", "changed default must not be silently reused");
        try { using var invalid = sandbox.Store.Open(results[0], null); throw new Exception("changed key accepted"); }
        catch (SshSetupException e) { Check(e.Code == "keyChanged", "fingerprint error"); }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { await sandbox.Store.GenerateAsync(null, cancelled.Token); throw new Exception("ignored cancellation"); }
        catch (OperationCanceledException) { }
        Check(Directory.GetFiles(sandbox.Store.DirectoryPath, "*.pending").Length == 0, "no abandoned partial files");
    }

    [Test]
    public async Task AuthorizedKeysRecognizesRestrictionsCommentsAndMissingFinalNewline()
    {
        using var sandbox = new Sandbox();
        var key = await sandbox.Store.GenerateAsync(null, default);
        foreach (var prefix in new[] { "", "restrict ", "command=\"echo hello world\",no-pty ", "from=\"127.0.0.1\" " })
        {
            var content = Encoding.UTF8.GetBytes("# preserve\n\n" + prefix + key.PublicKey + " comment with \"quote");
            Check(SshAuthorizedKeys.ContainsKey(content, key.PublicKey!), "restricted/commented key must not gain an unrestricted duplicate");
        }
        Check(!SshAuthorizedKeys.ContainsKey("# only comments"u8.ToArray(), key.PublicKey!), "empty authorization");
        foreach (var invalid in new[] { "broken", "ssh-ed25519 not-base64", "command=\"unterminated " + key.PublicKey })
        {
            try { SshAuthorizedKeys.ContainsKey(Encoding.UTF8.GetBytes(invalid), key.PublicKey!); throw new Exception("accepted malformed file"); }
            catch (SshSetupException e) { Check(e.Code == "authorizedKeys", "bounded error"); }
        }
    }

    [Test]
    public async Task BrokenUnreadablePublicAndMissingPreferredFilesCannotBeUsed()
    {
        using var sandbox = new Sandbox();
        var key = await sandbox.Store.GenerateAsync(null, default);
        var broken = System.IO.Path.Combine(sandbox.Path, "broken");
        File.WriteAllText(broken, "-----BEGIN OPENSSH PRIVATE KEY-----\nbroken\n-----END OPENSSH PRIVATE KEY-----\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(broken, (UnixFileMode)384);
        Check(sandbox.Store.Inspect(broken)?.Error == "invalidKey", "damaged private key is rejected");
        var publicOnly = System.IO.Path.Combine(sandbox.Path, "public-only.pub");
        File.WriteAllText(publicOnly, key.PublicKey);
        Check(sandbox.Store.Inspect(publicOnly) == null, "public keys are never candidates");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(broken, UnixFileMode.None);
            Check(sandbox.Store.Inspect(broken)?.Error == "invalidKey", "unreadable key rejected");
            File.SetUnixFileMode(broken, (UnixFileMode)384);
        }
        sandbox.Store.SaveDefault(key);
        File.Delete(key.Path);
        Check(sandbox.Store.Discover([], null).First().Error == "invalidKey", "missing shared key requires explicit new selection");
    }

    [Test]
    public void NewNodesDefaultToPasswordAndClosingSetupDoesNotPersistSelection()
    {
        _ = new SshTests(); // Initialize the same ReactiveUI infrastructure as editor tests.
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());
        var editor = new AddSshServerViewModel(new ProfileItem());
        Check(!editor.PrivateKeyAuth, "new nodes default to password");
        using var sandbox = new Sandbox();
        var target = new SshSetupTarget("127.0.0.1", 22, "test", SshProfileService.ParseHostKey(SshTests.TestKey));
        var wizard = new SshKeySetupViewModel(target, [], sandbox.Store);
        Check(wizard.GenerateNew && wizard.NeedsPassword, "empty discovery offers generation with password");
        wizard.InstallationPassword = "not-persisted";
        wizard.KeyPassphrase = "not-persisted-either";
        wizard.Cancel();
        Check(wizard.Result == null && wizard.InstallationPassword == "" && wizard.KeyPassphrase == "", "cancel clears secrets");
        Check(sandbox.Store.GetDefault() == null, "cancel never changes shared preference");
        editor.CancelOperation();
    }
}
