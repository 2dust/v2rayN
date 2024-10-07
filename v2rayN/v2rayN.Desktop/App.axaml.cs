using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using v2rayN.Desktop.ViewModels;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
    //public static EventWaitHandle ProgramStarted;

    public override void Initialize()
    {
        if (!AppHandler.Instance.InitApp())
        {
            Environment.Exit(0);
            return;
        }
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        this.DataContext = new AppViewModel();
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

        AppHandler.Instance.InitComponents();
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
}