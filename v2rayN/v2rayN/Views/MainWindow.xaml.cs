using MaterialDesignThemes.Wpf;
using ReactiveUI;
using Splat;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using v2rayN.Handler;

namespace v2rayN.Views
{
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
            menuExit.Click += menuExit_Click;
            menuCheckUpdate.Click += MenuCheckUpdate_Click;
            menuBackupAndRestore.Click += MenuBackupAndRestore_Click;

            var IsAdministrator = Utils.IsAdministrator();
            MessageBus.Current.Listen<string>(EMsgCommand.SendSnackMsg.ToString()).Subscribe(x => DelegateSnackMsg(x));
            ViewModel = new MainWindowViewModel(IsAdministrator, UpdateViewHandler);
            Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(MainWindowViewModel));

            WindowsHandler.Instance.RegisterGlobalHotkey(_config, OnHotkeyHandler, null);
            if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Horizontal)
            {
                tabProfiles.Content ??= new ProfilesView();
                tabMsgView.Content ??= new MsgView();
                tabClashProxies.Content ??= new ClashProxiesView();
                tabClashConnections.Content ??= new ClashConnectionsView();
            }
            else if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Vertical)
            {
                tabProfiles1.Content ??= new ProfilesView();
                tabMsgView1.Content ??= new MsgView();
                tabClashProxies1.Content ??= new ClashProxiesView();
                tabClashConnections1.Content ??= new ClashConnectionsView();
            }
            else
            {
                tabProfiles2.Content ??= new ProfilesView();
                tabMsgView2.Content ??= new MsgView();
                tabClashProxies2.Content ??= new ClashProxiesView();
                tabClashConnections2.Content ??= new ClashConnectionsView();
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
                this.BindCommand(ViewModel, vm => vm.AddCustomServerCmd, v => v.menuAddCustomServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan).DisposeWith(disposables);

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

                this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);

                //system proxy
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxyClear, v => v.menuSystemProxyClear2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxySet, v => v.menuSystemProxySet2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxyNothing, v => v.menuSystemProxyNothing2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxyPac, v => v.menuSystemProxyPac2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxyClearCmd, v => v.menuSystemProxyClear).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxySetCmd, v => v.menuSystemProxySet).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxyPacCmd, v => v.menuSystemProxyPac).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxyNothingCmd, v => v.menuSystemProxyNothing).DisposeWith(disposables);

                //routings and servers
                this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.cmbRoutings.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings.SelectedItem).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.menuRoutings.Visibility).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.sepRoutings.Visibility).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.Servers, v => v.cmbServers.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedServer, v => v.cmbServers.SelectedItem).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlServers, v => v.cmbServers.Visibility).DisposeWith(disposables);

                //tray menu
                this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy2).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.RunningServerToolTipText, v => v.tbNotify.ToolTipText).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.NotifyLeftClickCmd, v => v.tbNotify.LeftClickCommand).DisposeWith(disposables);

                //status bar
                this.OneWayBind(ViewModel, vm => vm.InboundDisplay, v => v.txtInboundDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.InboundLanDisplay, v => v.txtInboundLanDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningServerDisplay, v => v.txtRunningServerDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningInfoDisplay, v => v.txtRunningInfoDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.SpeedProxyDisplay, v => v.txtSpeedProxyDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.SpeedDirectDisplay, v => v.txtSpeedDirectDisplay.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableTun, v => v.togEnableTun.IsChecked).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SystemProxySelected, v => v.cmbSystemProxy.SelectedIndex).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.cmbRoutings2.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings2.SelectedItem).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.cmbRoutings2.Visibility).DisposeWith(disposables);

                if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Horizontal)
                {
                    gridMain.Visibility = Visibility.Visible;
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain.SelectedIndex).DisposeWith(disposables);
                }
                else if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Vertical)
                {
                    gridMain1.Visibility = Visibility.Visible;
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies1.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections1.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain1.SelectedIndex).DisposeWith(disposables);
                }
                else
                {
                    gridMain2.Visibility = Visibility.Visible;
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies2.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections2.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain2.SelectedIndex).DisposeWith(disposables);
                }
            });

            this.Title = $"{Utils.GetVersion()} - {(IsAdministrator ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";

            if (!_config.guiItem.enableHWA)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            RestoreUI();
            AddHelpMenuItem();
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
                    if (obj is null) return false;
                    return (new AddServerWindow((ProfileItem)obj)).ShowDialog() ?? false;

                case EViewAction.AddServer2Window:
                    if (obj is null) return false;
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
                    if (obj is null) return false;
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.SetStatisticsResult((ServerSpeedItem)obj);
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.DispatcherServerAvailability:
                    if (obj is null) return false;
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.TestServerAvailabilityResult((string)obj);
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.DispatcherReload:
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.ReloadResult();
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.DispatcherRefreshServersBiz:
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.RefreshServersBiz();
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.DispatcherRefreshIcon:
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        tbNotify.Icon = WindowsHandler.Instance.GetNotifyIcon(_config);
                        this.Icon = WindowsHandler.Instance.GetAppIcon(_config);
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.Shutdown:
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        Application.Current.Shutdown();
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.ScanScreenTask:
                    ScanScreenTaskAsync().ContinueWith(_ => { });
                    break;

                case EViewAction.UpdateSysProxy:
                    if (obj is null) return false;
                    SysProxyHandler.UpdateSysProxy(_config, (bool)obj);
                    break;

                case EViewAction.AddServerViaClipboard:
                    var clipboardData = WindowsUtils.GetClipboardData();
                    ViewModel?.AddServerViaClipboardAsync(clipboardData);
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
                    ViewModel?.SetListenerType(ESysProxyType.ForcedClear);
                    break;

                case EGlobalHotkey.SystemProxySet:
                    ViewModel?.SetListenerType(ESysProxyType.ForcedChange);
                    break;

                case EGlobalHotkey.SystemProxyUnchanged:
                    ViewModel?.SetListenerType(ESysProxyType.Unchanged);
                    break;

                case EGlobalHotkey.SystemProxyPac:
                    ViewModel?.SetListenerType(ESysProxyType.Pac);
                    break;
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            ShowHideWindow(false);
        }

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            tabProfiles = null;

            tbNotify.Dispose();
            StorageUI();
            ViewModel?.MyAppExitAsync(false);
        }

        private void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Logging.SaveLog("Current_SessionEnding");
            StorageUI();
            ViewModel?.MyAppExitAsync(true);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                switch (e.Key)
                {
                    case Key.V:
                        var clipboardData = WindowsUtils.GetClipboardData();
                        ViewModel?.AddServerViaClipboardAsync(clipboardData);
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
            Utils.ProcessStart($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
        }

        private void txtRunningInfoDisplay_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.TestServerAvailability();
        }

        private void menuSettingsSetUWP_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart(Utils.GetBinPath("EnableLoopback.exe"));
        }

        public async Task ScanScreenTaskAsync()
        {
            ShowHideWindow(false);

            var dpiXY = QRCodeHelper.GetDpiXY(Application.Current.MainWindow);
            string result = await Task.Run(() =>
            {
                return QRCodeHelper.ScanScreen(dpiXY.Item1, dpiXY.Item2);
            });

            ShowHideWindow(true);

            ViewModel?.ScanScreenTaskAsync(result);
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
            var bl = blShow ?? !_config.uiItem.showInTaskbar;
            if (bl)
            {
                Application.Current.MainWindow.Show();
                if (Application.Current.MainWindow.WindowState == WindowState.Minimized)
                {
                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                }
                Application.Current.MainWindow.Activate();
                Application.Current.MainWindow.Focus();
            }
            else
            {
                Application.Current.MainWindow.Hide();
            }
            _config.uiItem.showInTaskbar = bl;
        }

        private void RestoreUI()
        {
            if (_config.uiItem.mainWidth > 0 && _config.uiItem.mainHeight > 0)
            {
                Width = _config.uiItem.mainWidth;
                Height = _config.uiItem.mainHeight;
            }

            var maxWidth = SystemParameters.WorkArea.Width;
            var maxHeight = SystemParameters.WorkArea.Height;
            if (Width > maxWidth) Width = maxWidth;
            if (Height > maxHeight) Height = maxHeight;
            if (_config.uiItem.mainGirdHeight1 > 0 && _config.uiItem.mainGirdHeight2 > 0)
            {
                if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Horizontal)
                {
                    gridMain.ColumnDefinitions[0].Width = new GridLength(_config.uiItem.mainGirdHeight1, GridUnitType.Star);
                    gridMain.ColumnDefinitions[2].Width = new GridLength(_config.uiItem.mainGirdHeight2, GridUnitType.Star);
                }
                else if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Vertical)
                {
                    gridMain1.RowDefinitions[0].Height = new GridLength(_config.uiItem.mainGirdHeight1, GridUnitType.Star);
                    gridMain1.RowDefinitions[2].Height = new GridLength(_config.uiItem.mainGirdHeight2, GridUnitType.Star);
                }
            }
        }

        private void StorageUI()
        {
            _config.uiItem.mainWidth = Utils.ToInt(this.Width);
            _config.uiItem.mainHeight = Utils.ToInt(this.Height);

            if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Horizontal)
            {
                _config.uiItem.mainGirdHeight1 = Math.Ceiling(gridMain.ColumnDefinitions[0].ActualWidth + 0.1);
                _config.uiItem.mainGirdHeight2 = Math.Ceiling(gridMain.ColumnDefinitions[2].ActualWidth + 0.1);
            }
            else if (_config.uiItem.mainGirdOrientation == EGirdOrientation.Vertical)
            {
                _config.uiItem.mainGirdHeight1 = Math.Ceiling(gridMain1.RowDefinitions[0].ActualHeight + 0.1);
                _config.uiItem.mainGirdHeight2 = Math.Ceiling(gridMain1.RowDefinitions[2].ActualHeight + 0.1);
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
                Utils.ProcessStart(item.Tag.ToString());
            }
        }

        #endregion UI
    }
}