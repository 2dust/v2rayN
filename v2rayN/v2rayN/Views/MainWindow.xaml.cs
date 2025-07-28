using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using ReactiveUI;
using Splat;
using v2rayN.Handler;

namespace v2rayN.Views;

public partial class MainWindow
{
    private static Config _config;
    private CheckUpdateView? _checkUpdateView;
    private BackupAndRestoreView? _backupAndRestoreView;

    public MainWindow()
    {
        InitializeComponent();

        _config = AppHandler.Instance.Config;
        ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted, OnProgramStarted, null, -1, false);

        App.Current.SessionEnding += Current_SessionEnding;
        this.Closing += MainWindow_Closing;
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        menuSettingsSetUWP.Click += menuSettingsSetUWP_Click;
        menuPromotion.Click += menuPromotion_Click;
        menuClose.Click += menuClose_Click;
        menuCheckUpdate.Click += MenuCheckUpdate_Click;
        menuBackupAndRestore.Click += MenuBackupAndRestore_Click;

        MessageBus.Current.Listen<string>(EMsgCommand.SendSnackMsg.ToString()).Subscribe(DelegateSnackMsg);
        ViewModel = new MainWindowViewModel(UpdateViewHandler);
        Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(MainWindowViewModel));

        switch (_config.UiItem.MainGirdOrientation)
        {
            case EGirdOrientation.Horizontal:
                tabProfiles.Content ??= new ProfilesView();
                tabMsgView.Content ??= new MsgView();
                tabClashProxies.Content ??= new ClashProxiesView();
                tabClashConnections.Content ??= new ClashConnectionsView();
                gridMain.Visibility = Visibility.Visible;
                break;

            case EGirdOrientation.Vertical:
                tabProfiles1.Content ??= new ProfilesView();
                tabMsgView1.Content ??= new MsgView();
                tabClashProxies1.Content ??= new ClashProxiesView();
                tabClashConnections1.Content ??= new ClashConnectionsView();
                gridMain1.Visibility = Visibility.Visible;
                break;

            case EGirdOrientation.Tab:
            default:
                tabProfiles2.Content ??= new ProfilesView();
                tabMsgView2.Content ??= new MsgView();
                tabClashProxies2.Content ??= new ClashProxiesView();
                tabClashConnections2.Content ??= new ClashConnectionsView();
                gridMain2.Visibility = Visibility.Visible;
                break;
        }
        pbTheme.Content ??= new ThemeSettingView();

        this.WhenActivated(disposables =>
        {
            //servers
            this.BindCommand(ViewModel, vm => vm.AddVmessServerCmd, v => v.menuAddVmessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddVlessServerCmd, v => v.menuAddVlessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddShadowsocksServerCmd, v => v.menuAddShadowsocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddSocksServerCmd, v => v.menuAddSocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHttpServerCmd, v => v.menuAddHttpServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTrojanServerCmd, v => v.menuAddTrojanServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHysteria2ServerCmd, v => v.menuAddHysteria2Server).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTuicServerCmd, v => v.menuAddTuicServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddWireguardServerCmd, v => v.menuAddWireguardServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddAnytlsServerCmd, v => v.menuAddAnytlsServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddBrookServerCmd, v => v.menuAddBrookServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddJuicityServerCmd, v => v.menuAddJuicityServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddNaiveServerCmd, v => v.menuAddNaiveServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddShadowquicServerCmd, v => v.menuAddShadowquicServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddCustomServerCmd, v => v.menuAddCustomServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaImageCmd, v => v.menuAddServerViaImage).DisposeWith(disposables);

            //sub
            this.BindCommand(ViewModel, vm => vm.SubSettingCmd, v => v.menuSubSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateCmd, v => v.menuSubGroupUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateViaProxyCmd, v => v.menuSubGroupUpdateViaProxy).DisposeWith(disposables);

            //setting
            this.BindCommand(ViewModel, vm => vm.OptionSettingCmd, v => v.menuOptionSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingSettingCmd, v => v.menuRoutingSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DNSSettingCmd, v => v.menuDNSSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.GlobalHotkeySettingCmd, v => v.menuGlobalHotkeySetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RebootAsAdminCmd, v => v.menuRebootAsAdmin).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ClearServerStatisticsCmd, v => v.menuClearServerStatistics).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenTheFileLocationCmd, v => v.menuOpenTheFileLocation).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetDefaultCmd, v => v.menuRegionalPresetsDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetRussiaCmd, v => v.menuRegionalPresetsRussia).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetIranCmd, v => v.menuRegionalPresetsIran).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);

            switch (_config.UiItem.MainGirdOrientation)
            {
                case EGirdOrientation.Horizontal:
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain.SelectedIndex).DisposeWith(disposables);
                    break;

                case EGirdOrientation.Vertical:
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView1.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies1.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections1.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain1.SelectedIndex).DisposeWith(disposables);
                    break;

                case EGirdOrientation.Tab:
                default:
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies2.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections2.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain2.SelectedIndex).DisposeWith(disposables);
                    break;
            }
        });

        this.Title = $"{Utils.GetVersion()} - {(Utils.IsAdministrator() ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";

        if (!_config.GuiItem.EnableHWA)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        AddHelpMenuItem();
        WindowsHandler.Instance.RegisterGlobalHotkey(_config, OnHotkeyHandler, null);
        MessageBus.Current.Listen<string>(EMsgCommand.AppExit.ToString()).Subscribe(StorageUI);
    }

    #region Event

    private void OnProgramStarted(object state, bool timeout)
    {
        Application.Current?.Dispatcher.Invoke((Action)(() =>
        {
            ShowHideWindow(true);
        }));
    }

    private void DelegateSnackMsg(string content)
    {
        Application.Current?.Dispatcher.Invoke((() =>
        {
            MainSnackbar.MessageQueue?.Enqueue(content);
        }), DispatcherPriority.Normal);
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.AddServerWindow:
                if (obj is null)
                    return false;
                return (new AddServerWindow((ProfileItem)obj)).ShowDialog() ?? false;

            case EViewAction.AddServer2Window:
                if (obj is null)
                    return false;
                return (new AddServer2Window((ProfileItem)obj)).ShowDialog() ?? false;

            case EViewAction.DNSSettingWindow:
                return (new DNSSettingWindow().ShowDialog() ?? false);

            case EViewAction.RoutingSettingWindow:
                return (new RoutingSettingWindow().ShowDialog() ?? false);

            case EViewAction.OptionSettingWindow:
                return (new OptionSettingWindow().ShowDialog() ?? false);

            case EViewAction.GlobalHotkeySettingWindow:
                return (new GlobalHotkeySettingWindow().ShowDialog() ?? false);

            case EViewAction.SubSettingWindow:
                return (new SubSettingWindow().ShowDialog() ?? false);

            case EViewAction.ShowHideWindow:
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    ShowHideWindow((bool?)obj);
                }), DispatcherPriority.Normal);
                break;

            case EViewAction.DispatcherStatistics:
                if (obj is null)
                    return false;
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    ViewModel?.SetStatisticsResult((ServerSpeedItem)obj);
                }), DispatcherPriority.Normal);
                break;

            case EViewAction.DispatcherReload:
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    ViewModel?.ReloadResult();
                }), DispatcherPriority.Normal);
                break;

            case EViewAction.Shutdown:
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    Application.Current.Shutdown();
                }), DispatcherPriority.Normal);
                break;

            case EViewAction.ScanScreenTask:
                await ScanScreenTaskAsync();
                break;

            case EViewAction.ScanImageTask:
                await ScanImageTaskAsync();
                break;

            case EViewAction.AddServerViaClipboard:
                var clipboardData = WindowsUtils.GetClipboardData();
                if (clipboardData.IsNotEmpty())
                {
                    ViewModel?.AddServerViaClipboardAsync(clipboardData);
                }
                break;

            case EViewAction.AdjustMainLvColWidth:
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    Locator.Current.GetService<ProfilesViewModel>()?.AutofitColumnWidthAsync();
                }), DispatcherPriority.Normal);
                break;
        }

        return await Task.FromResult(true);
    }

    private void OnHotkeyHandler(EGlobalHotkey e)
    {
        switch (e)
        {
            case EGlobalHotkey.ShowForm:
                ShowHideWindow(null);
                break;

            case EGlobalHotkey.SystemProxyClear:
            case EGlobalHotkey.SystemProxySet:
            case EGlobalHotkey.SystemProxyUnchanged:
            case EGlobalHotkey.SystemProxyPac:
                Locator.Current.GetService<StatusBarViewModel>()?.SetListenerType((ESysProxyType)((int)e - 1));
                break;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        ShowHideWindow(false);
    }

    private async void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Logging.SaveLog("Current_SessionEnding");
        StorageUI();
        await ViewModel?.MyAppExitAsync(true);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            switch (e.Key)
            {
                case Key.V:
                    if (Keyboard.FocusedElement is TextBox)
                    {
                        return;
                    }

                    var clipboardData = WindowsUtils.GetClipboardData();
                    if (clipboardData.IsNotEmpty())
                    {
                        var service = Locator.Current.GetService<MainWindowViewModel>();
                        if (service != null)
                        {
                            _ = service.AddServerViaClipboardAsync(clipboardData);
                        }
                    }

                    break;

                case Key.S:
                    ScanScreenTaskAsync().ContinueWith(_ => { });
                    break;
            }
        }
        else
        {
            if (e.Key == Key.F5)
            {
                ViewModel?.Reload();
            }
        }
    }

    private void menuClose_Click(object sender, RoutedEventArgs e)
    {
        StorageUI();
        ShowHideWindow(false);
    }

    private void menuPromotion_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
    }

    private void menuSettingsSetUWP_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart(Utils.GetBinPath("EnableLoopback.exe"));
    }

    private async Task ScanScreenTaskAsync()
    {
        ShowHideWindow(false);

        if (Application.Current?.MainWindow is Window window)
        {
            var bytes = QRCodeHelper.CaptureScreen(window);
            await ViewModel?.ScanScreenResult(bytes);
        }

        ShowHideWindow(true);
    }

    private async Task ScanImageTaskAsync()
    {
        if (UI.OpenFileDialog(out var fileName, "PNG|*.png|All|*.*") != true)
        {
            return;
        }
        if (fileName.IsNullOrEmpty())
        {
            return;
        }
        await ViewModel?.ScanImageResult(fileName);
    }

    private void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        _checkUpdateView ??= new CheckUpdateView();
        DialogHost.Show(_checkUpdateView, "RootDialog");
    }

    private void MenuBackupAndRestore_Click(object sender, RoutedEventArgs e)
    {
        _backupAndRestoreView ??= new BackupAndRestoreView();
        DialogHost.Show(_backupAndRestoreView, "RootDialog");
    }

    #endregion Event

    #region UI

    public void ShowHideWindow(bool? blShow)
    {
        var bl = blShow ?? !_config.UiItem.ShowInTaskbar;
        if (bl)
        {
            this?.Show();
            if (this?.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this?.Activate();
            this?.Focus();
        }
        else
        {
            this?.Hide();
        }
        _config.UiItem.ShowInTaskbar = bl;
    }

    protected override void OnLoaded(object? sender, RoutedEventArgs e)
    {
        base.OnLoaded(sender, e);
        RestoreUI();
    }

    private void RestoreUI()
    {
        if (_config.UiItem.MainGirdHeight1 > 0 && _config.UiItem.MainGirdHeight2 > 0)
        {
            if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
            {
                gridMain.ColumnDefinitions[0].Width = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain.ColumnDefinitions[2].Width = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
            else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
            {
                gridMain1.RowDefinitions[0].Height = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain1.RowDefinitions[2].Height = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
        }
    }

    private void StorageUI(string? n = null)
    {
        ConfigHandler.SaveWindowSizeItem(_config, GetType().Name, Width, Height);

        if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain.ColumnDefinitions[0].ActualWidth, gridMain.ColumnDefinitions[2].ActualWidth);
        }
        else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain1.RowDefinitions[0].ActualHeight, gridMain1.RowDefinitions[2].ActualHeight);
        }
    }

    private void AddHelpMenuItem()
    {
        var coreInfo = CoreInfoHandler.Instance.GetCoreInfo();
        foreach (var it in coreInfo
            .Where(t => t.CoreType != ECoreType.v2fly
                        && t.CoreType != ECoreType.hysteria))
        {
            var item = new MenuItem()
            {
                Tag = it.Url.Replace(@"/releases", ""),
                Header = string.Format(ResUI.menuWebsiteItem, it.CoreType.ToString().Replace("_", " ")).UpperFirstChar()
            };
            item.Click += MenuItem_Click;
            menuHelp.Items.Add(item);
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
        {
            ProcUtils.ProcessStart(item.Tag.ToString());
        }
    }

    #endregion UI
}
