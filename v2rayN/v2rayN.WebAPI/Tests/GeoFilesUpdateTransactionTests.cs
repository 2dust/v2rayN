using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class GeoFilesUpdateTransactionTests
{
    [Test]
    public async Task CallbackFailureIsRejectedEvenWhenUpdaterTaskCompletesNormally()
    {
        var completion = new GeoFilesUpdateCompletion();
        completion.Report(success: false, "download failed", isProgressMessage: false);
        await Task.CompletedTask;

        var failed = false;
        try
        {
            completion.EnsureSuccessful();
        }
        catch (IOException exception) when (exception.Message.Contains("download failed", StringComparison.Ordinal))
        {
            failed = true;
        }

        await failed.Should().BeTrue();
    }

    [Test]
    public async Task GeoFilesProgressDoesNotReplaceTheFinalSuccessNotification()
    {
        var completion = new GeoFilesUpdateCompletion();
        completion.Report(success: false, "Downloading geoip.dat", isProgressMessage: true);

        var missingCompletionRejected = false;
        try
        {
            completion.EnsureSuccessful();
        }
        catch (IOException exception) when (exception.Message.Contains("did not report successful completion", StringComparison.Ordinal))
        {
            missingCompletionRejected = true;
        }

        completion.Report(success: true, "GeoFiles updated", isProgressMessage: false);
        completion.EnsureSuccessful();
        await missingCompletionRejected.Should().BeTrue();
    }

    [Test]
    public async Task FailedGeoFilesUpdateRestoresReplacedFilesAndRemovesNewFiles()
    {
        using var directory = new TemporaryDirectory();
        var existing = Path.Combine(directory.Path, "geoip.dat");
        var created = Path.Combine(directory.Path, "geosite-custom.srs");
        await File.WriteAllTextAsync(existing, "old-geoip");

        var failed = false;
        try
        {
            await GeoFilesUpdateTransaction.ApplyAsync(
                [existing, created],
                [existing, created],
                async _ =>
                {
                    await File.WriteAllTextAsync(existing, "partially-updated-geoip");
                    await File.WriteAllTextAsync(created, "new-srs");
                    throw new IOException("simulated later download/apply failure");
                },
                CancellationToken.None);
        }
        catch (IOException exception) when (exception.Message.Contains("simulated later", StringComparison.Ordinal))
        {
            failed = true;
        }

        await failed.Should().BeTrue();
        await (await File.ReadAllTextAsync(existing)).Should().BeEqualTo("old-geoip");
        await File.Exists(created).Should().BeFalse();
    }

    [Test]
    public async Task MissingRequiredGeoFileFailsAndRestoresTheOriginalSet()
    {
        using var directory = new TemporaryDirectory();
        var existing = Path.Combine(directory.Path, "geosite.dat");
        var required = Path.Combine(directory.Path, "geoip.dat");
        await File.WriteAllTextAsync(existing, "old-geosite");
        await File.WriteAllTextAsync(required, "old-geoip");

        var failed = false;
        try
        {
            await GeoFilesUpdateTransaction.ApplyAsync(
                [existing, required],
                [existing, required],
                async _ =>
                {
                    await File.WriteAllTextAsync(existing, "new-geosite");
                    File.Delete(required);
                },
                CancellationToken.None);
        }
        catch (IOException exception) when (exception.Message.Contains("did not produce", StringComparison.Ordinal))
        {
            failed = true;
        }

        await failed.Should().BeTrue();
        await (await File.ReadAllTextAsync(existing)).Should().BeEqualTo("old-geosite");
        await (await File.ReadAllTextAsync(required)).Should().BeEqualTo("old-geoip");
    }

    [Test]
    public async Task VerifiedGeoFilesUpdateKeepsNewFiles()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "geosite.dat");

        await GeoFilesUpdateTransaction.ApplyAsync(
            [target],
            [target],
            token => File.WriteAllTextAsync(target, "verified-update", token),
            CancellationToken.None);

        await (await File.ReadAllTextAsync(target)).Should().BeEqualTo("verified-update");
    }

    [Test]
    public async Task BeforeCommitFailureRestoresGeoFilesAfterCoreRestartFailure()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "geoip.dat");
        await File.WriteAllTextAsync(target, "old-geoip");
        var coreCompletionCalled = false;
        var coreSawRestoredGeo = false;
        var failed = false;

        try
        {
            await GeoFilesUpdateTransaction.ApplyAsync(
                [target],
                [target],
                token => File.WriteAllTextAsync(target, "new-geoip", token),
                CancellationToken.None,
                async (rollbackGeoFiles, _) =>
                {
                    await rollbackGeoFiles();
                    coreCompletionCalled = true;
                    coreSawRestoredGeo = await File.ReadAllTextAsync(target) == "old-geoip";
                    throw new IOException("simulated Core restart failure");
                });
        }
        catch (IOException exception) when (exception.Message.Contains("simulated Core restart failure", StringComparison.Ordinal))
        {
            failed = true;
        }

        await failed.Should().BeTrue();
        await coreCompletionCalled.Should().BeTrue();
        await coreSawRestoredGeo.Should().BeTrue();
        await (await File.ReadAllTextAsync(target)).Should().BeEqualTo("old-geoip");
    }

    [Test]
    public async Task ManualBatchAndRegionalPresetGeoFilesWorkShareOneTransactionGate()
    {
        var gate = new GeoFilesUpdateGate();
        var active = 0;
        var maximumActive = 0;
        var entrants = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Task.WhenAll(new[] { "manual", "batch", "regional-preset" }.Select(name => gate.RunAsync(async () =>
        {
            var nowActive = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximumActive, nowActive);
            entrants.Add(name);
            await Task.Delay(20);
            Interlocked.Decrement(ref active);
        }, CancellationToken.None)));

        await maximumActive.Should().BeEqualTo(1);
        await entrants.OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(new[] { "batch", "manual", "regional-preset" })
            .Should().BeTrue();
    }

    private static void UpdateMaximum(ref int maximum, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (current >= value || Interlocked.CompareExchange(ref maximum, value, current) == current)
            {
                return;
            }
        }
    }

    [Test]
    public async Task FailedUpdateRemovesNewMmdbAndSrsTargetsButLeavesUnmanagedFilesAlone()
    {
        using var directory = new TemporaryDirectory();
        var required = Path.Combine(directory.Path, "geosite.dat");
        var geoip = Path.Combine(directory.Path, "geoip.dat");
        var newMmdb = Path.Combine(directory.Path, "Country.mmdb");
        var newSrs = Path.Combine(directory.Path, "geosite-custom.srs");
        var unrelatedSrs = Path.Combine(directory.Path, "locally-managed.srs");
        var unrelatedGeo = Path.Combine(directory.Path, "geography-not-managed.db");

        try
        {
            await GeoFilesUpdateTransaction.ApplyAsync(
                [required, geoip, newMmdb, newSrs],
                [required, geoip],
                async _ =>
                {
                    await File.WriteAllTextAsync(required, "new-geosite");
                    await File.WriteAllTextAsync(geoip, "new-geoip");
                    await File.WriteAllTextAsync(newMmdb, "new-mmdb");
                    await File.WriteAllTextAsync(newSrs, "new-srs");
                    await File.WriteAllTextAsync(unrelatedSrs, "keep-srs");
                    await File.WriteAllTextAsync(unrelatedGeo, "keep-geo");
                    throw new IOException("simulated callback-reported download failure");
                },
                CancellationToken.None);
        }
        catch (IOException exception) when (exception.Message.Contains("simulated callback-reported", StringComparison.Ordinal))
        {
            // The ServiceLib callback can report failure while its Task completes normally;
            // the Web callback adapter converts that report to this transaction failure.
        }

        await File.Exists(required).Should().BeFalse();
        await File.Exists(geoip).Should().BeFalse();
        await File.Exists(newMmdb).Should().BeFalse();
        await File.Exists(newSrs).Should().BeFalse();
        await (await File.ReadAllTextAsync(unrelatedSrs)).Should().BeEqualTo("keep-srs");
        await (await File.ReadAllTextAsync(unrelatedGeo)).Should().BeEqualTo("keep-geo");
    }

    [Test]
    public async Task CancellationRollsBackChangesToTheManagedSet()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        var existing = Path.Combine(directory.Path, "geoip.dat");
        var newFile = Path.Combine(directory.Path, "geosite-new.srs");
        await File.WriteAllTextAsync(existing, "before-cancel");

        var canceled = false;
        try
        {
            await GeoFilesUpdateTransaction.ApplyAsync(
                [existing, newFile],
                [existing],
                async token =>
                {
                    await File.WriteAllTextAsync(existing, "partial-update", token);
                    await File.WriteAllTextAsync(newFile, "partial-srs", token);
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                },
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        await canceled.Should().BeTrue();
        await (await File.ReadAllTextAsync(existing)).Should().BeEqualTo("before-cancel");
        await File.Exists(newFile).Should().BeFalse();
    }

    [Test]
    public async Task RollbackFailureRetainsItsBackupSetAndReportsBothErrors()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "geoip.dat");
        await File.WriteAllTextAsync(target, "before-update");

        IOException? failure = null;
        try
        {
            await GeoFilesUpdateTransaction.ApplyAsync(
                [target],
                [target],
                _ =>
                {
                    File.Delete(target);
                    Directory.CreateDirectory(target);
                    throw new IOException("simulated update failure");
                },
                CancellationToken.None);
        }
        catch (IOException exception) when (exception.Message.Contains("rollback is incomplete", StringComparison.Ordinal))
        {
            failure = exception;
        }

        await (failure is not null).Should().BeTrue();
        await (failure!.InnerException is AggregateException aggregate
            && aggregate.InnerExceptions.Count == 2).Should().BeTrue();
        const string retainedPrefix = "backup set was retained at ";
        var retainedStart = failure!.Message.IndexOf(retainedPrefix, StringComparison.Ordinal) + retainedPrefix.Length;
        var retainedPath = failure.Message[retainedStart..].TrimEnd('.');
        await Directory.Exists(retainedPath).Should().BeTrue();
        Directory.Delete(retainedPath, recursive: true);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-geofiles-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
