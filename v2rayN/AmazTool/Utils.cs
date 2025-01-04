using System.Diagnostics;

namespace AmazTool
{
    internal class Utils
    {
        public static string GetExePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
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

        public static string V2rayN => "v2rayN";

        public static void StartV2RayN()
        {
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
    }
}