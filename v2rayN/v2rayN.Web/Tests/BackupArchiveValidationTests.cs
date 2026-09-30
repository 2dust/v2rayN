using System.IO.Compression;
using SQLite;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class BackupArchiveValidationTests
{
    [Test]
    public async Task SafeServiceLibBackupIsAccepted()
    {
        var archivePath = CreateArchive(("guiConfigs/guiNConfig.json", "{}"));
        try
        {
            await V2rayRuntime.IsSafeBackupArchive(archivePath).Should().BeTrue();
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [Test]
    public async Task ArchiveTraversalPathIsRejected()
    {
        var archivePath = CreateArchive(
            ("guiConfigs/guiNConfig.json", "{}"),
            ("guiConfigs/../../outside.txt", "not safe"));
        try
        {
            await V2rayRuntime.IsSafeBackupArchive(archivePath).Should().BeFalse();
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [Test]
    public async Task DesktopBackupSchemaWithOlderNullableColumnsIsAccepted()
    {
        using var directory = new TemporaryDirectory();
        var database = Path.Combine(directory.Path, "desktop.db");
        using (var connection = new SQLiteConnection(database))
        {
            connection.Execute("CREATE TABLE SubItem (Id varchar primary key not null, Remarks varchar, Url varchar);");
            connection.Execute("INSERT INTO SubItem (Id, Remarks, Url) VALUES (?, ?, ?);", "legacy-sub", "Legacy", "https://example.test");
        }

        var archive = CreateArchive(
            ("v2rayN_20260925/guiConfigs/guiNConfig.json", "{}"),
            ("v2rayN_20260925/guiConfigs/guiNDB.db", await File.ReadAllBytesAsync(database)));
        var extracted = Path.Combine(directory.Path, "desktop-extracted");
        try
        {
            await V2rayRuntime.IsSafeBackupArchive(archive).Should().BeTrue();
            await V2rayRuntime.TryExtractBackupConfig(archive, extracted).Should().BeTrue();
            await BackupDatabaseCompatibility.IsCompatible(Path.Combine(extracted, "guiNDB.db"), out _).Should().BeTrue();
        }
        finally
        {
            File.Delete(archive);
        }
    }

    [Test]
    public async Task ServiceLibAddsMissingColumnsWithoutDroppingDesktopRowsOrUnknownColumns()
    {
        using var directory = new TemporaryDirectory();
        var database = Path.Combine(directory.Path, "upgrade.db");
        using var connection = new SQLiteConnection(database);
        connection.Execute("CREATE TABLE SubItem (Id varchar primary key not null, Remarks varchar, Url varchar, DesktopOnly varchar);");
        connection.Execute("INSERT INTO SubItem (Id, Remarks, Url, DesktopOnly) VALUES (?, ?, ?, ?);",
            "legacy-sub", "Desktop subscription", "https://example.test", "preserve-me");

        connection.CreateTable<ServiceLib.Models.Entities.SubItem>();

        var restored = connection.Table<ServiceLib.Models.Entities.SubItem>().First();
        var columns = connection.Query<TableColumnRow>("PRAGMA table_info(\"SubItem\");");
        await (restored.Id == "legacy-sub" && restored.Remarks == "Desktop subscription").Should().BeTrue();
        await columns.Any(column => column.Name == "RequestHeaders").Should().BeTrue();
        await columns.Any(column => column.Name == "DesktopOnly").Should().BeTrue();
    }

    [Test]
    public async Task WebBackupSchemaAndEmbeddedAuthAreCompatibleButCannotReplaceLocalAuth()
    {
        using var directory = new TemporaryDirectory();
        var database = Path.Combine(directory.Path, "web.db");
        using (var connection = new SQLiteConnection(database))
        {
            connection.Execute("CREATE TABLE SubItem (Id varchar primary key not null, Remarks varchar, Url varchar, RequestHeaders varchar);");
            connection.Execute("CREATE TABLE ProfileExItem (IndexId varchar primary key not null, Delay integer, IpInfo varchar);");
            connection.Execute("INSERT INTO SubItem (Id, Remarks, Url, RequestHeaders) VALUES (?, ?, ?, ?);", "web-sub", "Web", "https://example.test", "{}");
        }

        var archive = CreateArchive(
            ("backup_web/guiConfigs/guiNConfig.json", "{}"),
            ("backup_web/guiConfigs/guiNDB.db", await File.ReadAllBytesAsync(database)),
            ("backup_web/guiConfigs/web-auth.json", "attacker-provided-auth"));
        var extracted = Path.Combine(directory.Path, "web-extracted");
        var current = Path.Combine(directory.Path, "current-config");
        var candidate = Path.Combine(directory.Path, "candidate-config");
        var backupCopy = Path.Combine(directory.Path, "backup-config");
        Directory.CreateDirectory(current);
        await File.WriteAllTextAsync(Path.Combine(current, "web-auth.json"), "local-auth-verifier");
        await File.WriteAllTextAsync(Path.Combine(current, "desktop-note"), "retain-local-config");
        V2rayRuntime.CopyConfigForBackup(current, backupCopy);

        try
        {
            await V2rayRuntime.IsSafeBackupArchive(archive).Should().BeTrue();
            await File.Exists(Path.Combine(backupCopy, "web-auth.json")).Should().BeFalse();
            await V2rayRuntime.TryExtractBackupConfig(archive, extracted).Should().BeTrue();
            await BackupDatabaseCompatibility.IsCompatible(Path.Combine(extracted, "guiNDB.db"), out _).Should().BeTrue();
            await File.Exists(Path.Combine(extracted, "web-auth.json")).Should().BeFalse();

            V2rayRuntime.PrepareRestoredConfigDirectory(extracted, current, candidate);

            await (await File.ReadAllTextAsync(Path.Combine(candidate, "web-auth.json")))
                .Should().BeEqualTo("local-auth-verifier");
            await (await File.ReadAllTextAsync(Path.Combine(candidate, "desktop-note")))
                .Should().BeEqualTo("retain-local-config");
            await File.Exists(Path.Combine(candidate, "guiNDB.db")).Should().BeTrue();
        }
        finally
        {
            File.Delete(archive);
        }
    }

    [Test]
    public async Task DatabaseWithMissingPrimaryKeyIsRejectedBeforeRestore()
    {
        using var directory = new TemporaryDirectory();
        var database = Path.Combine(directory.Path, "invalid.db");
        using (var connection = new SQLiteConnection(database))
        {
            connection.Execute("CREATE TABLE SubItem (Remarks varchar, Url varchar);");
        }

        await BackupDatabaseCompatibility.IsCompatible(database, out _).Should().BeFalse();
    }

    [Test]
    public async Task FailedConfigDirectorySwapRestoresThePreviouslyActiveConfig()
    {
        using var directory = new TemporaryDirectory();
        var current = Path.Combine(directory.Path, "guiConfigs");
        var missingCandidate = Path.Combine(directory.Path, "missing-candidate");
        var displaced = Path.Combine(directory.Path, "guiConfigs-before-restore");
        Directory.CreateDirectory(current);
        await File.WriteAllTextAsync(Path.Combine(current, "guiNConfig.json"), "old-config");

        var failed = false;
        try
        {
            V2rayRuntime.ReplaceConfigDirectory(missingCandidate, current, displaced);
        }
        catch (DirectoryNotFoundException)
        {
            failed = true;
        }

        await failed.Should().BeTrue();
        await Directory.Exists(current).Should().BeTrue();
        await Directory.Exists(displaced).Should().BeFalse();
        await (await File.ReadAllTextAsync(Path.Combine(current, "guiNConfig.json")))
            .Should().BeEqualTo("old-config");
    }

    private static string CreateArchive(params (string Name, object Content)[] files)
    {
        var path = Path.Combine(Path.GetTempPath(), $"v2rayn-web-test-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            using var entry = archive.CreateEntry(file.Name).Open();
            if (file.Content is byte[] bytes)
            {
                entry.Write(bytes);
            }
            else
            {
                using var writer = new StreamWriter(entry);
                writer.Write((string)file.Content);
            }
        }
        return path;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-backup-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class TableColumnRow
    {
        public string Name { get; set; } = string.Empty;
    }
}
