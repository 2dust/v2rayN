using System.IO.Compression;

namespace ServiceLib.Tests.Common;

public class BackupArchiveServiceTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExcludedRemoteBackup_OmitsWebDavSettings_AndRestoreKeepsLocalSettings(bool localOption)
    {
        var root = CreateTempRoot();
        var stageDirectory = string.Empty;
        try
        {
            var configDirectory = CreateConfigDirectory(root);
            var archivePath = Path.Combine(root, "remote.zip");
            var configPath = Path.Combine(configDirectory, Global.ConfigFileName);
            var sourceConfig = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
            sourceConfig["webdavitem"] = sourceConfig[nameof(Config.WebDavItem)]!.DeepClone();
            File.WriteAllText(configPath, sourceConfig.ToJsonString());
            var originalConfig = File.ReadAllText(configPath);

            await BackupArchiveService.TryCreateBackup(configDirectory, archivePath, true).Should().BeTrue();
            var archiveConfig = ReadConfigFromArchive(archivePath);
            await archiveConfig.Any(item => item.Key.Equals(nameof(Config.WebDavItem), StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
            await File.ReadAllText(configPath).Should().BeEqualTo(originalConfig);
            await File.ReadAllBytes(Path.Combine(configDirectory, "guiNDB.db")).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });

            var localSettings = new WebDavItem
            {
                Url = "https://reader.example/dav",
                UserName = "reader",
                Password = "reader-secret",
                DirName = "reader-dir",
                ExcludeFromRemoteBackup = localOption,
            };
            await BackupArchiveService.TryStageRestore(archivePath, localSettings, out stageDirectory).Should().BeTrue();

            var restored = JsonNode.Parse(File.ReadAllText(Path.Combine(stageDirectory, Global.ConfigFileName)))!.AsObject();
            var webDav = restored[nameof(Config.WebDavItem)]!.AsObject();
            await webDav[nameof(WebDavItem.Url)]!.GetValue<string>().Should().BeEqualTo(localSettings.Url);
            await webDav[nameof(WebDavItem.UserName)]!.GetValue<string>().Should().BeEqualTo(localSettings.UserName);
            await webDav[nameof(WebDavItem.Password)]!.GetValue<string>().Should().BeEqualTo(localSettings.Password);
            await webDav[nameof(WebDavItem.DirName)]!.GetValue<string>().Should().BeEqualTo(localSettings.DirName);
            await webDav[nameof(WebDavItem.ExcludeFromRemoteBackup)]!.GetValue<bool>().Should().BeEqualTo(localOption);
            await restored["OtherSetting"]!.GetValue<string>().Should().BeEqualTo("from-backup");
            await File.ReadAllBytes(Path.Combine(stageDirectory, "guiNDB.db")).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        }
        finally
        {
            if (Directory.Exists(stageDirectory))
            {
                Directory.Delete(stageDirectory, true);
            }
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task CompleteBackup_RestoresWebDavSettingsFromArchive()
    {
        var root = CreateTempRoot();
        var stageDirectory = string.Empty;
        try
        {
            var configDirectory = CreateConfigDirectory(root);
            var archivePath = Path.Combine(root, "complete.zip");
            await BackupArchiveService.TryCreateBackup(configDirectory, archivePath, false).Should().BeTrue();

            var archiveConfig = ReadConfigFromArchive(archivePath);
            await archiveConfig.ContainsKey(nameof(Config.WebDavItem)).Should().BeTrue();

            var localSettings = new WebDavItem { UserName = "reader", ExcludeFromRemoteBackup = false };
            await BackupArchiveService.TryStageRestore(archivePath, localSettings, out stageDirectory).Should().BeTrue();

            var restored = JsonNode.Parse(File.ReadAllText(Path.Combine(stageDirectory, Global.ConfigFileName)))!.AsObject();
            var webDav = restored[nameof(Config.WebDavItem)]!.AsObject();
            await webDav[nameof(WebDavItem.UserName)]!.GetValue<string>().Should().BeEqualTo("owner");
            await webDav[nameof(WebDavItem.ExcludeFromRemoteBackup)]!.GetValue<bool>().Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(stageDirectory))
            {
                Directory.Delete(stageDirectory, true);
            }
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task InvalidArchive_IsRejectedBeforeRestore()
    {
        var root = CreateTempRoot();
        try
        {
            var archivePath = Path.Combine(root, "invalid.zip");
            File.WriteAllText(archivePath, "not a ZIP");
            await BackupArchiveService.TryStageRestore(archivePath, new WebDavItem(), out var stageDirectory).Should().BeFalse();
            await string.IsNullOrEmpty(stageDirectory).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ArchiveWithoutConfig_IsRejectedBeforeRestore()
    {
        var root = CreateTempRoot();
        try
        {
            var archivePath = Path.Combine(root, "missing-config.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("guiConfigs/guiNDB.db");
                using var stream = entry.Open();
                stream.WriteByte(1);
            }

            await BackupArchiveService.TryStageRestore(archivePath, new WebDavItem(), out var stageDirectory).Should().BeFalse();
            await string.IsNullOrEmpty(stageDirectory).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task InvalidConfig_DoesNotCreateExcludedBackup()
    {
        var root = CreateTempRoot();
        try
        {
            var configDirectory = CreateConfigDirectory(root);
            File.WriteAllText(Path.Combine(configDirectory, Global.ConfigFileName), "{invalid json");
            var archivePath = Path.Combine(root, "remote.zip");

            await BackupArchiveService.TryCreateBackup(configDirectory, archivePath, true).Should().BeFalse();
            await File.Exists(archivePath).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"v2rayN-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateConfigDirectory(string root)
    {
        var configDirectory = Path.Combine(root, "guiConfigs");
        Directory.CreateDirectory(configDirectory);
        var config = new JsonObject
        {
            [nameof(Config.WebDavItem)] = new JsonObject
            {
                [nameof(WebDavItem.Url)] = "https://owner.example/dav",
                [nameof(WebDavItem.UserName)] = "owner",
                [nameof(WebDavItem.Password)] = "owner-secret",
                [nameof(WebDavItem.DirName)] = "owner-dir",
                [nameof(WebDavItem.ExcludeFromRemoteBackup)] = true,
            },
            ["OtherSetting"] = "from-backup",
        };
        File.WriteAllText(Path.Combine(configDirectory, Global.ConfigFileName), config.ToJsonString());
        File.WriteAllBytes(Path.Combine(configDirectory, "guiNDB.db"), [1, 2, 3]);
        return configDirectory;
    }

    private static JsonObject ReadConfigFromArchive(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.Entries.Single(x => x.Name == Global.ConfigFileName);
        using var reader = new StreamReader(entry.Open());
        return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
    }
}
