using System.Diagnostics;
using System.IO.Compression;
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

            Thread.Sleep(10000);

            try
            {
                Process[] existing = Process.GetProcessesByName("v2rayN");
                foreach (Process p in existing)
                {
                    var path = p.MainModule?.FileName ?? "";
                    if (path.StartsWith(GetPath("v2rayN")))
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
                string startKey = "v2rayN/";

                using ZipArchive archive = ZipFile.OpenRead(fileName);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    try
                    {
                        if (entry.Length == 0)
                        {
                            continue;
                        }
                        string fullName = entry.FullName;
                        if (fullName.StartsWith(startKey))
                        {
                            fullName = fullName[startKey.Length..];
                        }
                        if (string.Equals(GetExePath(), GetPath(fullName), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(GetExePath(), thisAppOldFile);
                        }

                        string entryOutputPath = GetPath(fullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(entryOutputPath)!);
                        entry.ExtractToFile(entryOutputPath, true);
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
                Console.WriteLine("Upgrade Failed,Hold ctrl + c to copy to clipboard.\n" +
                    "(升级失败,按住ctrl+c可以复制到剪贴板)." + sb.ToString());
                return;
            }

            Process.Start("v2rayN");
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
    }
}