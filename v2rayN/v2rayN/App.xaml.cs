﻿using System.Windows;
using System.Windows.Threading;
using v2rayN.Handler;
using v2rayN.Model;

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
            var exePathKey = Utile.GetMD5(Utile.GetExePath());

            var rebootAs = (e.Args ?? new string[] { }).Any(t => t == Global.RebootAs);
            ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out bool bCreatedNew);
            if (!rebootAs && !bCreatedNew)
            {
                ProgramStarted.Set();
                Current.Shutdown();
                Environment.Exit(0);
                return;
            }

            Logging.Setup();
            Init();
            Logging.LoggingEnabled(_config.guiItem.enableLog);
            Logging.SaveLog($"v2rayN start up | {Utile.GetVersion()} | {Utile.GetExePath()}");
            Logging.ClearLogs();

            Thread.CurrentThread.CurrentUICulture = new(_config.uiItem.currentLanguage);

            base.OnStartup(e);
        }

        private void Init()
        {
            if (ConfigHandler.LoadConfig(ref _config) != 0)
            {
                UI.ShowWarning($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
                Application.Current.Shutdown();
                Environment.Exit(0);
                return;
            }
            //if (RuntimeInformation.ProcessArchitecture != Architecture.X86 && RuntimeInformation.ProcessArchitecture != Architecture.X64)
            //{
            //    _config.guiItem.enableStatistics = false;
            //}
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
    }
}