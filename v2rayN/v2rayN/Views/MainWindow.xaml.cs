using ReactiveUI;
using Splat;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;
using v2rayN.Resx;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class MainWindow
    {
        private static Config _config;

        public MainWindow()
        {
            InitializeComponent();

            _config = LazyConfig.Instance.GetConfig();

            App.Current.SessionEnding += Current_SessionEnding;
            this.Closing += MainWindow_Closing;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            ViewModel = new MainWindowViewModel(MainSnackbar.MessageQueue, null);
            Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(MainWindowViewModel));

            for (int i = Global.MinFontSize; i <= Global.MinFontSize + 8; i++)
            {
                cmbCurrentFontSize.Items.Add(i.ToString());
            }

            Global.Languages.ForEach(it =>
            {
                cmbCurrentLanguage.Items.Add(it);
            });

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
                //this.BindCommand(ViewModel, vm => vm.ImportOldGuiConfigCmd, v => v.menuImportOldGuiConfig).DisposeWith(disposables);

                //check update
                this.BindCommand(ViewModel, vm => vm.CheckUpdateNCmd, v => v.menuCheckUpdateN).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateXrayCoreCmd, v => v.menuCheckUpdateXrayCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateClashMetaCoreCmd, v => v.menuCheckUpdateMihomoCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateSingBoxCoreCmd, v => v.menuCheckUpdateSingBoxCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateGeoCmd, v => v.menuCheckUpdateGeo).DisposeWith(disposables);

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

                this.OneWayBind(ViewModel, vm => vm.NotifyIcon, v => v.tbNotify.Icon).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningServerToolTipText, v => v.tbNotify.ToolTipText).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.NotifyLeftClickCmd, v => v.tbNotify.LeftClickCommand).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.AppIcon, v => v.Icon).DisposeWith(disposables);

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

                //UI
                this.Bind(ViewModel, vm => vm.ColorModeDark, v => v.togDarkMode.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.FollowSystemTheme, v => v.followSystemTheme.IsChecked).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.Swatches, v => v.cmbSwatches.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSwatch, v => v.cmbSwatches.SelectedItem).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentFontSize, v => v.cmbCurrentFontSize.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentLanguage, v => v.cmbCurrentLanguage.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashUI.Visibility).DisposeWith(disposables);
            });

            RestoreUI();
            AddHelpMenuItem();

            var IsAdministrator = Utils.IsAdministrator();
            this.Title = $"{Utils.GetVersion()} - {(IsAdministrator ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";

            if (!_config.guiItem.enableHWA)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            var helper = new WindowInteropHelper(this);
            var hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
            hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (_config.uiItem.followSystemTheme)
                {
                    const int WM_SETTINGCHANGE = 0x001A;
                    if (msg == WM_SETTINGCHANGE)
                    {
                        if (wParam == IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
                        {
                            ViewModel?.ModifyTheme(!Utils.IsLightTheme());
                        }
                    }
                }

                return IntPtr.Zero;
            });
            if (tabProfiles.Content is null)
            {
                tabProfiles.Content = new ProfilesView();
            }
            if (tabMsgView.Content is null)
            {
                tabMsgView.Content = new MsgView();
            }
            if (tabClashUI.Content is null)
            {
                tabClashUI.Content = new ClashProxiesView();
            }
        }

        #region Event

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            ViewModel?.ShowHideWindow(false);
        }

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            tabProfiles = null;

            tbNotify.Dispose();
            StorageUI();
            ViewModel?.MyAppExit(false);
        }

        private void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Logging.SaveLog("Current_SessionEnding");
            StorageUI();
            ViewModel?.MyAppExit(true);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                switch (e.Key)
                {
                    case Key.V:
                        ViewModel?.AddServerViaClipboard();
                        break;

                    case Key.S:
                        ViewModel?.ScanScreenTaskAsync().ContinueWith(_ => { });
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
            ViewModel?.ShowHideWindow(false);
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

        #endregion Event

        #region UI

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
                gridMain.ColumnDefinitions[0].Width = new GridLength(_config.uiItem.mainGirdHeight1, GridUnitType.Star);
                gridMain.ColumnDefinitions[2].Width = new GridLength(_config.uiItem.mainGirdHeight2, GridUnitType.Star);
            }
        }

        private void StorageUI()
        {
            _config.uiItem.mainWidth = Utils.ToInt(this.Width);
            _config.uiItem.mainHeight = Utils.ToInt(this.Height);

            _config.uiItem.mainGirdHeight1 = Math.Ceiling(gridMain.ColumnDefinitions[0].ActualWidth + 0.1);
            _config.uiItem.mainGirdHeight2 = Math.Ceiling(gridMain.ColumnDefinitions[2].ActualWidth + 0.1);
        }

        private void AddHelpMenuItem()
        {
            var coreInfo = LazyConfig.Instance.GetCoreInfo();
            foreach (var it in coreInfo
                .Where(t => t.coreType != ECoreType.v2fly
                            && t.coreType != ECoreType.clash
                            && t.coreType != ECoreType.clash_meta
                            && t.coreType != ECoreType.hysteria))
            {
                var item = new MenuItem()
                {
                    Tag = it.coreUrl.Replace(@"/releases", ""),
                    Header = string.Format(Resx.ResUI.menuWebsiteItem, it.coreType.ToString().Replace("_", " ")).UpperFirstChar()
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