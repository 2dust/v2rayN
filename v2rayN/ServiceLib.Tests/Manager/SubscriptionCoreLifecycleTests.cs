using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Manager;

[NotInParallel]
public class SubscriptionCoreLifecycleTests
{
    [Test]
    public async Task UnchangedSubscription_PreservesBothProcessesAndLiveFiles_WithoutRemovingTun()
    {
        using var mainProcess = await StartOwnedProcess();
        using var preProcess = await StartOwnedProcess();
        var mainPid = mainProcess.Id;
        var prePid = preProcess.Id;
        using var mainHandle = Process.GetProcessById(mainPid);
        using var preHandle = Process.GetProcessById(prePid);
        var mainStarted = mainHandle.StartTime;
        var preStarted = preHandle.StartTime;
        var tunRemovals = 0;
        var manager = new CoreManager(() => { tunRemovals++; return Task.CompletedTask; });
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await manager.Init(config, (_, _) => Task.CompletedTask);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var main = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        var preNode = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "tun");
        var pre = CoreConfigTestFactory.CreateContext(config, preNode, ECoreType.sing_box) with { IsTunEnabled = true };
        var running = await PreparedCoreConfigBundle.CreateAsync(main, pre);
        SetField(manager, "_processService", mainProcess);
        SetField(manager, "_processPreService", preProcess);
        SetField(manager, "_runningConfig", running);
        var livePath = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        await File.WriteAllTextAsync(livePath, "live file must remain untouched");
        try
        {
            var reloaded = await manager.LoadCore(main, pre, skipIfUnchanged: true);
            await reloaded.Should().BeFalse();
            await mainHandle.HasExited.Should().BeFalse();
            await preHandle.HasExited.Should().BeFalse();
            await mainHandle.StartTime.Should().BeEqualTo(mainStarted);
            await preHandle.StartTime.Should().BeEqualTo(preStarted);
            await tunRemovals.Should().BeEqualTo(0);
            await (await File.ReadAllTextAsync(livePath)).Should().BeEqualTo("live file must remain untouched");
        }
        finally { await manager.CoreStop(); }
    }

    [Test]
    public async Task SubscriptionRefresh_DoesNotStartAStoppedCore()
    {
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        await manager.Init(config, (_, _) => Task.CompletedTask);
        var node = new ProfileItem { IndexId = "missing", ConfigType = EConfigType.Custom, CoreType = ECoreType.Xray, Address = "missing.json" };
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        await (await manager.LoadCore(context, null, skipIfUnchanged: true)).Should().BeFalse();
    }

    [Test]
    public async Task FailedPreSocksPreparation_PreservesTheRunningProcess()
    {
        using var process = await StartOwnedProcess();
        using var handle = Process.GetProcessById(process.Id);
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await manager.Init(config, (_, _) => Task.CompletedTask);
        var main = CoreConfigTestFactory.CreateContext(config, CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray), ECoreType.Xray);
        SetField(manager, "_processService", process);
        SetField(manager, "_runningConfig", await PreparedCoreConfigBundle.CreateAsync(main, null));
        var pre = CoreConfigTestFactory.CreateContext(config,
            new ProfileItem { IndexId = "missing", ConfigType = EConfigType.Custom, CoreType = ECoreType.Xray, Address = "missing-pre.json" }, ECoreType.Xray);
        try
        {
            await (await manager.LoadCore(main, pre, skipIfUnchanged: true)).Should().BeFalse();
            await handle.HasExited.Should().BeFalse();
        }
        finally { await manager.CoreStop(); }
    }

    [Test]
    public async Task ChangedCandidate_ForAnOutdatedUserSelection_IsDiscarded()
    {
        using var process = await StartOwnedProcess();
        using var handle = Process.GetProcessById(process.Id);
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await manager.Init(config, (_, _) => Task.CompletedTask);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var main = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        SetField(manager, "_processService", process);
        SetField(manager, "_runningConfig", await PreparedCoreConfigBundle.CreateAsync(main, null));
        node.Password = "changed-after-download-started";
        try
        {
            await (await manager.LoadCore(main, null, skipIfUnchanged: true, isCurrent: () => false)).Should().BeFalse();
            await handle.HasExited.Should().BeFalse();
        }
        finally { await manager.CoreStop(); }
    }

    private static void SetField(CoreManager manager, string field, object value) =>
        typeof(CoreManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);

    [Test]
    public async Task UserSelectionChangingDuringNotification_DoesNotStopTheRunningProcess()
    {
        using var process = await StartOwnedProcess();
        using var handle = Process.GetProcessById(process.Id);
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var current = true;
        var notified = false;
        await manager.Init(config, (_, _) => { notified = true; current = false; return Task.CompletedTask; });
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        SetField(manager, "_processService", process);
        SetField(manager, "_runningConfig", await PreparedCoreConfigBundle.CreateAsync(context, null));
        node.Password = "changed";
        try
        {
            await (await manager.LoadCore(context, null, true, () => current)).Should().BeFalse();
            await notified.Should().BeTrue();
            await handle.HasExited.Should().BeFalse();
        }
        finally { await manager.CoreStop(); }
    }

    [Test]
    public async Task CandidateRejectedAtApplyBoundary_DoesNotOverwriteLiveFiles()
    {
        using var process = await StartOwnedProcess();
        using var handle = Process.GetProcessById(process.Id);
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await manager.Init(config, (_, _) => Task.CompletedTask);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        SetField(manager, "_processService", process);
        SetField(manager, "_runningConfig", await PreparedCoreConfigBundle.CreateAsync(context, null));
        var path = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        await File.WriteAllTextAsync(path, "running bytes");
        node.Password = "changed";
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray)!;
        var executables = info.CoreExes;
        var executableName = $"preflight-fixture-{Guid.NewGuid():N}";
        var executable = Utils.GetBinPath(Utils.GetExeName(executableName), ECoreType.Xray.ToString());
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.Copy(Utils.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe") : "/bin/sleep", executable);
        info.CoreExes = [executableName];
        var checks = 0;
        try
        {
            // Current during preparation/notifications, superseded at the final apply boundary.
            await (await manager.LoadCore(context, null, true, () => ++checks < 3)).Should().BeFalse();
            await checks.Should().BeEqualTo(3);
            await handle.HasExited.Should().BeFalse();
            await (await File.ReadAllTextAsync(path)).Should().BeEqualTo("running bytes");
        }
        finally
        {
            info.CoreExes = executables;
            await manager.CoreStop();
            File.Delete(executable);
        }
    }

    [Test]
    public async Task MissingExecutable_DoesNotStopTheRunningProcess()
    {
        using var process = await StartOwnedProcess();
        using var handle = Process.GetProcessById(process.Id);
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await manager.Init(config, (_, _) => Task.CompletedTask);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        SetField(manager, "_processService", process);
        SetField(manager, "_runningConfig", await PreparedCoreConfigBundle.CreateAsync(context, null));
        node.Password = "changed";
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray)!;
        var executables = info.CoreExes;
        info.CoreExes = [$"missing-test-core-{Guid.NewGuid():N}"];
        try
        {
            await (await manager.LoadCore(context, null, true)).Should().BeFalse();
            await handle.HasExited.Should().BeFalse();
        }
        finally
        {
            info.CoreExes = executables;
            await manager.CoreStop();
        }
    }

    [Test]
    public async Task UnknownCustomDifference_IsPending_AndKeepsProcessAndLiveFile()
    {
        using var process = await StartOwnedProcess();
        using var handle = Process.GetProcessById(process.Id);
        var source = Path.Combine(Path.GetTempPath(), $"pending-{Guid.NewGuid():N}.yaml");
        const string original = "server: example.com:443\nauth: pass\n";
        await File.WriteAllTextAsync(source, original);
        var messages = new List<string>();
        var manager = new CoreManager(() => throw new InvalidOperationException("TUN must not be touched"));
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await manager.Init(config, (_, message) => { messages.Add(message); return Task.CompletedTask; });
        var node = new ProfileItem { IndexId = "opaque", ConfigType = EConfigType.Custom, CoreType = ECoreType.hysteria, Address = source };
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.hysteria);
        SetField(manager, "_processService", process);
        SetField(manager, "_runningConfig", await PreparedCoreConfigBundle.CreateAsync(context, null));
        var live = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        await File.WriteAllTextAsync(live, "running bytes");
        try
        {
            await File.WriteAllTextAsync(source, "auth: pass\nserver: example.com:443\n");
            await (await manager.LoadCore(context, null, true)).Should().BeFalse();
            await manager.HasPendingConfigurationChanges.Should().BeTrue();
            await messages.Single().Should().BeEqualTo(ResUI.MsgSubscriptionChangesPending);
            await handle.HasExited.Should().BeFalse();
            await (await File.ReadAllTextAsync(live)).Should().BeEqualTo("running bytes");

            await File.WriteAllTextAsync(source, original);
            await (await manager.LoadCore(context, null, true)).Should().BeFalse();
            await manager.HasPendingConfigurationChanges.Should().BeFalse();
        }
        finally
        {
            await manager.CoreStop();
            File.Delete(source);
        }
    }

    private static async Task<ProcessService> StartOwnedProcess()
    {
        var process = new ProcessService(
            Utils.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe") : "/bin/sleep",
            Utils.IsWindows() ? "-t 127.0.0.1" : "60", AppContext.BaseDirectory, false, false, null, null);
        await process.StartAsync();
        return process;
    }
}
