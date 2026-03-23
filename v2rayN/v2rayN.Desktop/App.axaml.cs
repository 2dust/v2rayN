using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
    private readonly Dictionary<NativeMenuItem, string> _routingMenuMap = [];

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
                InitRoutingMenu();
            }

            desktop.Exit += OnExit;
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
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

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        foreach (var menuItem in _routingMenuMap.Keys)
        {
            menuItem.Click -= MenuRoutingClick;
        }
        _routingMenuMap.Clear();
    }

    private void InitRoutingMenu()
    {
        var vm = StatusBarViewModel.Instance;
        vm.RoutingItems.CollectionChanged += (_, _) => RefreshRoutingMenuItems();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarViewModel.SelectedRouting))
            {
                RefreshRoutingMenuCheckState();
            }
        };

        RefreshRoutingMenuItems();
    }

    private void RefreshRoutingMenuItems()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var routingMenu = GetRoutingRootMenu();
            if (routingMenu == null)
            {
                return;
            }

            routingMenu.Menu ??= new NativeMenu();
            foreach (var menuItem in _routingMenuMap.Keys)
            {
                menuItem.Click -= MenuRoutingClick;
            }
            _routingMenuMap.Clear();
            routingMenu.Menu.Items.Clear();

            foreach (var routing in StatusBarViewModel.Instance.RoutingItems)
            {
                var menuItem = new NativeMenuItem
                {
                    Header = routing.Remarks,
                    IsChecked = routing.IsActive,
                    ToggleType = NativeMenuItemToggleType.Radio,
                };
                menuItem.Click += MenuRoutingClick;
                _routingMenuMap[menuItem] = routing.Id;
                routingMenu.Menu.Items.Add(menuItem);
            }

            RefreshRoutingMenuCheckState();
        }, DispatcherPriority.Background);
    }

    private void RefreshRoutingMenuCheckState()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var selectedRoutingId = StatusBarViewModel.Instance.SelectedRouting?.Id;
            foreach (var pair in _routingMenuMap)
            {
                pair.Key.IsChecked = pair.Value == selectedRoutingId;
            }
        }, DispatcherPriority.Background);
    }

    private NativeMenuItem? GetRoutingRootMenu()
    {
        if (Current == null)
        {
            return null;
        }

        var icons = TrayIcon.GetIcons(Current);
        if (icons.Count == 0 || icons[0].Menu is not NativeMenu trayMenu)
        {
            return null;
        }

        return trayMenu.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(x => Equals(x.Header, ResUI.menuRouting));
    }

    private void MenuRoutingClick(object? sender, EventArgs e)
    {
        if (sender is not NativeMenuItem menuItem)
        {
            return;
        }

        if (!_routingMenuMap.TryGetValue(menuItem, out var routingId))
        {
            return;
        }

        var target = StatusBarViewModel.Instance.RoutingItems.FirstOrDefault(x => x.Id == routingId);
        if (target == null)
        {
            return;
        }

        if (StatusBarViewModel.Instance.SelectedRouting?.Id == target.Id)
        {
            RefreshRoutingMenuCheckState();
            return;
        }

        StatusBarViewModel.Instance.SelectedRouting = target;
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
