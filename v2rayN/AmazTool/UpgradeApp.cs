using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AmazTool
{
    internal class UpgradeApp
    {
        public static void Upgrade(string fileName)
        {
            Console.WriteLine(fileName);
            Console.WriteLine("In progress, please wait...(正在进行中，请等待)");

            Thread.Sleep(9000);

            if (!File.Exists(fileName))
            {
                Console.WriteLine("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                return;
            }

            Console.WriteLine("Try to end the process(尝试结束进程).");
            try
            {
                var path = GetPath(V2rayN);
                Console.WriteLine(path);
                var existing = Process.GetProcessesByName(V2rayN);
                var pp = existing.FirstOrDefault(p => p.MainModule?.FileName != null && p.MainModule?.FileName.Contains(path) == true);
                pp?.Kill();
                pp?.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                // Access may be denied without admin right. The user may not be an administrator.
                Console.WriteLine("Failed to close v2rayN(关闭v2rayN失败).\n" +
                    "Close it manually, or the upgrade may fail.(请手动关闭正在运行的v2rayN，否则可能升级失败。\n\n" + ex.StackTrace);
            }

            Console.WriteLine("Start extracting files(开始解压文件).");
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
                Console.WriteLine("Upgrade Failed(升级失败)." + ex.StackTrace);
                //return;
            }
            if (sb.Length > 0)
            {
                Console.WriteLine("Upgrade Failed(升级失败)." + sb.ToString());
                //return;
            }

            Console.WriteLine("Start v2rayN, please wait...(正在重启，请等待)");
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