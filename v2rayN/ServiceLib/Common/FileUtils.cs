using System.Formats.Tar;
using System.IO.Compression;

namespace ServiceLib.Common;

public static class FileUtils
{
    private static readonly string _tag = "FileManager";

    public static bool ByteArrayToFile(string fileName, byte[] content)
    {
        try
        {
            File.WriteAllBytes(fileName, content);
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return false;
    }

    public static void DecompressFile(string fileName, byte[] content)
    {
        try
        {
            using var fs = File.Create(fileName);
            using GZipStream input = new(new MemoryStream(content), CompressionMode.Decompress, false);
            input.CopyTo(fs);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public static void DecompressFile(string fileName, string toPath, string? toName)
    {
        try
        {
            FileInfo fileInfo = new(fileName);
            using var originalFileStream = fileInfo.OpenRead();
            using var decompressedFileStream = File.Create(toName != null ? Path.Combine(toPath, toName) : toPath);
            using GZipStream decompressionStream = new(originalFileStream, CompressionMode.Decompress);
            decompressionStream.CopyTo(decompressedFileStream);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public static void DecompressTarFile(string fileName, string toPath)
    {
        try
        {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            using var gz = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: true);
            TarFile.ExtractToDirectory(gz, toPath, overwriteFiles: true);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public static string NonExclusiveReadAllText(string path)
    {
        return NonExclusiveReadAllText(path, Encoding.Default);
    }

    private static string NonExclusiveReadAllText(string path, Encoding encoding)
    {
        try
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader sr = new(fs, encoding);
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            throw;
        }
    }

    public static bool ZipExtractToFile(string fileName, string toPath, string ignoredName)
    {
        try
        {
            using var archive = ZipFile.OpenRead(fileName);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0)
                {
                    continue;
                }
                try
                {
                    if (ignoredName.IsNotEmpty() && entry.Name.Contains(ignoredName))
                    {
                        continue;
                    }
                    entry.ExtractToFile(Path.Combine(toPath, entry.Name), true);
                }
                catch (IOException ex)
                {
                    Logging.SaveLog(_tag, ex);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return false;
        }
        return true;
    }

    public static List<string>? GetFilesFromZip(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }
        try
        {
            using var archive = ZipFile.OpenRead(fileName);
            return archive.Entries.Select(entry => entry.FullName).ToList();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    public static bool CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
    {
        try
        {
            if (File.Exists(destinationArchiveFileName))
            {
                File.Delete(destinationArchiveFileName);
            }

            ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, CompressionLevel.SmallestSize, true);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return false;
        }
        return true;
    }

    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool overwrite, string? ignoredName = null)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        // Cache directories before we start copying
        var dirs = dir.GetDirectories();

        // Create the destination directory
        _ = Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in dir.GetFiles())
        {
            if (ignoredName.IsNotEmpty() && file.Name.Contains(ignoredName))
            {
                continue;
            }
            if (file.Extension == file.Name)
            {
                continue;
            }
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            if (!overwrite && File.Exists(targetFilePath))
            {
                continue;
            }
            _ = file.CopyTo(targetFilePath, overwrite);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (var subDir in dirs)
            {
                var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true, overwrite, ignoredName);
            }
        }
    }

    public static void DeleteExpiredFiles(string sourceDir, DateTime dtLine)
    {
        try
        {
            var files = Directory.GetFiles(sourceDir, "*.*");
            foreach (var filePath in files)
            {
                var file = new FileInfo(filePath);
                if (file.CreationTime >= dtLine)
                {
                    continue;
                }
                file.Delete();
            }
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// Creates a Linux shell file with the specified contents.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="contents"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public static async Task<string> CreateLinuxShellFile(string fileName, string contents, bool overwrite)
    {
        var shFilePath = Utils.GetBinConfigPath(fileName);

        // Check if the file already exists and if we should overwrite it
        if (!overwrite && File.Exists(shFilePath))
        {
            return shFilePath;
        }

        File.Delete(shFilePath);
        await File.WriteAllTextAsync(shFilePath, contents);
        await Utils.SetLinuxChmod(shFilePath);

        return shFilePath;
    }
}
