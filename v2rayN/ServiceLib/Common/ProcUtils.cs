using System.Diagnostics;

namespace ServiceLib.Common;

public static class ProcUtils
{
    private static readonly string _tag = "ProcUtils";

    public static void ProcessStart(string? fileName, string arguments = "")
    {
        ProcessStart(fileName, arguments, null);
    }

    public static int? ProcessStart(string? fileName, string arguments, string? dir)
    {
        if (fileName.IsNullOrEmpty())
        {
            return null;
        }
        try
        {
            if (fileName.Contains(' ')) fileName = fileName.AppendQuotes();
            if (arguments.Contains(' ')) arguments = arguments.AppendQuotes();

            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = dir
                }
            };
            process.Start();
            return process.Id;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return null;
    }

    public static void RebootAsAdmin(bool blAdmin = true)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                Arguments = Global.RebootAs,
                WorkingDirectory = Utils.StartupPath(),
                FileName = Utils.GetExePath().AppendQuotes(),
                Verb = blAdmin ? "runas" : null,
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public static async Task ProcessKill(int pid)
    {
        try
        {
            await ProcessKill(Process.GetProcessById(pid), false);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public static async Task ProcessKill(Process? proc, bool review)
    {
        if (proc is null)
        {
            return;
        }

        var fileName = review ? proc?.MainModule?.FileName : null;
        var processName = review ? proc?.ProcessName : null;

        try { proc?.Kill(true); } catch (Exception ex) { Logging.SaveLog(_tag, ex); }
        try { proc?.Kill(); } catch (Exception ex) { Logging.SaveLog(_tag, ex); }
        try { proc?.Close(); } catch (Exception ex) { Logging.SaveLog(_tag, ex); }
        try { proc?.Dispose(); } catch (Exception ex) { Logging.SaveLog(_tag, ex); }

        await Task.Delay(300);
        if (review && fileName != null)
        {
            var proc2 = Process.GetProcessesByName(processName)
                .FirstOrDefault(t => t.MainModule?.FileName == fileName);
            if (proc2 != null)
            {
                Logging.SaveLog($"{_tag}, KillProcess not completing the job");
                await ProcessKill(proc2, false);
                proc2 = null;
            }
        }
    }
}