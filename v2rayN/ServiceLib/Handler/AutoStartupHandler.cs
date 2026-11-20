using Microsoft.Win32;

namespace ServiceLib.Handler;

public static class AutoStartupHandler
{
    private static readonly string _tag = "AutoStartupHandler";

    public static async Task<bool> UpdateTask(Config config)
    {
        if (Utils.IsWindows())
        {
            await ClearTaskWindows(config.GuiItem.AutoRun);

            if (config.GuiItem.AutoRun)
            {
                await SetTaskWindows();
            }
        }
        else if (Utils.IsLinux())
        {
            await ClearTaskLinux();

            if (config.GuiItem.AutoRun)
            {
                await SetTaskLinux();
            }
        }
        else if (Utils.IsMacOS())
        {
            await ClearTaskOSX();

            if (config.GuiItem.AutoRun)
            {
                await SetTaskOSX();
            }
        }

        return true;
    }

    #region Windows

    /// <summary>
    /// Remove duplicate auto-start entries for this installation. Two entries would launch two
    /// instances at logon; the second instance then activates the first one and defeats the
    /// "auto hide on startup" option.
    /// </summary>
    public static async Task CleanupDuplicateAutoRun(Config config)
    {
        if (!Utils.IsWindows())
        {
            return;
        }

        try
        {
            var count = GetCurrentAutoRunRegistryValueNames().Count;
            if (ScheduledTaskExists(GetAutoRunNameWindows()))
            {
                count++;
            }
            if (config.GuiItem.AutoRun ? count > 1 : count > 0)
            {
                await UpdateTask(config);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task ClearTaskWindows(bool autoRun)
    {
        ClearAutoRunRegistryValues();

        var autoRunName = GetAutoRunNameWindows();
        if (!autoRun && ScheduledTaskExists(autoRunName))
        {
            AutoStartTaskService(autoRunName, "", "");
        }

        await Task.CompletedTask;
    }

    [SupportedOSPlatform("windows")]
    private static List<string> GetCurrentAutoRunRegistryValueNames()
    {
        var lst = new List<string>();
        try
        {
            var currentName = GetAutoRunNameWindows();
            var exePath = Utils.GetExePath();
            using var regKey = Registry.CurrentUser.OpenSubKey(Global.AutoRunRegPath, false);
            if (regKey == null)
            {
                return lst;
            }

            foreach (var valueName in regKey.GetValueNames())
            {
                if (!valueName.StartsWith($"{Global.AutoRunName}_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = regKey.GetValue(valueName) as string;
                if (valueName.Equals(currentName, StringComparison.OrdinalIgnoreCase)
                    || IsSameExe(value, exePath))
                {
                    lst.Add(valueName);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return lst;
    }

    private static bool IsSameExe(string? commandLine, string exePath)
    {
        if (commandLine.IsNullOrEmpty())
        {
            return false;
        }
        return commandLine.Trim().Trim('"').Equals(exePath, StringComparison.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static void ClearAutoRunRegistryValues()
    {
        try
        {
            var valueNames = GetCurrentAutoRunRegistryValueNames();
            using var regKey = Registry.CurrentUser.OpenSubKey(Global.AutoRunRegPath, true);
            if (regKey == null)
            {
                return;
            }

            foreach (var valueName in valueNames)
            {
                regKey.DeleteValue(valueName, false);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task SetTaskWindows()
    {
        try
        {
            var autoRunName = GetAutoRunNameWindows();
            var exePath = Utils.GetExePath();
            if (Utils.IsAdministrator())
            {
                AutoStartTaskService(autoRunName, exePath, "");
            }
            else if (!ScheduledTaskExists(autoRunName))
            {
                // If an elevated task already exists, adding a Run entry would start the app twice at logon.
                WindowsUtils.RegWriteValue(Global.AutoRunRegPath, autoRunName, exePath.AppendQuotes());
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Auto Start via TaskService
    /// </summary>
    /// <param name="taskName"></param>
    /// <param name="fileName"></param>
    /// <param name="description"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [SupportedOSPlatform("windows")]
    public static void AutoStartTaskService(string taskName, string fileName, string description)
    {
#if false
        if (taskName.IsNullOrEmpty())
        {
            return;
        }

        var logonUser = WindowsIdentity.GetCurrent().Name;
        using var taskService = new Microsoft.Win32.TaskScheduler.TaskService();
        var tasks = taskService.RootFolder.GetTasks(new Regex(taskName));
        if (fileName.IsNullOrEmpty())
        {
            foreach (var t in tasks)
            {
                taskService.RootFolder.DeleteTask(t.Name);
            }
            return;
        }

        var task = taskService.NewTask();
        task.RegistrationInfo.Description = description;
        task.Settings.DisallowStartIfOnBatteries = false;
        task.Settings.StopIfGoingOnBatteries = false;
        task.Settings.RunOnlyIfIdle = false;
        task.Settings.IdleSettings.StopOnIdleEnd = false;
        task.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        task.Settings.Priority = ProcessPriorityClass.Normal;
        task.Triggers.Add(new Microsoft.Win32.TaskScheduler.LogonTrigger { UserId = logonUser });
        task.Principal.RunLevel = Microsoft.Win32.TaskScheduler.TaskRunLevel.Highest;
        task.Actions.Add(new Microsoft.Win32.TaskScheduler.ExecAction(fileName.AppendQuotes(), null, Path.GetDirectoryName(fileName)));

        taskService.RootFolder.RegisterTaskDefinition(taskName, task);
#endif
        if (string.IsNullOrEmpty(taskName))
        {
            return;
        }

        if (string.IsNullOrEmpty(fileName))
        {
            RunSchtasks("/Delete", "/TN", taskName, "/F");
            return;
        }

        var workingDir = Path.GetDirectoryName(fileName) ?? string.Empty;

        var taskXml = EmbedUtils.GetEmbedText(Global.TaskXmlTemplateFileName);

        taskXml = taskXml
            .Replace("{{DESCRIPTION}}", System.Security.SecurityElement.Escape(description ?? ""))
            .Replace("{{EXEC_PATH}}", System.Security.SecurityElement.Escape(fileName ?? ""))
            .Replace("{{ARGUMENTS}}", string.Empty)
            .Replace("{{WORKING_DIRECTORY}}", System.Security.SecurityElement.Escape(workingDir ?? ""));

        var taskXmlFileName = Utils.GetTempPath($"{Utils.GetGuid(false)}.xml");

        File.WriteAllText(taskXmlFileName, taskXml);

        RunSchtasks(
            "/Create",
            "/TN", taskName,
            "/XML", taskXmlFileName,
            "/F"
        );
    }

    private static bool RunSchtasks(params string[] arguments)
    {
        return RunSchtasks(true, arguments);
    }

    private static bool RunSchtasks(bool logError, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);

        if (process == null)
        {
            return false;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        if (logError)
        {
            if (!string.IsNullOrEmpty(output))
            {
                Logging.SaveLog($"RunSchtasks output: {output}");
            }
            if (!string.IsNullOrEmpty(error))
            {
                Logging.SaveLog($"RunSchtasks error: {error}");
            }
        }

        return process.ExitCode == 0;
    }

    [SupportedOSPlatform("windows")]
    private static bool ScheduledTaskExists(string taskName)
    {
        return RunSchtasks(false, "/Query", "/TN", taskName, "/FO", "CSV", "/NH");
    }

    private static string GetAutoRunNameWindows()
    {
        return $"{Global.AutoRunName}_{Utils.GetMd5(Utils.StartupPath())}";
    }

    #endregion Windows

    #region Linux

    [SupportedOSPlatform("linux")]
    private static async Task ClearTaskLinux()
    {
        try
        {
            File.Delete(GetHomePathLinux());
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        await Task.CompletedTask;
    }

    [SupportedOSPlatform("linux")]
    private static async Task SetTaskLinux()
    {
        try
        {
            var linuxConfig = EmbedUtils.GetEmbedText(Global.LinuxAutostartConfig);
            if (linuxConfig.IsNotEmpty())
            {
                linuxConfig = linuxConfig.Replace("$ExecPath$", Utils.GetExePath());
                Logging.SaveLog(linuxConfig);

                var homePath = GetHomePathLinux();
                await File.WriteAllTextAsync(homePath, linuxConfig);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    [SupportedOSPlatform("linux")]
    private static string GetHomePathLinux()
    {
        var homePath = Path.Combine(Utils.GetHomePath(), ".config", "autostart", $"{Global.AppName}.desktop");
        Directory.CreateDirectory(Path.GetDirectoryName(homePath));
        return homePath;
    }

    #endregion Linux

    #region macOS

    [SupportedOSPlatform("macos")]
    private static async Task ClearTaskOSX()
    {
        try
        {
            // Do not "launchctl unload" the agent: launchd then terminates the job's processes, which is
            // v2rayN itself when it was started at login. The agent only runs at load, so removing the
            // plist is enough to stop the next start.
            File.Delete(GetLaunchAgentPathMacOS());
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        await Task.CompletedTask;
    }

    [SupportedOSPlatform("macos")]
    private static async Task SetTaskOSX()
    {
        try
        {
            var plistContent = GenerateLaunchAgentPlist();
            var launchAgentPath = GetLaunchAgentPathMacOS();
            await File.WriteAllTextAsync(launchAgentPath, plistContent);

            var args = new[] { "-c", $"launchctl load -w \"{launchAgentPath}\"" };
            await Utils.GetCliWrapOutput(Global.LinuxBash, args);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    [SupportedOSPlatform("macos")]
    private static string GetLaunchAgentPathMacOS()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var launchAgentPath = Path.Combine(homePath, "Library", "LaunchAgents", $"{Global.AppName}-LaunchAgent.plist");
        Directory.CreateDirectory(Path.GetDirectoryName(launchAgentPath));
        return launchAgentPath;
    }

    [SupportedOSPlatform("macos")]
    private static string GenerateLaunchAgentPlist()
    {
        var exePath = Utils.GetExePath();
        var appName = Path.GetFileNameWithoutExtension(exePath);
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>{Global.AppName}-LaunchAgent</string>
    <key>ProgramArguments</key>
    <array>
        <string>/bin/sh</string>
        <string>-c</string>
        <string>if ! pgrep -x ""{appName}"" > /dev/null; then ""{exePath}""; fi</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <false/>
</dict>
</plist>";
    }

    #endregion macOS
}
