using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Splat;
using v2rayN.Desktop.Common;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
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

        var ViewModel = new StatusBarViewModel(null);
        Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(StatusBarViewModel));
        DataContext = ViewModel;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppHandler.Instance.InitComponents();

            desktop.Exit += OnExit;
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
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

    private async void MenuAddServerViaClipboardClick(object? sender, EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                var clipboardData = await AvaUtils.GetClipboardData(desktop.MainWindow);
                var service = Locator.Current.GetService<MainWindowViewModel>();
                if (service != null)
                {
                    _ = service.AddServerViaClipboardAsync(clipboardData);
                }
            }
        }
    }

    private async void MenuExit_Click(object? sender, EventArgs e)
    {
        var service = Locator.Current.GetService<MainWindowViewModel>();
        if (service != null)
        {
            await service.MyAppExitAsync(true);
        }
        service?.Shutdown(true);
    }
}
