using System.Text.Json;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class RuntimeRestartRecoveryTests
{
    [Test]
    public async Task RestoreMarkerWithStoppedCoreSuppressesAutostart()
    {
        var started = new List<string>();
        var result = await RuntimeRestartRecovery.RecoverAsync(
            new RuntimeRestartIntent(false, "old"),
            "restored",
            "default",
            (_, _) => Task.FromResult(true),
            (id, _) =>
            {
                started.Add(id);
                return Task.FromResult(OperationView.Ok("core.started"));
            },
            _ => { },
            CancellationToken.None);

        await (result.Source == RuntimeRestartRecoverySource.NotRequested).Should().BeTrue();
        await (started.Count == 0).Should().BeTrue();
        await RuntimeRestartRecovery.ShouldAutoStart(new RuntimeRestartIntent(false, null), configured: true).Should().BeFalse();
        await RuntimeRestartRecovery.ShouldAutoStart(null, configured: true).Should().BeTrue();
        await RuntimeRestartRecovery.ShouldAutoStart(null, configured: true, setupRequired: true).Should().BeFalse();
        await RuntimeRestartRecovery.ShouldRecoverCore(new RuntimeRestartIntent(true, "selected"), setupRequired: true).Should().BeFalse();
        await RuntimeRestartRecovery.ShouldRecoverCore(new RuntimeRestartIntent(true, "selected"), setupRequired: false).Should().BeTrue();
        await RuntimeRestartRecovery.ShouldAutoStart(null, configured: true, setupRequired: true).Should().BeFalse();
        await RuntimeRestartRecovery.ShouldRecoverCore(new RuntimeRestartIntent(true, "selected"), setupRequired: true).Should().BeFalse();
        await RuntimeRestartRecovery.ShouldRecoverCore(new RuntimeRestartIntent(true, "selected"), setupRequired: false).Should().BeTrue();
    }

    [Test]
    public async Task RunningCoreRestoresThePreviousProfileWhenItStillExists()
    {
        var (result, started, _) = await RecoverAsync(
            new RuntimeRestartIntent(true, "old"),
            "selected",
            "default",
            ["old", "selected", "default"]);

        await (result.Source == RuntimeRestartRecoverySource.PreferredProfile).Should().BeTrue();
        await (result.ProfileId == "old").Should().BeTrue();
        await started.Contains("old").Should().BeTrue();
        await started.Count.Should().BeEqualTo(1);
    }

    [Test]
    public async Task MissingPreviousProfileFallsBackToRestoredSelectedProfile()
    {
        var (result, started, logs) = await RecoverAsync(
            new RuntimeRestartIntent(true, "old"),
            "selected",
            "default",
            ["selected", "default"]);

        await (result.Source == RuntimeRestartRecoverySource.SelectedProfile).Should().BeTrue();
        await (result.ProfileId == "selected").Should().BeTrue();
        await started.Contains("selected").Should().BeTrue();
        await logs.Any(log => log.Contains("Previous profile old no longer exists", StringComparison.Ordinal)).Should().BeTrue();
        await logs.Any(log => log.Contains("Falling back to selected profile selected", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public async Task MissingPreviousAndSelectedProfilesFallBackToServiceLibDefault()
    {
        var (result, started, logs) = await RecoverAsync(
            new RuntimeRestartIntent(true, "old"),
            "missing-selected",
            "default",
            ["default"]);

        await (result.Source == RuntimeRestartRecoverySource.DefaultProfile).Should().BeTrue();
        await (result.ProfileId == "default").Should().BeTrue();
        await started.Contains("default").Should().BeTrue();
        await logs.Any(log => log.Contains("Falling back to the ServiceLib default profile default", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public async Task RestoreWithNoAvailableProfileKeepsCoreStoppedAndLogsContext()
    {
        var (result, started, logs) = await RecoverAsync(
            new RuntimeRestartIntent(true, "old"),
            "missing-selected",
            null,
            []);

        await (result.Source == RuntimeRestartRecoverySource.NoProfile).Should().BeTrue();
        await (started.Count == 0).Should().BeTrue();
        await logs.Any(log => log.Contains("no valid profile exists in the current configuration", StringComparison.Ordinal)).Should().BeTrue();
        await logs.Any(log => log.Contains("Core remains stopped", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public async Task FailedFallbackPreflightIsNotReportedAsSuccessOrRetried()
    {
        var starts = new List<string>();
        var logs = new List<string>();
        var result = await RuntimeRestartRecovery.RecoverAsync(
            new RuntimeRestartIntent(true, "removed"),
            "also-removed",
            "fallback",
            (id, _) => Task.FromResult(id == "fallback"),
            (id, _) =>
            {
                starts.Add(id);
                return Task.FromResult(OperationView.Fail("profile_validation_failed", "errors.invalidInput"));
            },
            logs.Add,
            CancellationToken.None);

        await (result.Source == RuntimeRestartRecoverySource.StartFailed).Should().BeTrue();
        await (result.FailureCode == "profile_validation_failed").Should().BeTrue();
        await starts.Contains("fallback").Should().BeTrue();
        await starts.Count.Should().BeEqualTo(1);
        await logs.Any(log => log.Contains("No further restart will be attempted", StringComparison.Ordinal)).Should().BeTrue();
        await (!logs.Any(log => log.Contains("Core restored successfully", StringComparison.Ordinal))).Should().BeTrue();
    }

    [Test]
    public async Task RestoreRuntimeMarkerSurvivesFailedInitializationAndIsConsumedAfterRecovery()
    {
        var markerJson = JsonSerializer.Serialize(new RuntimeRestartIntent(true, "old"));
        foreach (var reverseCreationOrder in new[] { false, true })
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "restore-state.json");
            var newest = DateTime.UtcNow.AddMinutes(-1);
            var claims = new[]
            {
                (Name: "consuming-abandoned", Json: JsonSerializer.Serialize(new RuntimeRestartIntent(false, null)), Time: newest.AddMinutes(-1)),
                (Name: "consuming-prior", Json: markerJson, Time: newest),
            };
            foreach (var claim in reverseCreationOrder ? claims.Reverse() : claims)
            {
                var claimPath = path + "." + claim.Name;
                await File.WriteAllTextAsync(claimPath, claim.Json);
                File.SetLastWriteTimeUtc(claimPath, claim.Time);
            }
            var invalidClaimPath = path + ".consuming-invalid-stale";
            await File.WriteAllTextAsync(invalidClaimPath, "not-json");
            File.SetLastWriteTimeUtc(invalidClaimPath, newest.AddMinutes(1));

            // Simulate initialization failing after the newest valid claim has been requeued.
            var firstStartupFailed = false;
            try
            {
                _ = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);
                throw new InvalidOperationException("simulated initialization failure");
            }
            catch (InvalidOperationException)
            {
                firstStartupFailed = true;
            }

            var secondStartup = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);
            await firstStartupFailed.Should().BeTrue();
            await (secondStartup is { WasRunning: true, PreferredProfileId: "old" }).Should().BeTrue();
            await File.Exists(path).Should().BeTrue();
            await Directory.GetFiles(directory.Path, "*.consuming-*").Length.Should().BeEqualTo(0);

            await V2rayRuntime.CommitRestoreRuntimeStateAsync(path);
            await File.Exists(path).Should().BeFalse();
            await Directory.GetFiles(directory.Path, "*.consuming-*").Length.Should().BeEqualTo(0);
        }

        using (var directory = new TemporaryDirectory())
        {
            var stoppedIntentPath = Path.Combine(directory.Path, "restore-stopped.json");
            var stoppedIntent = new RuntimeRestartIntent(false, null);
            await File.WriteAllTextAsync(stoppedIntentPath, JsonSerializer.Serialize(stoppedIntent));
            try
            {
                _ = await V2rayRuntime.LoadRuntimeRestartIntentAsync(stoppedIntentPath, CancellationToken.None);
                throw new InvalidOperationException("simulated stopped-intent initialization failure");
            }
            catch (InvalidOperationException) { }
            var stoppedOnSecondStartup = await V2rayRuntime.LoadRuntimeRestartIntentAsync(stoppedIntentPath, CancellationToken.None);
            await (stoppedOnSecondStartup is { WasRunning: false }).Should().BeTrue();
            await File.Exists(stoppedIntentPath).Should().BeTrue();
            await V2rayRuntime.CommitRestoreRuntimeStateAsync(stoppedIntentPath);
            await File.Exists(stoppedIntentPath).Should().BeFalse();
        }
        await markerJson.Should().Contain("PreferredProfileId");
        await (!markerJson.Contains("ProcessIds", StringComparison.Ordinal)
            && !markerJson.Contains("ApiPort", StringComparison.Ordinal)
            && !markerJson.Contains("Listeners", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public async Task RestoreClaimTieUsesTheMoreInformativeStateRegardlessOfCreationOrder()
    {
        foreach (var reverseCreationOrder in new[] { false, true })
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "restore-state.json");
            var sameTimestamp = DateTime.UtcNow.AddMinutes(-1);
            var claims = new[]
            {
                (Name: "consuming-a-stopped", Intent: new RuntimeRestartIntent(false, null)),
                (Name: "consuming-z-running", Intent: new RuntimeRestartIntent(true, "old")),
            };
            foreach (var claim in reverseCreationOrder ? claims.Reverse() : claims)
            {
                var claimPath = path + "." + claim.Name;
                await File.WriteAllTextAsync(claimPath, JsonSerializer.Serialize(claim.Intent));
                File.SetLastWriteTimeUtc(claimPath, sameTimestamp);
            }

            var recovered = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);
            await (recovered is { WasRunning: true, PreferredProfileId: "old" }).Should().BeTrue();
            await Directory.GetFiles(directory.Path, "*.consuming-*").Length.Should().BeEqualTo(0);
            await V2rayRuntime.CommitRestoreRuntimeStateAsync(path);
            await File.Exists(path).Should().BeFalse();
        }
    }

    [Test]
    public async Task ValidPrimaryMarkerWinsOverAbandonedClaimsAndCleansThemUp()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "restore-state.json");
        var primary = new RuntimeRestartIntent(false, "stopped-profile");
        var claim = new RuntimeRestartIntent(true, "newer-claim");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(primary));
        var claimPath = path + ".consuming-abandoned";
        await File.WriteAllTextAsync(claimPath, JsonSerializer.Serialize(claim));
        File.SetLastWriteTimeUtc(claimPath, DateTime.UtcNow.AddMinutes(1));

        var recovered = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);

        await (recovered is { WasRunning: false, PreferredProfileId: "stopped-profile" }).Should().BeTrue();
        await Directory.GetFiles(directory.Path, "*.consuming-*").Length.Should().BeEqualTo(0);
        await V2rayRuntime.CommitRestoreRuntimeStateAsync(path);
        await File.Exists(path).Should().BeFalse();
    }

    [Test]
    public async Task InvalidPrimaryMarkerIsReplacedByTheNewestValidClaim()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "restore-state.json");
        await File.WriteAllTextAsync(path, "not-json");
        var claimPath = path + ".consuming-recoverable";
        var expected = new RuntimeRestartIntent(true, "recovered-profile");
        await File.WriteAllTextAsync(claimPath, JsonSerializer.Serialize(expected));

        var recovered = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);

        await (recovered is { WasRunning: true, PreferredProfileId: "recovered-profile" }).Should().BeTrue();
        await Directory.GetFiles(directory.Path, "*.consuming-*").Length.Should().BeEqualTo(0);
        await V2rayRuntime.CommitRestoreRuntimeStateAsync(path);
        await File.Exists(path).Should().BeFalse();
    }

    [Test]
    public async Task FailedClaimMoveDoesNotDeleteTheOnlyValidClaim()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "restore-state.json");
        Directory.CreateDirectory(path);
        var claimPath = path + ".consuming-recoverable";
        await File.WriteAllTextAsync(claimPath, JsonSerializer.Serialize(new RuntimeRestartIntent(true, "recoverable")));

        var recovered = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);

        await (recovered is null).Should().BeTrue();
        await File.Exists(claimPath).Should().BeTrue();
    }

    [Test]
    public async Task WebUpdateRuntimeIntentRemainsUntilTheExternalHealthVerifierCommitsIt()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "WebAPI-update-runtime-state.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new RuntimeRestartIntent(true, "removed")));
        var intent = await V2rayRuntime.LoadRuntimeRestartIntentAsync(path, CancellationToken.None);
        var started = new List<string>();
        var recovery = await RuntimeRestartRecovery.RecoverAsync(
            intent!,
            selectedProfileId: "restored",
            defaultProfileId: "default",
            profileExists: (id, _) => Task.FromResult(id is "restored" or "default"),
            startCore: (id, _) =>
            {
                started.Add(id);
                return Task.FromResult(OperationView.Ok("core.started"));
            },
            _ => { },
            CancellationToken.None);

        await (recovery.Source == RuntimeRestartRecoverySource.SelectedProfile).Should().BeTrue();
        await started.SequenceEqual(["restored"]).Should().BeTrue();
        await File.Exists(path).Should().BeTrue();
        await V2rayRuntime.DeleteRuntimeIntentAsync(path);
        await File.Exists(path).Should().BeFalse();
    }

    private static async Task<(RuntimeRestartRecoveryResult Result, List<string> Started, List<string> Logs)> RecoverAsync(
        RuntimeRestartIntent intent,
        string? selected,
        string? defaultProfile,
        HashSet<string> existing)
    {
        var started = new List<string>();
        var logs = new List<string>();
        var result = await RuntimeRestartRecovery.RecoverAsync(
            intent,
            selected,
            defaultProfile,
            (id, _) => Task.FromResult(existing.Contains(id)),
            (id, _) =>
            {
                started.Add(id);
                return Task.FromResult(OperationView.Ok("core.started"));
            },
            logs.Add,
            CancellationToken.None);
        return (result, started, logs);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-WebAPI-restore-intent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

public class FailedRestoreRuntimeRecoveryTests
{
    [Test]
    public async Task TrackedCoreProcessKeepsTheRuntimeFaultedWithoutStartingAnotherCore()
    {
        var started = new List<string?>();
        var faulted = new List<(int[] ProcessIds, string Failure)>();
        var logs = new List<string>();

        var result = await V2rayRuntime.RecoverRuntimeAfterFailedRestoreAsync(
            new RuntimeRestartIntent(true, "old"),
            [4242],
            (processIds, failure) => faulted.Add((processIds, failure)),
            (profileId, _) =>
            {
                started.Add(profileId);
                return Task.FromResult(OperationView.Ok(ApiMessageKeys.CoreStarted));
            },
            logs.Add,
            CancellationToken.None);

        await (result is null).Should().BeTrue();
        await started.Count.Should().BeEqualTo(0);
        await faulted.Count.Should().BeEqualTo(1);
        await faulted[0].ProcessIds.SequenceEqual([4242]).Should().BeTrue();
        await faulted[0].Failure
            .Contains("could not confirm that the previous Core had stopped", StringComparison.Ordinal)
            .Should().BeTrue();
        await logs.Any(log => log.Contains("could not confirm", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public async Task StoppedCoreWithAnUnrelatedPortOwnerStillAttemptsTheNormalStart()
    {
        // Web's tracked Core is gone. An unrelated program has taken over the old proxy
        // port; that listener must not be mistaken for the previous Core surviving.
        var started = new List<string?>();
        var faulted = false;
        var logs = new List<string>();

        var result = await V2rayRuntime.RecoverRuntimeAfterFailedRestoreAsync(
            new RuntimeRestartIntent(true, "old"),
            [],
            (_, _) => faulted = true,
            (profileId, _) =>
            {
                started.Add(profileId);
                // The normal launch preflight owns the port-conflict decision.
                return Task.FromResult(OperationView.Fail(
                    "proxy_port_in_use",
                    ApiMessageKeys.CorePortInUse,
                    new { port = 10808 }));
            },
            logs.Add,
            CancellationToken.None);

        await faulted.Should().BeFalse();
        await started.SequenceEqual(["old"]).Should().BeTrue();
        await (result?.Code == "proxy_port_in_use").Should().BeTrue();
        await logs.Any(log => log.Contains("proxy_port_in_use", StringComparison.Ordinal)).Should().BeTrue();
        await logs.Any(log => log.Contains("could not confirm", StringComparison.Ordinal)).Should().BeFalse();
    }

    [Test]
    public async Task FailedRestoreWithStoppedRuntimeIntentNeitherStartsNorFaultsTheRuntime()
    {
        var started = new List<string?>();
        var faulted = false;

        var result = await V2rayRuntime.RecoverRuntimeAfterFailedRestoreAsync(
            new RuntimeRestartIntent(false, "old"),
            [],
            (_, _) => faulted = true,
            (profileId, _) =>
            {
                started.Add(profileId);
                return Task.FromResult(OperationView.Ok(ApiMessageKeys.CoreStarted));
            },
            _ => { },
            CancellationToken.None);

        await (result is null).Should().BeTrue();
        await faulted.Should().BeFalse();
        await started.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task SuccessfulTrackedStartIsReportedWithoutAFailureLog()
    {
        var started = new List<string?>();
        var logs = new List<string>();

        var result = await V2rayRuntime.RecoverRuntimeAfterFailedRestoreAsync(
            new RuntimeRestartIntent(true, "old"),
            [],
            (_, _) => throw new InvalidOperationException("the runtime must not be faulted when no tracked process remains"),
            (profileId, _) =>
            {
                started.Add(profileId);
                return Task.FromResult(OperationView.Ok(ApiMessageKeys.CoreStarted));
            },
            logs.Add,
            CancellationToken.None);

        await (result?.Success).Should().BeTrue();
        await started.SequenceEqual(["old"]).Should().BeTrue();
        await logs.Count.Should().BeEqualTo(0);
    }
}
