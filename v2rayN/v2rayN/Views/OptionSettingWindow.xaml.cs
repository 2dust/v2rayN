using ReactiveUI;
using System.Globalization;
using System.IO;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Media;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class OptionSettingWindow
    {
        private static Config _config;

        public OptionSettingWindow()
        {
            InitializeComponent();
            _config = LazyConfig.Instance.GetConfig();

            ViewModel = new OptionSettingViewModel(this);

            Global.IEProxyProtocols.ForEach(it =>
            {
                cmbsystemProxyAdvancedProtocol.Items.Add(it);
            });
            Global.LogLevel.ForEach(it =>
            {
                cmbloglevel.Items.Add(it);
            });
            Global.fingerprints.ForEach(it =>
            {
                cmbdefFingerprint.Items.Add(it);
            });
            Global.userAgent.ForEach(it =>
            {
                cmbdefUserAgent.Items.Add(it);
            });
            Global.domainStrategy4Freedoms.ForEach(it =>
            {
                cmbdomainStrategy4Freedom.Items.Add(it);
            });
            for (int i = 1; i <= 10; i++)
            {
                cmbStatisticsFreshRate.Items.Add(i);
            }
            Global.TunMtus.ForEach(it =>
            {
                cmbMtu.Items.Add(it);
            });
            Global.TunStacks.ForEach(it =>
            {
                cmbStack.Items.Add(it);
            });
            Global.coreTypes.ForEach(it =>
            {
                cmbCoreType1.Items.Add(it);
                cmbCoreType2.Items.Add(it);
                cmbCoreType3.Items.Add(it);
                cmbCoreType4.Items.Add(it);
                cmbCoreType5.Items.Add(it);
                cmbCoreType6.Items.Add(it);
            });

            //fill fonts
            try
            {
                var dir = new DirectoryInfo(Utils.GetFontsPath());
                var files = dir.GetFiles("*.ttf");
                var culture = _config.uiItem.currentLanguage.Equals(Global.Languages[0]) ? "zh-cn" : "en-us";
                foreach (var it in files)
                {
                    var families = Fonts.GetFontFamilies(Utils.GetFontsPath(it.Name));
                    foreach (FontFamily family in families)
                    {
                        var typefaces = family.GetTypefaces();
                        foreach (Typeface typeface in typefaces)
                        {
                            typeface.TryGetGlyphTypeface(out GlyphTypeface glyph);
                            var fontFace = glyph.Win32FaceNames[new CultureInfo("en-us")];
                            if (!fontFace.Equals("Regular") && !fontFace.Equals("Normal"))
                            {
                                continue;
                            }
                            var fontFamily = glyph.Win32FamilyNames[new CultureInfo(culture)];
                            if (Utils.IsNullOrEmpty(fontFamily))
                            {
                                continue;
                            }
                            cmbcurrentFontFamily.Items.Add(fontFamily);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog("fill fonts error", ex);
            }
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


                this.Bind(ViewModel, vm => vm.domainStrategy4Freedom, v => v.cmbdomainStrategy4Freedom.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.remoteDNS, v => v.txtremoteDNS.Text).DisposeWith(disposables);


                //this.Bind(ViewModel, vm => vm.Kcpmtu, v => v.txtKcpmtu.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.Kcptti, v => v.txtKcptti.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpuplinkCapacity, v => v.txtKcpuplinkCapacity.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpdownlinkCapacity, v => v.txtKcpdownlinkCapacity.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpreadBufferSize, v => v.txtKcpreadBufferSize.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.KcpwriteBufferSize, v => v.txtKcpwriteBufferSize.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.Kcpcongestion, v => v.togKcpcongestion.IsChecked).DisposeWith(disposables);


                this.Bind(ViewModel, vm => vm.AutoRun, v => v.togAutoRun.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableStatistics, v => v.togEnableStatistics.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.StatisticsFreshRate, v => v.cmbStatisticsFreshRate.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.KeepOlderDedupl, v => v.togKeepOlderDedupl.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.IgnoreGeoUpdateCore, v => v.togIgnoreGeoUpdateCore.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableAutoAdjustMainLvColWidth, v => v.togEnableAutoAdjustMainLvColWidth.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableSecurityProtocolTls13, v => v.togEnableSecurityProtocolTls13.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoHideStartup, v => v.togAutoHideStartup.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableCheckPreReleaseUpdate, v => v.togEnableCheckPreReleaseUpdate.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableDragDropSort, v => v.togEnableDragDropSort.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.DoubleClick2Activate, v => v.togDoubleClick2Activate.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.autoUpdateInterval, v => v.txtautoUpdateInterval.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.autoUpdateSubInterval, v => v.txtautoUpdateSubInterval.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.trayMenuServersLimit, v => v.txttrayMenuServersLimit.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.currentFontFamily, v => v.cmbcurrentFontFamily.Text).DisposeWith(disposables);


                this.Bind(ViewModel, vm => vm.systemProxyAdvancedProtocol, v => v.cmbsystemProxyAdvancedProtocol.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.systemProxyExceptions, v => v.txtsystemProxyExceptions.Text).DisposeWith(disposables);


                this.Bind(ViewModel, vm => vm.TunShowWindow, v => v.togShowWindow.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunStrictRoute, v => v.togStrictRoute.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunStack, v => v.cmbStack.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunMtu, v => v.cmbMtu.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunCustomTemplate, v => v.txtCustomTemplate.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunDirectIP, v => v.txtDirectIP.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.TunDirectProcess, v => v.txtDirectProcess.Text).DisposeWith(disposables);


                this.Bind(ViewModel, vm => vm.CoreType1, v => v.cmbCoreType1.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType2, v => v.cmbCoreType2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType3, v => v.cmbCoreType3.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType4, v => v.cmbCoreType4.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType5, v => v.cmbCoreType5.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CoreType6, v => v.cmbCoreType6.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);

            });
        }
        private void linkDnsObjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://www.v2fly.org/config/dns.html#dnsobject");
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void btnBrowse_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            openFileDialog1.Filter = "tunConfig|*.json|All|*.*";
            openFileDialog1.ShowDialog();

            txtCustomTemplate.Text = openFileDialog1.FileName;
        }
    }
}
