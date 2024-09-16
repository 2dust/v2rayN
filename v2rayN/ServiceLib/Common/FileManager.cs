using System.IO.Compression;
using System.Text;

namespace ServiceLib.Common
{
    public static class FileManager
    {
        public static bool ByteArrayToFile(string fileName, byte[] content)
        {
            try
            {
                File.WriteAllBytes(fileName, content);
                return true;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return false;
        }

        public static void UncompressedFile(string fileName, byte[] content)
        {
            try
            {
                using FileStream fs = File.Create(fileName);
                using GZipStream input = new(new MemoryStream(content), CompressionMode.Decompress, false);
                input.CopyTo(fs);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public static void UncompressedFile(string fileName, string toPath, string? toName)
        {
            try
            {
                FileInfo fileInfo = new(fileName);
                using FileStream originalFileStream = fileInfo.OpenRead();
                using FileStream decompressedFileStream = File.Create(toName != null ? Path.Combine(toPath, toName) : toPath);
                using GZipStream decompressionStream = new(originalFileStream, CompressionMode.Decompress);
                decompressionStream.CopyTo(decompressedFileStream);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public static string NonExclusiveReadAllText(string path)
        {
            return NonExclusiveReadAllText(path, Encoding.Default);
        }

        public static string NonExclusiveReadAllText(string path, Encoding encoding)
        {
            try
            {
                using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader sr = new(fs, encoding);
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                throw;
            }
        }

        public static bool ZipExtractToFile(string fileName, string toPath, string ignoredName)
        {
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(fileName);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.Length == 0)
                    {
                        continue;
                    }
                    try
                    {
                        if (!Utils.IsNullOrEmpty(ignoredName) && entry.Name.Contains(ignoredName))
                        {
                            continue;
                        }
                        entry.ExtractToFile(Path.Combine(toPath, entry.Name), true);
                    }
                    catch (IOException ex)
                    {
                        Logging.SaveLog(ex.Message, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return false;
            }
            return true;
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
                Logging.SaveLog(ex.Message, ex);
                return false;
            }
            return true;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, string ignoredName)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                if (!Utils.IsNullOrEmpty(ignoredName) && file.Name.Contains(ignoredName))
                {
                    continue;
                }
                if (file.Extension == file.Name)
                {
                    continue;
                }
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true, ignoredName);
                }
            }
        }
    }
}