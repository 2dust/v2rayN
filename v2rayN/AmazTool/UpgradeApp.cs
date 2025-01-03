using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AmazTool
{
    internal class UpgradeApp
    {
        public static void Upgrade(string fileName)
        {
            Console.WriteLine($"{Resx.Resource.StartUnzipping}\n{fileName}");

            Waiting(3);

            if (!File.Exists(fileName))
            {
                Console.WriteLine(Resx.Resource.UpgradeFileNotFound);
                return;
            }

            Console.WriteLine(Resx.Resource.TryTerminateProcess);
            try
            {
                var existing = Process.GetProcessesByName(V2rayN);
                foreach (var pp in existing)
                {
                    var path = pp.MainModule?.FileName ?? "";
                    if (path.StartsWith(GetPath(V2rayN)))
                    {
                        pp?.Kill();
                        pp?.WaitForExit(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                // Access may be denied without admin right. The user may not be an administrator.
                Console.WriteLine(Resx.Resource.FailedTerminateProcess + ex.StackTrace);
            }

            Console.WriteLine(Resx.Resource.StartUnzipping);
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
                Console.WriteLine(Resx.Resource.FailedUpgrade + ex.StackTrace);
                //return;
            }
            if (sb.Length > 0)
            {
                Console.WriteLine(Resx.Resource.FailedUpgrade + sb.ToString());
                //return;
            }

            Console.WriteLine(Resx.Resource.Restartv2rayN);
            Waiting(2);
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

        private static void Waiting(int second)
        {
            for (var i = second; i > 0; i--)
            {
                Console.WriteLine(i);
                Thread.Sleep(1000);
            }
        }

        private static string V2rayN => "v2rayN";
    }
}