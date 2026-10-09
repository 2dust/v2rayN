using System.IO.Compression;

namespace ServiceLib.Common;

public static class BackupArchiveService
{
    private const string ConfigDirectoryName = "guiConfigs";

    public static bool TryCreateBackup(string configDirectory, string archivePath, bool excludeWebDavSettings)
    {
        var tempDirectory = Utils.GetTempPath($"v2rayN_{Guid.NewGuid():N}");
        var tempArchivePath = $"{archivePath}.{Guid.NewGuid():N}.tmp";
        var success = false;
        var publishedArchive = false;

        try
        {
            var tempConfigDirectory = Path.Combine(tempDirectory, ConfigDirectoryName);
            FileUtils.CopyDirectory(configDirectory, tempConfigDirectory, false, true);

            if (excludeWebDavSettings)
            {
                var configPath = Path.Combine(tempConfigDirectory, Global.ConfigFileName);
                var config = ReadConfig(File.ReadAllText(configPath));
                foreach (var key in config.Select(item => item.Key)
                             .Where(key => key.Equals(nameof(Config.WebDavItem), StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    config.Remove(key);
                }
                File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }

            if (!FileUtils.CreateFromDirectory(tempDirectory, tempArchivePath))
            {
                throw new IOException("Failed to create the backup archive.");
            }

            if (excludeWebDavSettings)
            {
                using var archive = ZipFile.OpenRead(tempArchivePath);
                if (HasWebDavSettings(ReadConfigEntry(archive)))
                {
                    throw new InvalidDataException("The backup archive still contains WebDAV settings.");
                }
            }

            File.Move(tempArchivePath, archivePath, true);
            publishedArchive = true;
            success = true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(BackupArchiveService), ex);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(BackupArchiveService), ex);
                success = false;
            }

            try
            {
                if (File.Exists(tempArchivePath))
                {
                    File.Delete(tempArchivePath);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(BackupArchiveService), ex);
                success = false;
            }
        }

        if (!success && excludeWebDavSettings && publishedArchive && File.Exists(archivePath))
        {
            try
            {
                File.Delete(archivePath);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(BackupArchiveService), ex);
            }
        }

        return success;
    }

    public static bool TryStageRestore(string archivePath, WebDavItem localWebDavSettings, out string stageDirectory)
    {
        stageDirectory = Utils.GetTempPath($"v2rayN_restore_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stageDirectory);
            using var archive = ZipFile.OpenRead(archivePath);
            var files = new HashSet<string>(Utils.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var segments = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2 || !segments[^2].Equals(ConfigDirectoryName, StringComparison.Ordinal)
                    || entry.Name is "." or ".." || Path.GetFileName(entry.Name) != entry.Name
                    || !files.Add(entry.Name))
                {
                    throw new InvalidDataException("Invalid backup archive entry.");
                }

                using var input = entry.Open();
                using var output = File.Create(Path.Combine(stageDirectory, entry.Name));
                input.CopyTo(output);
            }

            var configPath = Path.Combine(stageDirectory, Global.ConfigFileName);
            var config = ReadConfig(File.ReadAllText(configPath));
            if (!HasWebDavSettings(config))
            {
                config[nameof(Config.WebDavItem)] = JsonSerializer.SerializeToNode(localWebDavSettings);
                File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }

            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(BackupArchiveService), ex);
            try
            {
                if (Directory.Exists(stageDirectory))
                {
                    Directory.Delete(stageDirectory, true);
                }
            }
            catch (Exception cleanupException)
            {
                Logging.SaveLog(nameof(BackupArchiveService), cleanupException);
            }
            stageDirectory = string.Empty;
            return false;
        }
    }

    private static JsonObject ReadConfigEntry(ZipArchive archive)
    {
        var configEntries = archive.Entries.Where(entry =>
        {
            var segments = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 2 && segments[^2] == ConfigDirectoryName && entry.Name == Global.ConfigFileName;
        }).ToList();

        if (configEntries.Count != 1)
        {
            throw new InvalidDataException("The backup archive must contain one configuration file.");
        }

        using var reader = new StreamReader(configEntries[0].Open());
        return ReadConfig(reader.ReadToEnd());
    }

    private static JsonObject ReadConfig(string json)
    {
        return JsonUtils.ParseJson(json) as JsonObject
            ?? throw new InvalidDataException("Invalid configuration JSON in the backup archive.");
    }

    private static bool HasWebDavSettings(JsonObject config)
    {
        return config.Any(item => item.Key.Equals(nameof(Config.WebDavItem), StringComparison.OrdinalIgnoreCase));
    }
}
