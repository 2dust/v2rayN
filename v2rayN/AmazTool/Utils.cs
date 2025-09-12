using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

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

    public static bool IsPackagedInstall()
    {
        try
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                return false;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")))
                return true;

            var sp = StartupPath()?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(sp) && sp.StartsWith("/opt/v2rayN", StringComparison.Ordinal))
                return true;

            var procPath = Environment.ProcessPath;
            var procDir = string.IsNullOrEmpty(procPath)
                ? ""
                : Path.GetDirectoryName(procPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(procDir) && procDir.StartsWith("/opt/v2rayN", StringComparison.Ordinal))
                return true;
        }
        catch
        {
        }
        return false;
    }
}
