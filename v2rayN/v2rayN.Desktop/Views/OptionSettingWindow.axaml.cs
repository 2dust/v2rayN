using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace v2rayN.Desktop.Views
{
    public partial class OptionSettingWindow : ReactiveWindow<OptionSettingViewModel>
    {
        private static Config _config;

        public OptionSettingWindow()
        {
            InitializeComponent();

            btnCancel.Click += (s, e) => this.Close();
            _config = AppHandler.Instance.Config;

            ViewModel = new OptionSettingViewModel(UpdateViewHandler);

            clbdestOverride.SelectionChanged += ClbdestOverride_SelectionChanged;
            Global.destOverrideProtocols.ForEach(it =>
            {
                clbdestOverride.Items.Add(it);
            });
            _config.Inbound.First().DestOverride?.ForEach(it =>
            {
                clbdestOverride.SelectedItems.Add(it);
            });
            Global.IEProxyProtocols.ForEach(it =>
            {
                cmbsystemProxyAdvancedProtocol.Items.Add(it);
            });
            Global.LogLevels.ForEach(it =>
            {
                cmbloglevel.Items.Add(it);
            });
            Global.Fingerprints.ForEach(it =>
            {
                cmbdefFingerprint.Items.Add(it);
            });
            Global.UserAgent.ForEach(it =>
            {
                cmbdefUserAgent.Items.Add(it);
            });
            Global.SingboxMuxs.ForEach(it =>
            {
                cmbmux4SboxProtocol.Items.Add(it);
            });

            Global.TunMtus.ForEach(it =>
            {
                cmbMtu.Items.Add(it);
            });
            Global.TunStacks.ForEach(it =>
            {
                cmbStack.Items.Add(it);
            });
            Global.CoreTypes.ForEach(it =>
            {
                cmbCoreType1.Items.Add(it);
                cmbCoreType2.Items.Add(it);
                cmbCoreType3.Items.Add(it);
                cmbCoreType4.Items.Add(it);
                cmbCoreType5.Items.Add(it);
                cmbCoreType6.Items.Add(it);
            });

            for (var i = 2; i <= 8; i++)
            {
                cmbMixedConcurrencyCount.Items.Add(i);
            }
            for (var i = 2; i <= 6; i++)
            {
                cmbSpeedTestTimeout.Items.Add(i * 5);
            }
            Global.SpeedTestUrls.ForEach(it =>
            {
                cmbSpeedTestUrl.Items.Add(it);
            });
            Global.SpeedPingTestUrls.ForEach(it =>
            {
                cmbSpeedPingTestUrl.Items.Add(it);
            });
            Global.SubConvertUrls.ForEach(it =>
            {
                cmbSubConvertUrl.Items.Add(it);
            });
            Global.GeoFilesSources.ForEach(it =>
            {
                cmbGetFilesSourceUrl.Items.Add(it);
            });
            Global.SingboxRulesetSources.ForEach(it =>
            {
                cmbSrsFilesSourceUrl.Items.Add(it);
            });
            Global.RoutingRulesSources.ForEach(it =>
            {
                cmbRoutingRulesSourceUrl.Items.Add(it);
            });
            foreach (EGirdOrientation it in Enum.GetValues(typeof(EGirdOrientation)))
            {
                cmbMainGirdOrientation.Items.Add(it.ToString());
            }

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
                this.Bind(ViewModel, vm => vm.CurrentFontFamily, v => v.cmbcurrentFontFamily.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SpeedTestTimeout, v => v.cmbSpeedTestTimeout.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SpeedTestUrl, v => v.cmbSpeedTestUrl.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SpeedPingTestUrl, v => v.cmbSpeedPingTestUrl.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.MixedConcurrencyCount, v => v.cmbMixedConcurrencyCount.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SubConvertUrl, v => v.cmbSubConvertUrl.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.MainGirdOrientation, v => v.cmbMainGirdOrientation.SelectedIndex).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.GeoFileSourceUrl, v => v.cmbGetFilesSourceUrl.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SrsFileSourceUrl, v => v.cmbSrsFilesSourceUrl.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.RoutingRulesSourceUrl, v => v.cmbRoutingRulesSourceUrl.SelectedValue).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.notProxyLocalAddress, v => v.tognotProxyLocalAddress.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.systemProxyAdvancedProtocol, v => v.cmbsystemProxyAdvancedProtocol.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.systemProxyExceptions, v => v.txtsystemProxyExceptions.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.TunStrictRoute, v => v.togStrictRoute.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunStack, v => v.cmbStack.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunMtu, v => v.cmbMtu.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunEnableExInbound, v => v.togEnableExInbound.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunEnableIPv6Address, v => v.togEnableIPv6Address.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunLinuxSudoPassword, v => v.txtLinuxSudoPassword.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.CoreType1, v => v.cmbCoreType1.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType2, v => v.cmbCoreType2.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType3, v => v.cmbCoreType3.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType4, v => v.cmbCoreType4.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType5, v => v.cmbCoreType5.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType6, v => v.cmbCoreType6.SelectedValue).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });

            if (Utils.IsWindows())
            {
                txbSettingsExceptionTip2.IsVisible = false;
            }
            else
            {
                txbSettingsExceptionTip.IsVisible = false;
                panSystemProxyAdvanced.IsVisible = false;
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
            lstFonts.ForEach(it => { cmbcurrentFontFamily.Items.Add(it); });
            cmbcurrentFontFamily.Items.Add(string.Empty);
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
            ViewModel.destOverride = clbdestOverride.SelectedItems.Cast<string>().ToList();
        }
    }
}
