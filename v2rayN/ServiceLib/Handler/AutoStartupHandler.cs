using System.Security.Principal;
using System.Text.RegularExpressions;

namespace ServiceLib.Handler
{
    public static class AutoStartupHandler
    {
        public static async Task<bool> UpdateTask(Config config)
        {
            if (Utils.IsWindows())
            {
                await ClearTaskWindows();

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

            return true;
        }

        #region Windows

        private static async Task ClearTaskWindows()
        {
            var autoRunName = GetAutoRunNameWindows();
            WindowsUtils.RegWriteValue(Global.AutoRunRegPath, autoRunName, "");
            if (Utils.IsAdministrator())
            {
                AutoStartTaskService(autoRunName, "", "");
            }
        }

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
                else
                {
                    WindowsUtils.RegWriteValue(Global.AutoRunRegPath, autoRunName, exePath.AppendQuotes());
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        /// <summary>
        /// Auto Start via TaskService
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="fileName"></param>
        /// <param name="description"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AutoStartTaskService(string taskName, string fileName, string description)
        {
            if (Utils.IsNullOrEmpty(taskName))
            {
                return;
            }

            var logonUser = WindowsIdentity.GetCurrent().Name;
            using var taskService = new Microsoft.Win32.TaskScheduler.TaskService();
            var tasks = taskService.RootFolder.GetTasks(new Regex(taskName));
            if (Utils.IsNullOrEmpty(fileName))
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
            task.Triggers.Add(new Microsoft.Win32.TaskScheduler.LogonTrigger { UserId = logonUser, Delay = TimeSpan.FromSeconds(10) });
            task.Principal.RunLevel = Microsoft.Win32.TaskScheduler.TaskRunLevel.Highest;
            task.Actions.Add(new Microsoft.Win32.TaskScheduler.ExecAction(fileName.AppendQuotes(), null, Path.GetDirectoryName(fileName)));

            taskService.RootFolder.RegisterTaskDefinition(taskName, task);
        }

        private static string GetAutoRunNameWindows()
        {
            return $"{Global.AutoRunName}_{Utils.GetMd5(Utils.StartupPath())}";
        }

        #endregion Windows

        #region Linux

        private static async Task ClearTaskLinux()
        {
            File.Delete(GetHomePathLinux());
        }

        private static async Task SetTaskLinux()
        {
            try
            {
                var linuxConfig = Utils.GetEmbedText(Global.LinuxAutostartConfig);
                if (linuxConfig.IsNotEmpty())
                {
                    linuxConfig = linuxConfig.Replace("$ExecPath$", Utils.GetExePath());
                    Logging.SaveLog(linuxConfig);

                    var homePath = GetHomePathLinux();
                    Directory.CreateDirectory(Path.GetDirectoryName(homePath));

                    await File.WriteAllTextAsync(homePath, linuxConfig);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private static string GetHomePathLinux()
        {
            return Path.Combine(Utils.GetHomePath(), ".config", "autostart", $"{Global.AppName}.desktop");
        }

        #endregion Linux
    }
}