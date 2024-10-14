﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace v2rayN
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static EventWaitHandle ProgramStarted;
        private static Config _config;

        public App()
        {
            // Locator.CurrentMutable.RegisterViewsForViewModels(Assembly.GetCallingAssembly());
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        /// <summary>
        /// 只打开一个进程
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            var exePathKey = Utils.GetMd5(Utils.GetExePath());

            var rebootas = (e.Args ?? new string[] { }).Any(t => t == Global.RebootAs);
            ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out bool bCreatedNew);
            if (!rebootas && !bCreatedNew)
            {
                ProgramStarted.Set();
                Environment.Exit(0);
                return;
            }

            if (!AppHandler.Instance.InitApp())
            {
                UI.Show($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
                Environment.Exit(0);
                return;
            }

            AppHandler.Instance.InitComponents();
            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logging.SaveLog("App_DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject != null)
            {
                Logging.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject!);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Logging.SaveLog("TaskScheduler_UnobservedTaskException", e.Exception);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logging.SaveLog("OnExit");
            base.OnExit(e);
            Process.GetCurrentProcess().Kill();
        }
    }
}