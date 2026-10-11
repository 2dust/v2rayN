using ReactiveUI.Primitives;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.CoreConfig.Singbox;

[NotInParallel]
public class SshConnectionTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private sealed class Probe : ISshConnectionTestService
    {
        public ProfileItem? Draft;
        public int Calls;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Block;
        public async Task<SshConnectionTestResult> TestAsync(ProfileItem snapshot, string url,
            IProgress<SshConnectionTestResult>? progress, CancellationToken token)
        {
            Draft = snapshot; Calls++; Started.TrySetResult();
            if (Block)
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { return new(url, new(SshTestState.Cancelled, Error: "cancelled"), new(SshTestState.Skipped)); }
            }
            return new(url, new(SshTestState.Passed, 10), new(SshTestState.Passed, 20, HttpStatus: 200));
        }
    }

    [Test]
    public async Task DraftTestingDoesNotSaveAndCredentialChangesInvalidateResults()
    {
        _ = new SshTests();
        var config = CoreConfigTestFactory.CreateConfig();
        config.IndexId = "running-node";
        CoreConfigTestFactory.BindAppManagerConfig(config);
        foreach (var editing in new[] { false, true })
        {
            var original = SshTests.Node();
            if (!editing) original.IndexId = "";
            var serialized = JsonUtils.Serialize(original);
            var probe = new Probe();
            var editor = new AddSshServerViewModel(original, probe) { Username = "draft-user", Password = "draft-password" };
            await editor.TestCmd.Execute().ToTask();
            Check(probe.Draft?.Username == "draft-user" && probe.Draft.Password == "draft-password", "test uses draft credentials");
            Check(JsonUtils.Serialize(original) == serialized && JsonUtils.Serialize(editor.SelectedSource) == serialized, "test never mutates saved/new source");
            Check(config.IndexId == "running-node", "running selection unchanged");
            Check(editor.TestStatus.Contains("HTTP 200") && !editor.TestStatus.Contains("draft-password"), "separate sanitized results");
            var status = editor.TestStatus;
            editor.Remarks = "renamed";
            Check(editor.TestStatus == status, "rename preserves result");
            Action[] changes = [() => editor.Username += "x", () => editor.Password += "x", () => editor.PrivateKeyAuth = true,
                () => editor.PrivateKeyPath = "/tmp/test-key", () => editor.Passphrase = "secret",
                () => editor.HostKey = "", () => editor.Address = "localhost", () => editor.Port = "2200"];
            foreach (var change in changes)
            {
                editor.TestStatus = status; change();
                Check(editor.TestStatus == "", "changed connection input clears old result");
            }
            editor.CancelOperation();
        }
    }

    [Test]
    public async Task StopCloseRepeatedClicksAndPasswordModeSetupAreGuarded()
    {
        _ = new SshTests();
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());
        var probe = new Probe { Block = true };
        var editor = new AddSshServerViewModel(SshTests.Node(), probe);
        // No DB is initialized: password-mode setup must return before key discovery/installation.
        await editor.SetupKeyCmd.Execute().ToTask();
        var first = editor.TestCmd.Execute().ToTask();
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = editor.TestCmd.Execute().ToTask();
        Check(editor.IsTesting && editor.IsBusy, "form locked while test runs");
        await editor.StopTestCmd.Execute().ToTask();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3));
        Check(probe.Calls == 1 && !editor.IsTesting && !editor.IsBusy, "duplicate test suppressed and cancellation completes");
        Check(editor.Password == "test-password", "stop retains credentials");
        probe.Block = false;
        await editor.TestCmd.Execute().ToTask();
        Check(probe.Calls == 2 && editor.TestStatus.Contains("HTTP 200"), "stopped window can retest");
        probe.Block = true;
        probe.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var closing = editor.TestCmd.Execute().ToTask();
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        editor.CancelOperation();
        await closing.WaitAsync(TimeSpan.FromSeconds(3));
        Check(editor.Password == "" && !editor.IsBusy, "close cancels and clears secrets");
    }

    [Test]
    public async Task UnconfirmedIdentityCannotReachAuthentication()
    {
        _ = new SshTests();
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());
        var node = SshTests.Node();
        node.SetProtocolExtra(node.GetProtocolExtra() with { SshTrustedAddress = null });
        var probe = new Probe();
        var editor = new AddSshServerViewModel(node, probe);
        using var confirmation = editor.ConfirmHostKeyInteraction.RegisterHandler(c => c.SetOutput(false));
        // Explicit refusal of a pasted identity; testing must not consider it trusted.
        await editor.TrustCmd.Execute().ToTask();
        Check(SshProfileService.Validate(editor.CreateDraftSnapshot()) != null && probe.Calls == 0, "declining trust prevents authentication");
        editor.CancelOperation();
    }

    [Test]
    public async Task InvalidUrlNeverEchoesCredentialsOrAttemptsAuthentication()
    {
        var result = await new SshConnectionTestService().TestAsync(SshTests.Node(),
            "https://account:hidden-password@127.0.0.1/path?token=hidden-token", null, default);
        Check(result.Login.Error == "testUrl" && result.Proxy.State == SshTestState.Skipped, "credential URL rejected before SSH");
        Check(result.Url == "https://127.0.0.1/path" && !JsonUtils.Serialize(result).Contains("hidden-"), "URL credentials and query not exposed");
    }

    [Test]
    public void DraftCoreHasOnlySshRouteAndProtectedLoopbackInbound()
    {
        var node = SshTests.Node();
        var data = new CoreConfigSingboxService(new() { Node = node, RunCoreType = ECoreType.sing_box })
            .GenerateSshConnectionTestConfig(12345, "local-only-secret");
        var json = JsonNode.Parse(data)!;
        Check(json["outbounds"]!.AsArray().Count == 1 && json["outbounds"]![0]!["type"]!.ToString() == "ssh", "no direct fallback");
        Check(json["route"]!["final"]!.ToString() == "proxy", "all requests use draft node");
        Check(json["inbounds"]![0]!["listen"]!.ToString() == "127.0.0.1" && json["inbounds"]![0]!["users"] != null, "loopback authenticated listener");
        Check(json["experimental"] == null && json["route"]!["rules"] == null, "no stats or routing state");
    }
}
