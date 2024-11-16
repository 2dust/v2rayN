﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DialogHostAvalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        private static Config _config;
        private WindowNotificationManager? _manager;
        private CheckUpdateView? _checkUpdateView;
        private BackupAndRestoreView? _backupAndRestoreView;

        public MainWindow()
        {
            InitializeComponent();

            _config = AppHandler.Instance.Config;
            _manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { MaxItems = 3, Position = NotificationPosition.BottomRight };

            this.KeyDown += MainWindow_KeyDown;
            menuSettingsSetUWP.Click += menuSettingsSetUWP_Click;
            menuPromotion.Click += menuPromotion_Click;
            menuCheckUpdate.Click += MenuCheckUpdate_Click;
            menuBackupAndRestore.Click += MenuBackupAndRestore_Click;
            menuClose.Click += MenuClose_Click;

            MessageBus.Current.Listen<string>(EMsgCommand.SendSnackMsg.ToString()).Subscribe(DelegateSnackMsg);
            ViewModel = new MainWindowViewModel(UpdateViewHandler);
            Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(MainWindowViewModel));

            //WindowsHandler.Instance.RegisterGlobalHotkey(_config, OnHotkeyHandler, null);

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

                this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);

                switch (_config.UiItem.MainGirdOrientation)
                {
                    case EGirdOrientation.Horizontal:
                        gridMain.IsVisible = true;
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView.IsVisible).DisposeWith(disposables);
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies.IsVisible).DisposeWith(disposables);
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections.IsVisible).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain.SelectedIndex).DisposeWith(disposables);
                        break;

                    case EGirdOrientation.Vertical:
                        gridMain1.IsVisible = true;
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView1.IsVisible).DisposeWith(disposables);
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies1.IsVisible).DisposeWith(disposables);
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections1.IsVisible).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain1.SelectedIndex).DisposeWith(disposables);
                        break;

                    case EGirdOrientation.Tab:
                    default:
                        gridMain2.IsVisible = true;
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies2.IsVisible).DisposeWith(disposables);
                        this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections2.IsVisible).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain2.SelectedIndex).DisposeWith(disposables);
                        break;
                }
            });

            this.Title = $"{Utils.GetVersion()} - {(AppHandler.Instance.IsAdministrator ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";
            if (Utils.IsWindows())
            {
                ThreadPool.RegisterWaitForSingleObject(Program.ProgramStarted, OnProgramStarted, null, -1, false);

                menuGlobalHotkeySetting.IsVisible = false;
            }
            else
            {
                if (AppHandler.Instance.IsAdministrator)
                {
                    this.Title = $"{Utils.GetVersion()} - {ResUI.TbSettingsLinuxSudoPasswordNotSudoRunApp}";
                    NoticeHandler.Instance.SendMessageAndEnqueue(ResUI.TbSettingsLinuxSudoPasswordNotSudoRunApp);
                }
                menuRebootAsAdmin.IsVisible = false;
                menuSettingsSetUWP.IsVisible = false;
                menuGlobalHotkeySetting.IsVisible = false;
            }
            menuAddServerViaScan.IsVisible = false;

            switch (_config.UiItem.MainGirdOrientation)
            {
                case EGirdOrientation.Horizontal:
                    tabProfiles.Content ??= new ProfilesView(this);
                    tabMsgView.Content ??= new MsgView();
                    tabClashProxies.Content ??= new ClashProxiesView();
                    tabClashConnections.Content ??= new ClashConnectionsView();
                    break;

                case EGirdOrientation.Vertical:
                    tabProfiles1.Content ??= new ProfilesView(this);
                    tabMsgView1.Content ??= new MsgView();
                    tabClashProxies1.Content ??= new ClashProxiesView();
                    tabClashConnections1.Content ??= new ClashConnectionsView();
                    break;

                case EGirdOrientation.Tab:
                default:
                    tabProfiles2.Content ??= new ProfilesView(this);
                    tabMsgView2.Content ??= new MsgView();
                    tabClashProxies2.Content ??= new ClashProxiesView();
                    tabClashConnections2.Content ??= new ClashConnectionsView();
                    break;
            }
            conTheme.Content ??= new ThemeSettingView();

            RestoreUI();
            AddHelpMenuItem();
        }

        #region Event

        private void OnProgramStarted(object state, bool timeout)
        {
            Dispatcher.UIThread.Post(() =>
                    ShowHideWindow(true),
                DispatcherPriority.Default);
        }

        private void DelegateSnackMsg(string content)
        {
            Dispatcher.UIThread.Post(() =>
                       _manager?.Show(new Notification(null, content, NotificationType.Information)),
            DispatcherPriority.Normal);
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.AddServerWindow:
                    if (obj is null) return false;
                    return await new AddServerWindow((ProfileItem)obj).ShowDialog<bool>(this);

                case EViewAction.AddServer2Window:
                    if (obj is null) return false;
                    return await new AddServer2Window((ProfileItem)obj).ShowDialog<bool>(this);

                case EViewAction.DNSSettingWindow:
                    return await new DNSSettingWindow().ShowDialog<bool>(this);

                case EViewAction.RoutingSettingWindow:
                    return await new RoutingSettingWindow().ShowDialog<bool>(this);

                case EViewAction.OptionSettingWindow:
                    return await new OptionSettingWindow().ShowDialog<bool>(this);

                case EViewAction.GlobalHotkeySettingWindow:
                    return await new GlobalHotkeySettingWindow().ShowDialog<bool>(this);

                case EViewAction.SubSettingWindow:
                    return await new SubSettingWindow().ShowDialog<bool>(this);

                case EViewAction.ShowHideWindow:
                    Dispatcher.UIThread.Post(() =>
                        ShowHideWindow((bool?)obj),
                    DispatcherPriority.Default);
                    break;

                case EViewAction.DispatcherStatistics:
                    if (obj is null) return false;
                    Dispatcher.UIThread.Post(() =>
                        ViewModel?.SetStatisticsResult((ServerSpeedItem)obj),
                    DispatcherPriority.Default);
                    break;

                case EViewAction.DispatcherReload:
                    Dispatcher.UIThread.Post(() =>
                        ViewModel?.ReloadResult(),
                    DispatcherPriority.Default);
                    break;

                case EViewAction.Shutdown:
                    StorageUI();
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                    break;

                case EViewAction.ScanScreenTask:
                    await ScanScreenTaskAsync();
                    break;

                case EViewAction.ScanImageTask:
                    await ScanImageTaskAsync();
                    break;

                case EViewAction.AddServerViaClipboard:
                    var clipboardData = await AvaUtils.GetClipboardData(this);
                    ViewModel?.AddServerViaClipboardAsync(clipboardData);
                    break;

                case EViewAction.AdjustMainLvColWidth:
                    Dispatcher.UIThread.Post(() =>
                       Locator.Current.GetService<ProfilesViewModel>()?.AutofitColumnWidthAsync(),
                        DispatcherPriority.Default);
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

                    //case EGlobalHotkey.SystemProxyClear:
                    //    ViewModel?.SetListenerType(ESysProxyType.ForcedClear);
                    //    break;

                    //case EGlobalHotkey.SystemProxySet:
                    //    ViewModel?.SetListenerType(ESysProxyType.ForcedChange);
                    //    break;

                    //case EGlobalHotkey.SystemProxyUnchanged:
                    //    ViewModel?.SetListenerType(ESysProxyType.Unchanged);
                    //    break;

                    //case EGlobalHotkey.SystemProxyPac:
                    //    ViewModel?.SetListenerType(ESysProxyType.Pac);
                    //    break;
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            Logging.SaveLog("OnClosing -> " + e.CloseReason.ToString());

            switch (e.CloseReason)
            {
                case WindowCloseReason.OwnerWindowClosing or WindowCloseReason.WindowClosing:
                    e.Cancel = true;
                    ShowHideWindow(false);
                    break;

                case WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown:
                    await ViewModel?.MyAppExitAsync(true);
                    break;
            }

            base.OnClosing(e);
        }

        private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                switch (e.Key)
                {
                    case Key.V:
                        var clipboardData = await AvaUtils.GetClipboardData(this);
                        ViewModel?.AddServerViaClipboardAsync(clipboardData);
                        break;

                    case Key.S:
                        await ScanScreenTaskAsync();
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

        private void menuPromotion_Click(object? sender, RoutedEventArgs e)
        {
            Utils.ProcessStart($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
        }

        private void menuSettingsSetUWP_Click(object? sender, RoutedEventArgs e)
        {
            Utils.ProcessStart(Utils.GetBinPath("EnableLoopback.exe"));
        }

        public async Task ScanScreenTaskAsync()
        {
            //ShowHideWindow(false);

            NoticeHandler.Instance.SendMessageAndEnqueue("Not yet implemented.(还未实现)");
            return;
            //if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            //{
            //    //var bytes = QRCodeHelper.CaptureScreen(desktop);
            //    //await ViewModel?.ScanScreenResult(bytes);
            //}

            //ShowHideWindow(true);
        }

        private async Task ScanImageTaskAsync()
        {
            var fileName = await UI.OpenFileDialog(this, null);
            if (fileName.IsNullOrEmpty())
            {
                return;
            }
            await ViewModel?.ScanImageResult(fileName);
        }

        private void MenuCheckUpdate_Click(object? sender, RoutedEventArgs e)
        {
            _checkUpdateView ??= new CheckUpdateView();
            DialogHost.Show(_checkUpdateView);
        }

        private void MenuBackupAndRestore_Click(object? sender, RoutedEventArgs e)
        {
            _backupAndRestoreView ??= new BackupAndRestoreView(this);
            DialogHost.Show(_backupAndRestoreView);
        }

        private async void MenuClose_Click(object? sender, RoutedEventArgs e)
        {
            if (await UI.ShowYesNo(this, ResUI.menuExitTips) == ButtonResult.No)
            {
                return;
            }
            StorageUI();

            await ViewModel?.MyAppExitAsync(false);
        }

        #endregion Event

        #region UI
         
        public void ShowHideWindow(bool? blShow)
        {
            var bl = blShow ?? !_config.UiItem.ShowInTaskbar;
            if (bl)
            {
                this.Show();
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }
                this.Activate();
                this.Focus();
            }
            else
            {
                if (_config.UiItem.Hide2TrayWhenClose)
                {
                    this.Hide();
                }
                else
                {
                    this.WindowState = WindowState.Minimized;
                }
            }

            _config.UiItem.ShowInTaskbar = bl;
        }

        private void RestoreUI()
        {
            if (_config.UiItem.MainWidth > 0 && _config.UiItem.MainHeight > 0)
            {
                Width = _config.UiItem.MainWidth;
                Height = _config.UiItem.MainHeight;
            }

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

        private void StorageUI()
        {
            _config.UiItem.MainWidth = Utils.ToInt(this.Width);
            _config.UiItem.MainHeight = Utils.ToInt(this.Height);

            if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
            {
                _config.UiItem.MainGirdHeight1 = Math.Ceiling(gridMain.ColumnDefinitions[0].ActualWidth + 0.1);
                _config.UiItem.MainGirdHeight2 = Math.Ceiling(gridMain.ColumnDefinitions[2].ActualWidth + 0.1);
            }
            else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
            {
                _config.UiItem.MainGirdHeight1 = Math.Ceiling(gridMain1.RowDefinitions[0].ActualHeight + 0.1);
                _config.UiItem.MainGirdHeight2 = Math.Ceiling(gridMain1.RowDefinitions[2].ActualHeight + 0.1);
            }
            ConfigHandler.SaveConfig(_config);
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
                    Tag = it.Url?.Replace(@"/releases", ""),
                    Header = string.Format(ResUI.menuWebsiteItem, it.CoreType.ToString().Replace("_", " ")).UpperFirstChar()
                };
                item.Click += MenuItem_Click;
                menuHelp.Items.Add(item);
            }
        }

        private void MenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                Utils.ProcessStart(item.Tag?.ToString());
            }
        }

        #endregion UI
    }
}