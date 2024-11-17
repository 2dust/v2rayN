using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace AmazTool
{
    internal class UpgradeApp
    {
        public static void Upgrade(string fileName)
        {
            Console.WriteLine(fileName);

            Thread.Sleep(9000);

            if (!File.Exists(fileName))
            {
                Console.WriteLine(LocalizationHelper.GetLocalizedValue("Upgrade_File_Not_Found"));
                return;
            }

            Console.WriteLine(LocalizationHelper.GetLocalizedValue("Try_Terminate_Process"));
            try
            {
                var existing = Process.GetProcessesByName(V2rayN);
                foreach (var pp in existing)
                {
                    pp?.Kill();
                    pp?.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                // Access may be denied without admin right. The user may not be an administrator.
                Console.WriteLine(LocalizationHelper.GetLocalizedValue("Failed_Terminate_Process") + ex.StackTrace);
            }

            Console.WriteLine(LocalizationHelper.GetLocalizedValue("Start_Unzipping"));
            StringBuilder sb = new();
            try
            {
                string thisAppOldFile = $"{GetExePath()}.tmp";
                File.Delete(thisAppOldFile);
                string splitKey = "/";

                using ZipArchive archive = ZipFile.OpenRead(fileName);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    try
                    {
                        if (entry.Length == 0)
                        {
                            continue;
                        }

                        Console.WriteLine(entry.FullName);

                        var lst = entry.FullName.Split(splitKey);
                        if (lst.Length == 1) continue;
                        string fullName = string.Join(splitKey, lst[1..lst.Length]);

                        if (string.Equals(GetExePath(), GetPath(fullName), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(GetExePath(), thisAppOldFile);
                        }

                        string entryOutputPath = GetPath(fullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(entryOutputPath)!);
                        entry.ExtractToFile(entryOutputPath, true);

                        Console.WriteLine(entryOutputPath);
                    }
                    catch (Exception ex)
                    {
                        sb.Append(ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(LocalizationHelper.GetLocalizedValue("Failed_Upgrade") + ex.StackTrace);
                //return;
            }
            if (sb.Length > 0)
            {
                Console.WriteLine(LocalizationHelper.GetLocalizedValue("Failed_Upgrade") + sb.ToString());
                //return;
            }

            Console.WriteLine(LocalizationHelper.GetLocalizedValue("Restart_v2rayN"));
            Thread.Sleep(9000);
            Process process = new()
            {
                StartInfo = new()
                {
                    UseShellExecute = true,
                    FileName = V2rayN,
                    WorkingDirectory = StartupPath()
                }
            };
            process.Start();
        }

        private static string GetExePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }

        private static string StartupPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetPath(string fileName)
        {
            string startupPath = StartupPath();
            if (string.IsNullOrEmpty(fileName))
            {
                return startupPath;
            }
            return Path.Combine(startupPath, fileName);
        }

        private static string V2rayN => "v2rayN";
    }
}