using System.Diagnostics;

namespace AmazTool;

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
        var startupPath = StartupPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return startupPath;
        }
        return Path.Combine(startupPath, fileName);
    }

    /// <summary>
    /// 把升级包内的条目名解析为安装目录下的绝对路径。
    /// 条目名试图逃出安装目录时返回 false，并给出原因。
    /// </summary>
    /// <remarks>
    /// 升级包来自网络，条目名不可信。以下情形一律拒绝：
    ///   · 绝对路径（如 /etc/passwd、C:\Windows\...）
    ///   · UNC 路径（\\server\share）
    ///   · 盘符或 NTFS 备用数据流（含 ':'）
    ///   · 任何一段为 ".."（相对路径回溯）
    ///   · 规范化之后仍不在安装目录之下的
    /// </remarks>
    public static bool TryGetEntryPath(string entryName, out string entryPath, out string reason)
    {
        entryPath = string.Empty;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(entryName))
        {
            reason = "empty entry name";
            return false;
        }

        // 安装目录的完整路径，且确保以分隔符结尾，避免 "C:\app" 匹配到 "C:\app2"
        var root = Path.GetFullPath(StartupPath());
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        // 统一分隔符后再判断，同时挡住 "/" 与 "\" 混用
        var normalized = entryName.Replace('\\', '/');

        if (normalized.StartsWith('/'))
        {
            reason = "absolute path";
            return false;
        }
        if (normalized.StartsWith("//"))
        {
            reason = "UNC path";
            return false;
        }
        if (normalized.Contains(':'))
        {
            reason = "drive letter or alternate data stream";
            return false;
        }
        if (Path.IsPathRooted(entryName))
        {
            reason = "rooted path";
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                reason = "parent directory reference";
                return false;
            }
        }

        var combined = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            reason = "escapes install directory";
            return false;
        }

        entryPath = combined;
        return true;
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

    public static void Waiting(int second)
    {
        for (var i = second; i > 0; i--)
        {
            Console.WriteLine(i);
            Thread.Sleep(1000);
        }
    }
}
