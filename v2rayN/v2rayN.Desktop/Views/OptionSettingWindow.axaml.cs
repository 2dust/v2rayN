using System.Reactive.Disposables;
using Avalonia.Controls;
using ReactiveUI;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class OptionSettingWindow : WindowBase<OptionSettingViewModel>
{
    private static Config _config;

    public OptionSettingWindow()
    {
        InitializeComponent();

        btnCancel.Click += (s, e) => this.Close();
        _config = AppHandler.Instance.Config;

        ViewModel = new OptionSettingViewModel(UpdateViewHandler);

        clbdestOverride.SelectionChanged += ClbdestOverride_SelectionChanged;
        clbdestOverride.ItemsSource = Global.destOverrideProtocols;
        _config.Inbound.First().DestOverride?.ForEach(it =>
        {
            clbdestOverride.SelectedItems.Add(it);
        });

        cmbsystemProxyAdvancedProtocol.ItemsSource = Global.IEProxyProtocols;
        cmbloglevel.ItemsSource = Global.LogLevels;
        cmbdefFingerprint.ItemsSource = Global.Fingerprints;
        cmbdefUserAgent.ItemsSource = Global.UserAgent;
        cmbmux4SboxProtocol.ItemsSource = Global.SingboxMuxs;
        cmbMtu.ItemsSource = Global.TunMtus;
        cmbStack.ItemsSource = Global.TunStacks;

        cmbCoreType1.ItemsSource = Global.CoreTypes;
        cmbCoreType2.ItemsSource = Global.CoreTypes;
        cmbCoreType3.ItemsSource = Global.CoreTypes;
        cmbCoreType4.ItemsSource = Global.CoreTypes;
        cmbCoreType5.ItemsSource = Global.CoreTypes;
        cmbCoreType6.ItemsSource = Global.CoreTypes;
        cmbCoreType9.ItemsSource = Global.CoreTypes;

        cmbCoreSplitRouteType.ItemsSource = Global.CoreTypes;

        cmbCoreSplitType1.ItemsSource = Global.CoreTypes;
        cmbCoreSplitType3.ItemsSource = Global.CoreTypes;
        cmbCoreSplitType4.ItemsSource = Global.CoreTypes;
        cmbCoreSplitType5.ItemsSource = Global.CoreTypes;
        cmbCoreSplitType6.ItemsSource = Global.CoreTypes;
        cmbCoreSplitType7.ItemsSource = Global.Hysteria2CoreTypes;
        cmbCoreSplitType8.ItemsSource = Global.TuicCoreTypes;
        cmbCoreSplitType9.ItemsSource = Global.CoreTypes;

        cmbMixedConcurrencyCount.ItemsSource = Enumerable.Range(2, 7).ToList();
        cmbSpeedTestTimeout.ItemsSource = Enumerable.Range(2, 5).Select(i => i * 5).ToList();
        cmbSpeedTestUrl.ItemsSource = Global.SpeedTestUrls;
        cmbSpeedPingTestUrl.ItemsSource = Global.SpeedPingTestUrls;
        cmbSubConvertUrl.ItemsSource = Global.SubConvertUrls;
        cmbGetFilesSourceUrl.ItemsSource = Global.GeoFilesSources;
        cmbSrsFilesSourceUrl.ItemsSource = Global.SingboxRulesetSources;
        cmbRoutingRulesSourceUrl.ItemsSource = Global.RoutingRulesSources;
        cmbIPAPIUrl.ItemsSource = Global.IPAPIUrls;

        cmbMainGirdOrientation.ItemsSource = Utils.GetEnumNames<EGirdOrientation>();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.localPort, v => v.txtlocalPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SecondLocalPortEnabled, v => v.togSecondLocalPortEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.udpEnabled, v => v.togudpEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.sniffingEnabled, v => v.togsniffingEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.routeOnly, v => v.togrouteOnly.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.allowLANConn, v => v.togAllowLANConn.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.newPort4LAN, v => v.togNewPort4LAN.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.newPort4LAN, v => v.txtuser.IsEnabled).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.newPort4LAN, v => v.txtpass.IsEnabled).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.user, v => v.txtuser.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.pass, v => v.txtpass.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.muxEnabled, v => v.togmuxEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.logEnabled, v => v.toglogEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.loglevel, v => v.cmbloglevel.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.defAllowInsecure, v => v.togdefAllowInsecure.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.defFingerprint, v => v.cmbdefFingerprint.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.defUserAgent, v => v.cmbdefUserAgent.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.mux4SboxProtocol, v => v.cmbmux4SboxProtocol.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.enableCacheFile4Sbox, v => v.togenableCacheFile4Sbox.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.hyUpMbps, v => v.txtUpMbps.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.hyDownMbps, v => v.txtDownMbps.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.enableFragment, v => v.togenableFragment.IsChecked).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.AutoRun, v => v.togAutoRun.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.EnableStatistics, v => v.togEnableStatistics.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DisplayRealTimeSpeed, v => v.togDisplayRealTimeSpeed.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KeepOlderDedupl, v => v.togKeepOlderDedupl.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.EnableAutoAdjustMainLvColWidth, v => v.togEnableAutoAdjustMainLvColWidth.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.EnableUpdateSubOnlyRemarksExist, v => v.togEnableUpdateSubOnlyRemarksExist.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.EnableSecurityProtocolTls13, v => v.togEnableSecurityProtocolTls13.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoHideStartup, v => v.togAutoHideStartup.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Hide2TrayWhenClose, v => v.togHide2TrayWhenClose.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DoubleClick2Activate, v => v.togDoubleClick2Activate.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoUpdateInterval, v => v.txtautoUpdateInterval.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CurrentFontFamily, v => v.cmbcurrentFontFamily.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SpeedTestTimeout, v => v.cmbSpeedTestTimeout.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SpeedTestUrl, v => v.cmbSpeedTestUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SpeedPingTestUrl, v => v.cmbSpeedPingTestUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.MixedConcurrencyCount, v => v.cmbMixedConcurrencyCount.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SubConvertUrl, v => v.cmbSubConvertUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.MainGirdOrientation, v => v.cmbMainGirdOrientation.SelectedIndex).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GeoFileSourceUrl, v => v.cmbGetFilesSourceUrl.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SrsFileSourceUrl, v => v.cmbSrsFilesSourceUrl.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RoutingRulesSourceUrl, v => v.cmbRoutingRulesSourceUrl.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IPAPIUrl, v => v.cmbIPAPIUrl.SelectedValue).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.notProxyLocalAddress, v => v.tognotProxyLocalAddress.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.systemProxyAdvancedProtocol, v => v.cmbsystemProxyAdvancedProtocol.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.systemProxyExceptions, v => v.txtsystemProxyExceptions.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.TunStrictRoute, v => v.togStrictRoute.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TunStack, v => v.cmbStack.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TunMtu, v => v.cmbMtu.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TunEnableExInbound, v => v.togEnableExInbound.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TunEnableIPv6Address, v => v.togEnableIPv6Address.IsChecked).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.CoreType1, v => v.cmbCoreType1.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType2, v => v.cmbCoreType2.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType3, v => v.cmbCoreType3.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType4, v => v.cmbCoreType4.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType5, v => v.cmbCoreType5.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType6, v => v.cmbCoreType6.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType9, v => v.cmbCoreType9.SelectedValue).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.EnableSplitCore, v => v.togCoreSplit.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RouteSplitCoreType, v => v.cmbCoreSplitRouteType.SelectedValue).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SplitCoreType1, v => v.cmbCoreSplitType1.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType3, v => v.cmbCoreSplitType3.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType4, v => v.cmbCoreSplitType4.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType5, v => v.cmbCoreSplitType5.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType6, v => v.cmbCoreSplitType6.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType7, v => v.cmbCoreSplitType7.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType8, v => v.cmbCoreSplitType8.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SplitCoreType9, v => v.cmbCoreSplitType9.SelectedValue).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });

        if (Utils.IsWindows())
        {
            txbSettingsExceptionTip2.IsVisible = false;

            labHide2TrayWhenClose.IsVisible = false;
            togHide2TrayWhenClose.IsVisible = false;
        }
        else if (Utils.IsLinux())
        {
            txbSettingsExceptionTip.IsVisible = false;
            panSystemProxyAdvanced.IsVisible = false;
        }
        else if (Utils.IsOSX())
        {
            txbSettingsExceptionTip.IsVisible = false;
            panSystemProxyAdvanced.IsVisible = false;

            labHide2TrayWhenClose.IsVisible = false;
            togHide2TrayWhenClose.IsVisible = false;
        }
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                this.Close(true);
                break;

            case EViewAction.InitSettingFont:
                await InitSettingFont();
                break;
        }
        return await Task.FromResult(true);
    }

    private async Task InitSettingFont()
    {
        var lstFonts = await GetFonts();

        lstFonts.Add(string.Empty);
        cmbcurrentFontFamily.ItemsSource = lstFonts;
    }

    private async Task<List<string>> GetFonts()
    {
        var lstFonts = new List<string>();
        try
        {
            if (Utils.IsWindows())
            {
                return lstFonts;
            }
            else if (Utils.IsNonWindows())
            {
                var result = await Utils.GetLinuxFontFamily("zh");
                if (result.IsNullOrEmpty())
                {
                    return lstFonts;
                }

                var lst = result.Split(Environment.NewLine)
                    .Where(t => t.IsNotEmpty())
                    .ToList()
                    .Select(t => t.Split(",").FirstOrDefault() ?? "")
                    .OrderBy(t => t)
                    .ToList();
                return lst;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("GetFonts", ex);
        }
        return lstFonts;
    }

    private void ClbdestOverride_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.destOverride = clbdestOverride.SelectedItems.Cast<string>().ToList();
        }
    }
}
