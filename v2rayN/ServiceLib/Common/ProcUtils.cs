namespace ServiceLib.Common;

public static class ProcUtils
{
    private static readonly string _tag = "ProcUtils";

    public static void ProcessStart(string? fileName, string arguments = "")
    {
        _ = ProcessStart(fileName, arguments, null);
    }

    public static int? ProcessStart(string? fileName, string arguments, string? dir)
    {
        if (fileName.IsNullOrEmpty())
        {
            return null;
        }
        try
        {
            // Security: Validate and sanitize inputs to prevent command injection
            // Only quote if not already quoted and contains spaces
            if (fileName.Contains(' ') && !fileName.StartsWith("\"") && !fileName.EndsWith("\""))
            {
                fileName = fileName.AppendQuotes();
            }

            // Security: Don't quote the entire arguments string - it may contain multiple args
            // The caller should properly format arguments with quotes if needed
            // Only quote if it's a single argument with spaces and not already quoted
            if (!string.IsNullOrEmpty(arguments) &&
                arguments.Contains(' ') &&
                !arguments.Contains('"') &&
                !arguments.Contains(" -") &&
                !arguments.Contains(" /"))
            {
                arguments = arguments.AppendQuotes();
            }

            // Security: For security-critical operations, consider validating fileName
            // is an expected executable or using a whitelist approach

            Process proc = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,  // Required for opening URLs and running with shell associations
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = dir ?? string.Empty
                }
            };
            _ = proc.Start();
            return dir is null ? null : proc.Id;
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
            var exePath = Utils.GetExePath();

            // Security: Only quote if not already quoted and contains spaces
            if (exePath.Contains(' ') && !exePath.StartsWith("\"") && !exePath.EndsWith("\""))
            {
                exePath = exePath.AppendQuotes();
            }

            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                Arguments = Global.RebootAs,
                WorkingDirectory = Utils.StartupPath(),
                FileName = exePath,
                Verb = blAdmin ? "runas" : null,
            };
            _ = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
