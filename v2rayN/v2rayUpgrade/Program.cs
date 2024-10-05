using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace v2rayUpgrade
{
    internal static class Program
    {
        private static readonly string defaultFilename = "v2rayN.zip_temp";
        private static string? fileName;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                fileName = Uri.UnescapeDataString(string.Join(" ", args));
            }
            else
            {
                fileName = defaultFilename;
            }
            Console.WriteLine(fileName);
            Console.WriteLine("In progress, please wait...(正在进行中，请等待)");

            Thread.Sleep(5000);

            try
            {
                Process[] existing = Process.GetProcessesByName(V2rayN());
                foreach (Process p in existing)
                {
                    var path = p.MainModule?.FileName ?? "";
                    if (path.StartsWith(GetPath(V2rayN())))
                    {
                        p.Kill();
                        p.WaitForExit(100);
                    }
                }
            }
            catch (Exception ex)
            {
                // Access may be denied without admin right. The user may not be an administrator.
                Console.WriteLine("Failed to close v2rayN(关闭v2rayN失败).\n" +
                    "Close it manually, or the upgrade may fail.(请手动关闭正在运行的v2rayN，否则可能升级失败。\n\n" + ex.StackTrace);
            }

            if (!File.Exists(fileName))
            {
                if (File.Exists(defaultFilename))
                {
                    fileName = defaultFilename;
                }
                else
                {
                    Console.WriteLine("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                    return;
                }
            }

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
                return;
            }
            if (sb.Length > 0)
            {
                Console.WriteLine("Upgrade Failed.\n" +
                    "(升级失败)." + sb.ToString());
                return;
            }

            Console.WriteLine("Start v2rayN, please wait...(正在重启，请等待)");
            Thread.Sleep(3000);
            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = V2rayN(),
                    WorkingDirectory = StartupPath()
                }
            };
            process.Start();
        }

        public static string GetExePath()
        {
            return Environment.ProcessPath ?? string.Empty;
        }

        public static string StartupPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetPath(string fileName)
        {
            string startupPath = StartupPath();
            if (string.IsNullOrEmpty(fileName))
            {
                return startupPath;
            }
            return Path.Combine(startupPath, fileName);
        }

        private static string V2rayN()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (File.Exists(GetPath("v2rayN.exe")))
                    return "v2rayN";
                else
                    return "v2rayN.Desktop";
            }
            else
            {
                return "v2rayN.Desktop";
            }
        }
    }
}