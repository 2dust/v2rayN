using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ServiceLib.Models.CoreConfigs;
using ServiceLib.Services;
using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Manager;

[NotInParallel]
public class CoreManagerReloadTests
{
    [Test]
    [Arguments(ECoreType.Xray, false)]
    [Arguments(ECoreType.Xray, true)]
    [Arguments(ECoreType.sing_box, false)]
    [Arguments(ECoreType.sing_box, true)]
    public async Task UnchangedSubscriptionKeepsTheRunningProcessAndLiveConfig(ECoreType coreType, bool tun)
    {
        await using var fixture = await ReloadFixture.Create(coreType, tun);
        await fixture.Manager.LoadCore(fixture.Context("old-profile"), null);
        var originalId = fixture.Manager.StartedIds.Single();
        using var original = Process.GetProcessById(originalId);
        var originalStart = original.StartTime;
        var originalBytes = await File.ReadAllBytesAsync(fixture.MainFile);
        File.SetLastWriteTimeUtc(fixture.MainFile, new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        var originalWriteTime = File.GetLastWriteTimeUtc(fixture.MainFile);
        var originalTunRemovals = fixture.Manager.TunRemovals;

        // Subscription import may assign a new database ID to an unchanged server.
        fixture.Config.IndexId = "imported-profile";
        var reused = await fixture.Manager.LoadCore(fixture.Context("imported-profile"), null, forceRestart: false);

        await reused.Should().BeTrue();
        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(1);
        await original.HasExited.Should().BeFalse();
        await original.StartTime.Should().BeEqualTo(originalStart);
        await fixture.Manager.TunRemovals.Should().BeEqualTo(originalTunRemovals);
        await (await File.ReadAllBytesAsync(fixture.MainFile)).SequenceEqual(originalBytes).Should().BeTrue();
        await File.GetLastWriteTimeUtc(fixture.MainFile).Should().BeEqualTo(originalWriteTime);
    }

    [Test]
    [Arguments(ECoreType.Xray, "password")]
    [Arguments(ECoreType.sing_box, "password")]
    [Arguments(ECoreType.Xray, "port")]
    [Arguments(ECoreType.sing_box, "tls")]
    [Arguments(ECoreType.sing_box, "wireguard-key")]
    [Arguments(ECoreType.Xray, "core")]
    [Arguments(ECoreType.Xray, "tun")]
    public async Task ChangedConnectionSettingsStillReplaceTheProcess(ECoreType coreType, string change)
    {
        await using var fixture = await ReloadFixture.Create(coreType, false);
        var initial = fixture.Context("profile");
        if (change == "tls")
        {
            initial = CoreConfigTestFactory.CreateContext(fixture.Config,
                CoreConfigTestFactory.CreateVmessNode(coreType, "profile"), coreType);
        }
        else if (change == "wireguard-key")
        {
            initial.Node.ConfigType = EConfigType.WireGuard;
            initial.Node.Password = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
            initial.Node.SetProtocolExtra(initial.Node.GetProtocolExtra() with
            {
                WgPublicKey = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()),
                WgInterfaceAddress = "172.16.0.2/32",
            });
        }
        await fixture.Manager.LoadCore(initial, null);
        using var original = Process.GetProcessById(fixture.Manager.StartedIds.Single());
        var changed = change is "tls" or "wireguard-key" ? initial : fixture.Context("profile");
        switch (change)
        {
            case "password": changed.Node.Password = "changed-secret"; break;
            case "port": changed.Node.Port = 8443; break;
            case "tls": changed.Node.StreamSecurity = "tls"; changed.Node.Sni = "changed.example"; break;
            case "wireguard-key": changed.Node.SetProtocolExtra(changed.Node.GetProtocolExtra() with
            {
                WgPublicKey = Convert.ToBase64String(Enumerable.Repeat((byte)3, 32).ToArray()),
            }); break;
            case "core": changed.Node.CoreType = ECoreType.sing_box; changed = changed with { RunCoreType = ECoreType.sing_box }; break;
            case "tun": changed = changed with { IsTunEnabled = true }; break;
        }

        await fixture.Manager.LoadCore(changed, null, forceRestart: false);

        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(2);
        await original.HasExited.Should().BeTrue();
    }

