using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Splat;
using v2rayN.Desktop.Common;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
    public static EventWaitHandle ProgramStarted;
    private static Config _config;

    public override void Initialize()
    {
        Init();
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            OnStartup(desktop.Args);

            desktop.Exit += OnExit;
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnStartup(string[]? Args)
    {
        var exePathKey = Utils.GetMD5(Utils.GetExePath());

        var rebootas = (Args ?? new string[] { }).Any(t => t == Global.RebootAs);
        //ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out bool bCreatedNew);
        //if (!rebootas && !bCreatedNew)
        //{
        //    ProgramStarted.Set();
        //    Environment.Exit(0);
        //    return;
        //}

        Logging.Setup();
        Logging.LoggingEnabled(_config.guiItem.enableLog);
        Logging.SaveLog($"v2rayN start up | {Utils.GetVersion()} | {Utils.GetExePath()}");
        Logging.SaveLog($"{Environment.OSVersion} - {(Environment.Is64BitOperatingSystem ? 64 : 32)}");
        Logging.ClearLogs();
    }

    private void Init()
    {
        if (ConfigHandler.LoadConfig(ref _config) != 0)
        {
            //Logging.SaveLog($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
            Environment.Exit(0);
            return;
        }
        LazyConfig.Instance.SetConfig(_config);
        Locator.CurrentMutable.RegisterLazySingleton(() => new NoticeHandler(), typeof(NoticeHandler));
        Thread.CurrentThread.CurrentUICulture = new(_config.uiItem.currentLanguage);
        
        //Under Win10
        if (Utils.IsWindows() && Environment.OSVersion.Version.Major < 10)
        {
            Environment.SetEnvironmentVariable("DOTNET_EnableWriteXorExecute", "0", EnvironmentVariableTarget.User);
        }
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

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow.IsVisible)
            {
                desktop.MainWindow?.Hide();
            }
            else
            {
                desktop.MainWindow?.Show();
            }
        }
    }

    private void MenuAddServerViaClipboardClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboardData = AvaUtils.GetClipboardData(desktop.MainWindow).Result;
            Locator.Current.GetService<MainWindowViewModel>()?.AddServerViaClipboardAsync(clipboardData);
        }
    }

    private void MenuSubUpdate_Click(object? sender, EventArgs e)
    {
        Locator.Current.GetService<MainWindowViewModel>()?.UpdateSubscriptionProcess("", false);
    }

    private void MenuSubUpdateViaProxy_Click(object? sender, EventArgs e)
    {
        Locator.Current.GetService<MainWindowViewModel>()?.UpdateSubscriptionProcess("", true);
    }

    private void MenuExit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Locator.Current.GetService<MainWindowViewModel>()?.MyAppExitAsync(false);

            desktop.Shutdown();
        }
    }
}