namespace v2rayN.WebAPI.Services;

/// <summary>
/// Adds Web-owned backup/verification/rollback around ServiceLib's public GeoFiles updater.
/// The updater remains responsible for deciding which files to download and how to download them.
/// </summary>
internal static class GeoFilesUpdateTransaction
{
    public static async Task ApplyAsync(
        IEnumerable<string> managedFiles,
        IEnumerable<string> requiredFiles,
        Func<CancellationToken, Task> update,
        CancellationToken cancellationToken,
        Func<Func<Task>, CancellationToken, Task>? beforeCommit = null)
    {
        var required = requiredFiles
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
        var originalTargets = managedFiles
            .Concat(required)
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
        var backupRoot = Path.Combine(Path.GetTempPath(), $"v2rayn-WebAPI-geofiles-{Guid.NewGuid():N}");
        var backups = new Dictionary<string, GeoFileBackup>(PathComparer);
        var transactionCompleted = false;
        var rollbackCompleted = false;
        var updateStarted = false;

        Task RollBackFilesAsync()
        {
            if (rollbackCompleted) return Task.CompletedTask;

            var originallyExisting = backups.Keys.ToHashSet(PathComparer);
            foreach (var target in originalTargets)
            {
                if (!originallyExisting.Contains(target) && (File.Exists(target) || IsSymbolicLink(target)))
                {
                    File.Delete(target);
                }
            }

            foreach (var (target, backup) in backups)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (IsSymbolicLink(target)) File.Delete(target);
                File.Copy(backup.BackupPath, target, overwrite: true);
                File.SetLastWriteTimeUtc(target, backup.LastWriteTimeUtc);
            }
            rollbackCompleted = true;
            return Task.CompletedTask;
        }

        try
        {
            Directory.CreateDirectory(backupRoot);
            for (var index = 0; index < originalTargets.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = originalTargets[index];
                if (Directory.Exists(target))
                {
                    throw new IOException($"GeoFiles target is a directory: {target}");
                }
                if (IsSymbolicLink(target))
                {
                    throw new IOException($"GeoFiles target is a symbolic link and cannot be updated safely: {target}");
                }
                if (!File.Exists(target)) continue;
                var backup = Path.Combine(backupRoot, index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture));
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(target);
                File.Copy(target, backup);
                backups.Add(target, new GeoFileBackup(backup, lastWriteTimeUtc));
            }

            try
            {
                updateStarted = true;
                await update(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var requiredFile in required)
                {
                    if (!File.Exists(requiredFile) || new FileInfo(requiredFile).Length <= 0)
                    {
                        throw new IOException($"GeoFiles update did not produce a non-empty {Path.GetFileName(requiredFile)}.");
                    }
                }
                if (beforeCommit is not null) await beforeCommit(RollBackFilesAsync, cancellationToken);
                transactionCompleted = true;
            }
            catch (Exception updateException)
            {
                if (!rollbackCompleted)
                {
                    try
                    {
                        await RollBackFilesAsync();
                    }
                    catch (Exception rollbackException)
                    {
                        throw new GeoFilesUpdateRollbackException(backupRoot, updateException, rollbackException);
                    }
                }

                throw;
            }
        }
        finally
        {
            if ((transactionCompleted || rollbackCompleted || !updateStarted) && Directory.Exists(backupRoot))
            {
                try { Directory.Delete(backupRoot, recursive: true); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A cleanup failure does not invalidate a successful update or rollback.
                }
            }
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            if (new FileInfo(path).LinkTarget is not null) return true;
            return File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private sealed record GeoFileBackup(string BackupPath, DateTime LastWriteTimeUtc);
}

internal sealed class GeoFilesUpdateRollbackException(
    string backupRoot,
    Exception updateException,
    Exception rollbackException)
    : IOException(
        $"GeoFiles update failed and rollback is incomplete; the backup set was retained at {backupRoot}.",
        new AggregateException(updateException, rollbackException))
{
    public string BackupRoot { get; } = backupRoot;
}
