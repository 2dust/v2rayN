using v2rayN.Manager;
using v2rayN.Views;

namespace v2rayN;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public static EventWaitHandle ProgramStarted;

    private TunDelayStartupOption _tunDelayOption = TunDelayStartupOption.NotSpecified;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    /// <summary>
    /// Open only one process
    /// </summary>
    /// <param name="e"></param>
    protected override void OnStartup(StartupEventArgs e)
    {
        var exePathKey = Utils.GetMd5(Utils.GetExePath());

        var rebootas = e.Args.Any(t => t == Global.RebootAs);
        ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out var bCreatedNew);
        if (!rebootas && !bCreatedNew)
        {
            ProgramStarted.Set();
            Environment.Exit(0);
            return;
        }

        if (!AppManager.Instance.InitApp())
        {
            UI.Show($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
            Environment.Exit(0);
            return;
        }

        _tunDelayOption = StartupArgumentHelper.ParseTunDelay(e.Args);
        if (_tunDelayOption.IsSpecified)
        {
            // The command-line option always suppresses TUN during the initial startup.
            AppManager.Instance.Config.TunModeItem.EnableTun = false;
        }

        AppManager.Instance.WindowDialog = new WindowDialog();

        AppManager.Instance.InitComponents();
        LogTunDelayStartupOption();

        RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .BuildApp();

        base.OnStartup(e);

        var mainWindowViewModel = new MainWindowViewModel();
        var viewFor = SimpleViewLocator.Instance.ResolveView(mainWindowViewModel);
        viewFor!.ViewModel = mainWindowViewModel;

        var mainWindow = (MainWindow)viewFor;
        mainWindow.Show();
        MainWindow = mainWindow;

        if (_tunDelayOption.IsSpecified && _tunDelayOption.DelaySeconds > 0)
        {
            _ = EnableTunAfterDelayAsync(_tunDelayOption.DelaySeconds);
        }
    }

    private void LogTunDelayStartupOption()
    {
        if (!_tunDelayOption.IsSpecified)
        {
            return;
        }

        if (_tunDelayOption.ParseError.IsNotEmpty())
        {
            Logging.SaveLog($"TUN delay option error: {_tunDelayOption.ParseError} TUN will remain disabled until enabled manually.");
            return;
        }

        if (_tunDelayOption.DelaySeconds == 0)
        {
            Logging.SaveLog("TUN delay option: TUN is disabled for startup and automatic enable is disabled (-tundelay 0).");
        }
        else
        {
            Logging.SaveLog($"TUN delay option: TUN is disabled for startup and will be enabled after {_tunDelayOption.DelaySeconds} seconds.");
        }
    }

    private async Task EnableTunAfterDelayAsync(int delaySeconds)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            await Dispatcher.InvokeAsync(() =>
            {
                if (StatusBarViewModel.Instance.EnableTun)
                {
                    Logging.SaveLog($"TUN delay elapsed after {delaySeconds} seconds; TUN is already enabled.");
                    return;
                }

                Logging.SaveLog($"TUN delay elapsed after {delaySeconds} seconds; enabling TUN.");
                StatusBarViewModel.Instance.EnableTun = true;
            });
        }
        catch (Exception ex)
        {
            Logging.SaveLog("EnableTunAfterDelayAsync", ex);
        }
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
            Logging.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
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
