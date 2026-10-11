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

    public static bool IsPathUnderDirectory(string baseDir, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(baseDir) || string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        var baseFull = Path.GetFullPath(baseDir);
        var targetFull = Path.GetFullPath(targetPath);

        baseFull = baseFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return targetFull.StartsWith(baseFull, GetPathComparison());
    }

    public static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
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
