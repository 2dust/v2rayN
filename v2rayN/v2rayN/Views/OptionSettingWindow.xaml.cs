using ReactiveUI;
using System.Globalization;
using System.IO;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Media;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class OptionSettingWindow
    {
        private static Config _config;

        public OptionSettingWindow()
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            _config = LazyConfig.Instance.GetConfig();
            var lstFonts = GetFonts(Utils.GetFontsPath());

            ViewModel = new OptionSettingViewModel(this);

            clbdestOverride.SelectionChanged += ClbdestOverride_SelectionChanged;
            Global.destOverrideProtocols.ForEach(it =>
            {
                clbdestOverride.Items.Add(it);
            });
            _config.inbound[0].destOverride?.ForEach(it =>
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

            for (int i = 2; i <= 6; i++)
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
            foreach (EGirdOrientation it in Enum.GetValues(typeof(EGirdOrientation)))
            {
                cmbMainGirdOrientation.Items.Add(it.ToString());
            }

            lstFonts.ForEach(it => { cmbcurrentFontFamily.Items.Add(it); });
            cmbcurrentFontFamily.Items.Add(string.Empty);

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.localPort, v => v.txtlocalPort.Text).DisposeWith(disposables);
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
                this.Bind(ViewModel, vm => vm.loglevel, v => v.cmbloglevel.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.defAllowInsecure, v => v.togdefAllowInsecure.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.defFingerprint, v => v.cmbdefFingerprint.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.defUserAgent, v => v.cmbdefUserAgent.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.mux4SboxProtocol, v => v.cmbmux4SboxProtocol.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.enableCacheFile4Sbox, v => v.togenableCacheFile4Sbox.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.hyUpMbps, v => v.txtUpMbps.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.hyDownMbps, v => v.txtDownMbps.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.enableFragment, v => v.togenableFragment.IsChecked).DisposeWith(disposables);

                //this.Bind(ViewModel, vm => vm.Kcpmtu, v => v.txtKcpmtu.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.Kcptti, v => v.txtKcptti.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpuplinkCapacity, v => v.txtKcpuplinkCapacity.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpdownlinkCapacity, v => v.txtKcpdownlinkCapacity.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpreadBufferSize, v => v.txtKcpreadBufferSize.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpwriteBufferSize, v => v.txtKcpwriteBufferSize.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.Kcpcongestion, v => v.togKcpcongestion.IsChecked).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.AutoRun, v => v.togAutoRun.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableStatistics, v => v.togEnableStatistics.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.KeepOlderDedupl, v => v.togKeepOlderDedupl.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.IgnoreGeoUpdateCore, v => v.togIgnoreGeoUpdateCore.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableAutoAdjustMainLvColWidth, v => v.togEnableAutoAdjustMainLvColWidth.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableUpdateSubOnlyRemarksExist, v => v.togEnableUpdateSubOnlyRemarksExist.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableSecurityProtocolTls13, v => v.togEnableSecurityProtocolTls13.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoHideStartup, v => v.togAutoHideStartup.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableCheckPreReleaseUpdate, v => v.togEnableCheckPreReleaseUpdate.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableDragDropSort, v => v.togEnableDragDropSort.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.DoubleClick2Activate, v => v.togDoubleClick2Activate.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoUpdateInterval, v => v.txtautoUpdateInterval.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TrayMenuServersLimit, v => v.txttrayMenuServersLimit.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentFontFamily, v => v.cmbcurrentFontFamily.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SpeedTestTimeout, v => v.cmbSpeedTestTimeout.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SpeedTestUrl, v => v.cmbSpeedTestUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SpeedPingTestUrl, v => v.cmbSpeedPingTestUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableHWA, v => v.togEnableHWA.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SubConvertUrl, v => v.cmbSubConvertUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.MainGirdOrientation, v => v.cmbMainGirdOrientation.SelectedIndex).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.systemProxyAdvancedProtocol, v => v.cmbsystemProxyAdvancedProtocol.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.systemProxyExceptions, v => v.txtsystemProxyExceptions.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.TunStrictRoute, v => v.togStrictRoute.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunStack, v => v.cmbStack.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunMtu, v => v.cmbMtu.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunEnableExInbound, v => v.togEnableExInbound.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunEnableIPv6Address, v => v.togEnableIPv6Address.IsChecked).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.CoreType1, v => v.cmbCoreType1.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType2, v => v.cmbCoreType2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType3, v => v.cmbCoreType3.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType4, v => v.cmbCoreType4.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType5, v => v.cmbCoreType5.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType6, v => v.cmbCoreType6.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private List<string> GetFonts(string path)
        {
            var lstFonts = new List<string>();
            try
            {
                string[] searchPatterns = { "*.ttf", "*.ttc" };
                var files = new List<string>();
                foreach (var pattern in searchPatterns)
                {
                    files.AddRange(Directory.GetFiles(path, pattern));
                }
                var culture = _config.uiItem.currentLanguage == Global.Languages[0] ? "zh-cn" : "en-us";
                var culture2 = "en-us";
                foreach (var ttf in files)
                {
                    var families = Fonts.GetFontFamilies(Utils.GetFontsPath(ttf));
                    foreach (FontFamily family in families)
                    {
                        var typefaces = family.GetTypefaces();
                        foreach (Typeface typeface in typefaces)
                        {
                            typeface.TryGetGlyphTypeface(out GlyphTypeface glyph);
                            //var fontFace = glyph.Win32FaceNames[new CultureInfo("en-us")];
                            //if (!fontFace.Equals("Regular") && !fontFace.Equals("Normal"))
                            //{
                            //    continue;
                            //}
                            var fontFamily = glyph.Win32FamilyNames[new CultureInfo(culture)];
                            if (Utils.IsNullOrEmpty(fontFamily))
                            {
                                fontFamily = glyph.Win32FamilyNames[new CultureInfo(culture2)];
                                if (Utils.IsNullOrEmpty(fontFamily))
                                {
                                    continue;
                                }
                            }
                            lstFonts.Add(fontFamily);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog("fill fonts error", ex);
            }
            return lstFonts;
        }

        private void ClbdestOverride_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.destOverride = clbdestOverride.SelectedItems.Cast<string>().ToList();
        }
    }
}