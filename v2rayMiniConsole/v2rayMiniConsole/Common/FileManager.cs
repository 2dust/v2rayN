﻿using System.IO;
using System.IO.Compression;
using System.Text;

namespace v2rayN
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
    }
}