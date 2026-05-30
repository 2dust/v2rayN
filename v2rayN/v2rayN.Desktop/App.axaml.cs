using System.Runtime.InteropServices;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";
    private const nint ActivationPolicyAccessory = 1;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Design.IsDesignMode)
            {
                AppManager.Instance.InitComponents();
                DataContext = StatusBarViewModel.Instance;
            }

            desktop.Exit += OnExit;
            desktop.MainWindow = new MainWindow();

            if (OperatingSystem.IsMacOS())
            {
                Current?.TryGetFeature<IActivatableLifetime>()?.Activated += (_, args) =>
                {
                    if (args.Kind == ActivationKind.Reopen)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            (desktop.MainWindow as MainWindow)?.ShowHideWindow(true);

                            if (!AppManager.Instance.Config.UiItem.MacOSShowInDock)
                            {
                                SetActivationPolicyAccessory();
                            }
                        });
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetActivationPolicyAccessory()
        => objc_msgSend(
            objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication")),
            sel_registerName("setActivationPolicy:"),
            ActivationPolicyAccessory);

    [DllImport(LibObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(LibObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(nint receiver, nint selector, nint argument);

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

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
    }

    private async void MenuAddServerViaClipboardClick(object? sender, EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                AppEvents.AddServerViaClipboardRequested.Publish();
                await Task.Delay(1000);
            }
        }
    }

    private async void MenuExit_Click(object? sender, EventArgs e)
    {
        await AppManager.Instance.AppExitAsync(false);
        AppManager.Instance.Shutdown(true);
    }
}