    [Test]
    public async Task ExplicitReloadStillReplacesAnUnchangedProcess()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, true);
        await fixture.Manager.LoadCore(fixture.Context("profile"), null);
        using var original = Process.GetProcessById(fixture.Manager.StartedIds.Single());

        await fixture.Manager.LoadCore(fixture.Context("profile"), null);

        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(2);
        await original.HasExited.Should().BeTrue();
        await fixture.Manager.TunRemovals.Should().BeEqualTo(OperatingSystem.IsWindows() ? 2 : 0);
    }

    [Test]
    public async Task ADeadCoreIsNotPreservedByAnUnchangedConfiguration()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, false);
        await fixture.Manager.LoadCore(fixture.Context("profile"), null);
        using var original = Process.GetProcessById(fixture.Manager.StartedIds.Single());
        original.Kill();
        await original.WaitForExitAsync();

        await fixture.Manager.LoadCore(fixture.Context("profile"), null, forceRestart: false);

        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(2);
        using var replacement = Process.GetProcessById(fixture.Manager.StartedIds.Last());
        await replacement.HasExited.Should().BeFalse();
    }

    [Test]
    public async Task AFailedStartDoesNotCreateAReusableRunningSnapshot()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, false);
        fixture.Manager.FailNextStart = true;
        await fixture.Manager.LoadCore(fixture.Context("profile"), null);
        await fixture.Manager.LoadCore(fixture.Context("profile"), null, forceRestart: false);
        await fixture.Manager.LoadCore(fixture.Context("profile"), null, forceRestart: false);

        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(1);
        using var replacement = Process.GetProcessById(fixture.Manager.StartedIds.Single());
        await replacement.HasExited.Should().BeFalse();
    }

    [Test]
    public async Task ChangedLaunchArgumentsAreNotHiddenByEqualJson()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, false);
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray)!;
        var originalArguments = info.Arguments;
        try
        {
            await fixture.Manager.LoadCore(fixture.Context("profile"), null);
            info.Arguments += " --changed-launch-option";
            await fixture.Manager.LoadCore(fixture.Context("profile"), null, forceRestart: false);
            await fixture.Manager.StartedIds.Count.Should().BeEqualTo(2);
        }
        finally
        {
            info.Arguments = originalArguments;
        }
    }

    [Test]
    public async Task UnchangedMainAndAuxiliaryCoreKeepBothProcessesAndTun()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, true);
        var pre = fixture.PreContext();
        var main = fixture.Context("old-profile") with { IsTunEnabled = false };
        await fixture.Manager.LoadCore(main, pre);
        var originalIds = fixture.Manager.StartedIds.ToArray();
        var oldMain = await File.ReadAllBytesAsync(fixture.MainFile);
        var oldPre = await File.ReadAllBytesAsync(fixture.PreFile);
        var oldTunRemovals = fixture.Manager.TunRemovals;

        await fixture.Manager.LoadCore(fixture.Context("imported-profile") with { IsTunEnabled = false }, fixture.PreContext(), forceRestart: false);

        await fixture.Manager.StartedIds.SequenceEqual(originalIds).Should().BeTrue();
        foreach (var id in originalIds)
        {
            using var process = Process.GetProcessById(id);
            await process.HasExited.Should().BeFalse();
        }
        await fixture.Manager.TunRemovals.Should().BeEqualTo(oldTunRemovals);
        await (await File.ReadAllBytesAsync(fixture.MainFile)).SequenceEqual(oldMain).Should().BeTrue();
        await (await File.ReadAllBytesAsync(fixture.PreFile)).SequenceEqual(oldPre).Should().BeTrue();
    }

    [Test]
    public async Task ChangedAuxiliaryConfigurationRestartsBothProcesses()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, true);
        var pre = fixture.PreContext();
        var main = fixture.Context("profile") with { IsTunEnabled = false };
        await fixture.Manager.LoadCore(main, pre);
        pre.Node.Password = "changed-pre-secret";

        await fixture.Manager.LoadCore(main, pre, forceRestart: false);

        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(4);
        await fixture.Manager.TunRemovals.Should().BeEqualTo(OperatingSystem.IsWindows() ? 2 : 0);
    }

    [Test]
    public async Task FailedAuxiliaryPreparationPreservesBothProcessesAndLiveFiles()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, true);
        var pre = fixture.PreContext();
        var main = fixture.Context("profile") with { IsTunEnabled = false };
        await fixture.Manager.LoadCore(main, pre);
        var originalIds = fixture.Manager.StartedIds.ToArray();
        var oldMain = await File.ReadAllBytesAsync(fixture.MainFile);
        var oldPre = await File.ReadAllBytesAsync(fixture.PreFile);
        var oldTunRemovals = fixture.Manager.TunRemovals;
        main.Node.Password = "changed-main-secret";
        pre.Node.Address = string.Empty;

        await fixture.Manager.LoadCore(main, pre, forceRestart: false);

        await fixture.Manager.StartedIds.SequenceEqual(originalIds).Should().BeTrue();
        foreach (var id in originalIds)
        {
            using var process = Process.GetProcessById(id);
            await process.HasExited.Should().BeFalse();
        }
        await fixture.Manager.TunRemovals.Should().BeEqualTo(oldTunRemovals);
        await (await File.ReadAllBytesAsync(fixture.MainFile)).SequenceEqual(oldMain).Should().BeTrue();
        await (await File.ReadAllBytesAsync(fixture.PreFile)).SequenceEqual(oldPre).Should().BeTrue();
    }

    [Test]
    public async Task AuxiliaryStartupUsesTheJsonPreparedBeforeMainStartup()
    {
        await using var fixture = await ReloadFixture.Create(ECoreType.Xray, true);
        var pre = fixture.PreContext();
        fixture.Manager.OnStarted = count =>
        {
            if (count == 1) fixture.Config.CoreBasicItem.Loglevel = "debug";
        };

        await fixture.Manager.LoadCore(fixture.Context("profile") with { IsTunEnabled = false }, pre);

        await fixture.Manager.StartedIds.Count.Should().BeEqualTo(2);
        var actual = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(fixture.PreFile));
        await actual!["log"]!["level"]!.GetValue<string>().Should().BeEqualTo("warn");
    }

    private sealed class HarmlessCoreManager : CoreManager
    {
        public List<int> StartedIds { get; } = [];
        public int TunRemovals { get; private set; }
        public bool FailNextStart { get; set; }
        public Action<int>? OnStarted { get; set; }

        protected override async Task<ProcessService?> RunProcess(
            CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo, bool isTunLaunch = false)
        {
            if (FailNextStart)
            {
                FailNextStart = false;
                return null;
            }
            var windows = OperatingSystem.IsWindows();
            var process = new ProcessService(
                windows ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "PING.EXE") : "/bin/sleep",
                windows ? "-t 127.0.0.1" : "600",
                AppContext.BaseDirectory,
                displayLog: true,
                redirectInput: false,
                environmentVars: null,
                updateFunc: (_, _) => Task.CompletedTask);
            await process.StartAsync();
            StartedIds.Add(process.Id);
            OnStarted?.Invoke(StartedIds.Count);
            return process;
        }

        protected override Task RemoveTunDevice()
        {
            TunRemovals++;
            return Task.CompletedTask;
        }
    }

    private sealed class ReloadFixture : IAsyncDisposable
    {
        private readonly Action _restoreGlobals;
        private readonly Dictionary<string, byte[]?> _originalFiles = [];
        private readonly List<string> _createdExecutables = [];
        private readonly ECoreType _coreType;
        private readonly CancellationTokenSource _listenerCancellation = new();
        private TcpListener? _listener;
        private Task? _listenerTask;

        private ReloadFixture(Config config, ECoreType coreType, Action restoreGlobals)
        {
            Config = config;
            _coreType = coreType;
            _restoreGlobals = restoreGlobals;
            MainFile = Utils.GetBinConfigPath(Global.CoreConfigFileName);
            PreFile = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
            RememberFile(MainFile);
            RememberFile(PreFile);
        }

        public Config Config { get; }
        public string MainFile { get; }
        public string PreFile { get; }
        public HarmlessCoreManager Manager { get; } = new();

        public static async Task<ReloadFixture> Create(ECoreType coreType, bool tun)
        {
            var originalConfig = AppManager.Instance.Config;
            var originalCoreType = AppManager.Instance.RunningCoreType;
            var config = CoreConfigTestFactory.CreateConfig(coreType);
            config.TunModeItem.EnableTun = tun;
            config.IndexId = "old-profile";
            CoreConfigTestFactory.BindAppManagerConfig(config);
            var fixture = new ReloadFixture(config, coreType, () =>
            {
                CoreConfigTestFactory.BindAppManagerConfig(originalConfig);
                AppManager.Instance.RunningCoreType = originalCoreType;
            });
            try
            {
                foreach (var type in new[] { ECoreType.Xray, ECoreType.sing_box })
                {
                    var info = CoreInfoManager.Instance.GetCoreInfo(type)!;
                    var executable = Utils.GetBinPath(Utils.GetExeName(info.CoreExes.First()), type.ToString());
                    fixture.RequirePrivatePath(executable);
                    if (!File.Exists(executable))
                    {
                        await File.WriteAllTextAsync(executable, "Owned test marker; the launch adapter never executes this file.");
                        fixture._createdExecutables.Add(executable);
                    }
                }
                await fixture.Manager.Init(config, (_, _) => Task.CompletedTask);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public CoreConfigContext Context(string id)
        {
            var node = CoreConfigTestFactory.CreateSocksNode(_coreType, id);
            return CoreConfigTestFactory.CreateContext(Config, node, _coreType) with
            {
                FullConfigTemplate = new FullConfigTemplateItem { Enabled = false },
                RawDnsItem = new DNSItem { Enabled = false },
                IsWindows = OperatingSystem.IsWindows(),
                IsMacOS = OperatingSystem.IsMacOS(),
                IsLinux = OperatingSystem.IsLinux(),
            };
        }

        public CoreConfigContext PreContext()
        {
            if (_listener is null)
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Config.Inbound[0].LocalPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _listenerTask = ServeProxyGreeting(_listenerCancellation.Token);
            }
            var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "pre-profile");
            node.Port = Config.Inbound[0].LocalPort;
            return CoreConfigTestFactory.CreateContext(Config, node, ECoreType.sing_box) with
            {
                IsTunEnabled = true,
                IsWindows = OperatingSystem.IsWindows(),
                IsMacOS = OperatingSystem.IsMacOS(),
                IsLinux = OperatingSystem.IsLinux(),
            };
        }

        private async Task ServeProxyGreeting(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    using var client = await _listener!.AcceptTcpClientAsync(token);
                    var stream = client.GetStream();
                    var greeting = new byte[3];
                    await stream.ReadExactlyAsync(greeting, token);
                    if (!greeting.SequenceEqual(new byte[] { 5, 1, 0 }))
                        throw new InvalidDataException("Unexpected SOCKS greeting in the owned lifecycle fixture.");
                    await stream.WriteAsync(new byte[] { 5, 0 }, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (SocketException) when (token.IsCancellationRequested) { }
        }

        private void RequirePrivatePath(string path)
        {
            if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A lifecycle fixture must use only its isolated test output directory.");
            }
        }

        private void RememberFile(string path)
        {
            RequirePrivatePath(path);
            _originalFiles[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Manager.CoreStop();
            }
            finally
            {
                _listenerCancellation.Cancel();
                _listener?.Stop();
                if (_listenerTask is not null) await _listenerTask;
                _listenerCancellation.Dispose();
                foreach (var (path, content) in _originalFiles)
                {
                    if (content is null) File.Delete(path);
                    else await File.WriteAllBytesAsync(path, content);
                }
                foreach (var path in _createdExecutables) File.Delete(path);
                _restoreGlobals();
            }
        }
    }
}
