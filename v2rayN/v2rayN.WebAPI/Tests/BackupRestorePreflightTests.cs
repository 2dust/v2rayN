using Microsoft.Extensions.Configuration;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Launcher;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class BackupRestorePreflightTests
{
    [Test]
    [Arguments("https://127.0.0.1:5443", "backup.restoreProbeUnavailable")]
    [Arguments("http://192.168.1.20:5080", "backup.restoreProbeUnavailable")]
    public async Task UnprobeableListenersRefuseBeforeAnyRestoreMutation(string listener, string messageKey)
    {
        foreach (var wasRunning in new[] { false, true })
        {
            using var directory = new TemporaryDirectory();
            var configPath = Path.Combine(directory.Path, "guiNConfig.json");
            await File.WriteAllTextAsync(configPath, "original-config");
            var coreRunning = wasRunning;
            var mutationsStarted = false;
            var preflight = CreatePreflight(listener, setsidPath: "/usr/bin/setsid");

            var result = await BackupRestorePreflightGate.ExecuteAsync(preflight, async () =>
            {
                mutationsStarted = true;
                coreRunning = false;
                await File.WriteAllTextAsync(configPath, "restored-config");
                return OperationView.Ok(ApiMessageKeys.BackupRestoreStarted);
            });

            await result.Success.Should().BeFalse();
            await result.Code.Should().BeEqualTo("backup_restore_health_probe_unavailable");
            await result.MessageKey.Should().BeEqualTo(messageKey);
            await mutationsStarted.Should().BeFalse();
            await coreRunning.Should().BeEqualTo(wasRunning);
            await (await File.ReadAllTextAsync(configPath)).Should().BeEqualTo("original-config");
        }
    }

    [Test]
    public async Task MissingExecutableSetsidRefusesBeforeStoppingCoreOrReplacingConfig()
    {
        foreach (var wasRunning in new[] { false, true })
        {
            using var directory = new TemporaryDirectory();
            var configPath = Path.Combine(directory.Path, "guiNConfig.json");
            await File.WriteAllTextAsync(configPath, "original-config");
            var coreRunning = wasRunning;
            var mutationsStarted = false;
            var preflight = CreatePreflight("http://127.0.0.1:5080", setsidPath: null);

            var result = await BackupRestorePreflightGate.ExecuteAsync(preflight, async () =>
            {
                mutationsStarted = true;
                coreRunning = false;
                await File.WriteAllTextAsync(configPath, "restored-config");
                return OperationView.Ok(ApiMessageKeys.BackupRestoreStarted);
            });

            await result.Success.Should().BeFalse();
            await result.Code.Should().BeEqualTo("backup_restore_setsid_unavailable");
            await result.MessageKey.Should().BeEqualTo(ApiMessageKeys.BackupRestoreSetSidUnavailable);
            await mutationsStarted.Should().BeFalse();
            await coreRunning.Should().BeEqualTo(wasRunning);
            await (await File.ReadAllTextAsync(configPath)).Should().BeEqualTo("original-config");
        }
    }

    [Test]
    public async Task NativeWindowsRestoreBuildsVerifiedHiddenReplacementWithoutSetsid()
    {
        var preflight = NativeWebRestorePreflight.Evaluate(
            Config(("urls", "http://localhost:5080")),
            isLinux: false,
            isWindows: true,
            externalSupervisor: false,
            containerEnvironment: false,
            setsidPath: null,
            processPath: @"C:\WebAPI\v2rayN.WebAPI.exe",
            managedEntryPoint: null,
            hostArguments: ["--urls", "http://localhost:5080"],
            fileExists: _ => true);

        await preflight.Allowed.Should().BeTrue();
        await preflight.Owner.Should().BeEqualTo(WebLifecycleOwner.NativeProcess);
        await preflight.Replacement!.HealthUri.ToString().Should().BeEqualTo("http://localhost:5080/api/health");
        await preflight.Replacement.Command.CreateNoWindow.Should().BeTrue();
        await preflight.Replacement.Command.LauncherPath.Should().BeEqualTo(Path.GetFullPath(@"C:\WebAPI\v2rayN.WebAPI.exe"));
        await preflight.Replacement.Command.Arguments.SequenceEqual([
            WebLaunchOptions.BackgroundChildFlag,
            "--urls",
            "http://localhost:5080",
        ]).Should().BeTrue();
        await preflight.Replacement.Command.ToProcessStartInfo().UseShellExecute.Should().BeFalse();
    }

    [Test]
    public async Task ExternalSupervisorOwnsRestartAndDoesNotRequireANativeHealthProbeOrLauncher()
    {
        var preflight = NativeWebRestorePreflight.Evaluate(
            Config(("urls", "https://127.0.0.1:5443")),
            isLinux: true,
            isWindows: false,
            externalSupervisor: true,
            containerEnvironment: false,
            setsidPath: null,
            processPath: null,
            managedEntryPoint: null,
            hostArguments: []);

        await preflight.Allowed.Should().BeTrue();
        await preflight.Owner.Should().BeEqualTo(WebLifecycleOwner.ExternalSupervisor);
        await (preflight.Replacement is null).Should().BeTrue();
    }

    private static NativeWebRestorePreflightResult CreatePreflight(string listener, string? setsidPath) =>
        NativeWebRestorePreflight.Evaluate(
            Config(("urls", listener)),
            isLinux: true,
            isWindows: false,
            externalSupervisor: false,
            containerEnvironment: false,
            setsidPath,
            processPath: "/opt/v2rayN.WebAPI",
            managedEntryPoint: null,
            hostArguments: [],
            fileExists: _ => true,
            isUnixExecutable: _ => true);

    private static IConfigurationRoot Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-WebAPI-restore-preflight-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
