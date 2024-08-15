using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace v2rayN
{
    internal static class WindowsUtils
    {
        /// <summary>
        /// 获取剪贴板数
        /// </summary>
        /// <returns></returns>
        public static string? GetClipboardData()
        {
            string? strData = string.Empty;
            try
            {
                IDataObject data = Clipboard.GetDataObject();
                if (data.GetDataPresent(DataFormats.UnicodeText))
                {
                    strData = data.GetData(DataFormats.UnicodeText)?.ToString();
                }
                return strData;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return strData;
        }

        /// <summary>
        /// 拷贝至剪贴板
        /// </summary>
        /// <returns></returns>
        public static void SetClipboardData(string strData)
        {
            try
            {
                Clipboard.SetText(strData);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Auto Start via TaskService
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="fileName"></param>
        /// <param name="description"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AutoStart(string taskName, string fileName, string description)
        {
            if (Utils.IsNullOrEmpty(taskName))
            {
                return;
            }
            string TaskName = taskName;
            var logonUser = WindowsIdentity.GetCurrent().Name;
            string taskDescription = description;
            string deamonFileName = fileName;

            using var taskService = new TaskService();
            var tasks = taskService.RootFolder.GetTasks(new Regex(TaskName));
            foreach (var t in tasks)
            {
                taskService.RootFolder.DeleteTask(t.Name);
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            var task = taskService.NewTask();
            task.RegistrationInfo.Description = taskDescription;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.RunOnlyIfIdle = false;
            task.Settings.IdleSettings.StopOnIdleEnd = false;
            task.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            task.Triggers.Add(new LogonTrigger { UserId = logonUser, Delay = TimeSpan.FromSeconds(10) });
            task.Principal.RunLevel = TaskRunLevel.Highest;
            task.Actions.Add(new ExecAction(deamonFileName.AppendQuotes(), null, Path.GetDirectoryName(deamonFileName)));

            taskService.RootFolder.RegisterTaskDefinition(TaskName, task);
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int attributeValue, uint attributeSize);

        public static ImageSource IconToImageSource(Icon icon)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                new System.Windows.Int32Rect(0, 0, icon.Width, icon.Height),
                BitmapSizeOptions.FromEmptyOptions());
        }

        public static bool IsLightTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }

        public static string? RegReadValue(string path, string name, string def)
        {
            RegistryKey? regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string? value = regKey?.GetValue(name) as string;
                if (Utils.IsNullOrEmpty(value))
                {
                    return def;
                }
                else
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
            return def;
        }

        public static void RegWriteValue(string path, string name, object value)
        {
            RegistryKey? regKey = null;
            try
            {
                regKey = Registry.CurrentUser.CreateSubKey(path);
                if (Utils.IsNullOrEmpty(value.ToString()))
                {
                    regKey?.DeleteValue(name, false);
                }
                else
                {
                    regKey?.SetValue(name, value);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
        }

        public static void RemoveTunDevice()
        {
            try
            {
                var sum = MD5.HashData(Encoding.UTF8.GetBytes("wintunsingbox_tun"));
                var guid = new Guid(sum);
                string pnputilPath = @"C:\Windows\System32\pnputil.exe";
                string arg = $$""" /remove-device  "SWD\Wintun\{{{guid}}}" """;

                // Try to remove the device
                Process proc = new()
                {
                    StartInfo = new()
                    {
                        FileName = pnputilPath,
                        Arguments = arg,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                proc.Start();
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
            }
            catch
            {
            }
        }

        public static void SetDarkBorder(Window window, bool dark)
        {
            // Make sure the handle is created before the window is shown
            IntPtr hWnd = new WindowInteropHelper(window).EnsureHandle();
            int attribute = dark ? 1 : 0;
            uint attributeSize = (uint)Marshal.SizeOf(attribute);
            DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
            DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
        }

        /// <summary>
        /// IsAdministrator
        /// </summary>
        /// <returns></returns>
        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity current = WindowsIdentity.GetCurrent();
                WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
                //WindowsBuiltInRole可以枚举出很多权限，例如系统用户、User、Guest等等
                return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        public static void SetAutoRun(string AutoRunRegPath, string AutoRunName, bool run)
        {
            try
            {
                var autoRunName = $"{AutoRunName}_{Utils.GetMD5(Utils.StartupPath())}";
                //delete first
                RegWriteValue(AutoRunRegPath, autoRunName, "");
                if (IsAdministrator())
                {
                    AutoStart(autoRunName, "", "");
                }

                if (run)
                {
                    string exePath = Utils.GetExePath();
                    if (IsAdministrator())
                    {
                        AutoStart(autoRunName, exePath, "");
                    }
                    else
                    {
                        RegWriteValue(AutoRunRegPath, autoRunName, exePath.AppendQuotes());
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        #region Windows API

        [Flags]
        public enum DWMWINDOWATTRIBUTE : uint
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        }

        #endregion Windows API
    }
}