using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class OptionSettingViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;

        #region Core

        [Reactive] public int localPort { get; set; }
        [Reactive] public bool udpEnabled { get; set; }
        [Reactive] public bool sniffingEnabled { get; set; }
        [Reactive] public bool routeOnly { get; set; }
        [Reactive] public bool allowLANConn { get; set; }
        [Reactive] public bool newPort4LAN { get; set; }
        [Reactive] public string user { get; set; }
        [Reactive] public string pass { get; set; }
        [Reactive] public bool muxEnabled { get; set; }
        [Reactive] public bool logEnabled { get; set; }
        [Reactive] public string loglevel { get; set; }
        [Reactive] public bool defAllowInsecure { get; set; }
        [Reactive] public string defFingerprint { get; set; }
        [Reactive] public string defUserAgent { get; set; }

        #endregion Core

        #region Core DNS

        [Reactive] public string domainStrategy4Freedom { get; set; }
        [Reactive] public string remoteDNS { get; set; }

        #endregion Core DNS

        #region Core KCP

        //[Reactive] public int Kcpmtu { get; set; }
        //[Reactive] public int Kcptti { get; set; }
        //[Reactive] public int KcpuplinkCapacity { get; set; }
        //[Reactive] public int KcpdownlinkCapacity { get; set; }
        //[Reactive] public int KcpreadBufferSize { get; set; }
        //[Reactive] public int KcpwriteBufferSize { get; set; }
        //[Reactive] public bool Kcpcongestion { get; set; }

        #endregion Core KCP

        #region UI

        [Reactive] public bool AutoRun { get; set; }
        [Reactive] public bool EnableStatistics { get; set; }
        [Reactive] public int StatisticsFreshRate { get; set; }
        [Reactive] public bool KeepOlderDedupl { get; set; }
        [Reactive] public bool IgnoreGeoUpdateCore { get; set; }
        [Reactive] public bool EnableAutoAdjustMainLvColWidth { get; set; }
        [Reactive] public bool EnableSecurityProtocolTls13 { get; set; }
        [Reactive] public bool AutoHideStartup { get; set; }
        [Reactive] public bool EnableCheckPreReleaseUpdate { get; set; }
        [Reactive] public bool EnableDragDropSort { get; set; }
        [Reactive] public bool DoubleClick2Activate { get; set; }
        [Reactive] public int autoUpdateInterval { get; set; }
        [Reactive] public int trayMenuServersLimit { get; set; }
        [Reactive] public string currentFontFamily { get; set; }
        [Reactive] public int SpeedTestTimeout { get; set; }
        [Reactive] public string SpeedTestUrl { get; set; }
        [Reactive] public bool EnableHWA { get; set; }
        [Reactive] public string SubConvertUrl { get; set; }

        #endregion UI

        #region System proxy

        [Reactive] public string systemProxyAdvancedProtocol { get; set; }
        [Reactive] public string systemProxyExceptions { get; set; }

        #endregion System proxy

        #region Tun mode

        [Reactive] public bool TunShowWindow { get; set; }
        [Reactive] public bool TunEnabledLog { get; set; }
        [Reactive] public bool TunStrictRoute { get; set; }
        [Reactive] public string TunStack { get; set; }
        [Reactive] public int TunMtu { get; set; }
        [Reactive] public string TunCustomTemplate { get; set; }
        [Reactive] public bool TunBypassMode { get; set; }
        [Reactive] public bool TunBypassMode2 { get; set; }
        [Reactive] public string TunDirectIP { get; set; }
        [Reactive] public string TunDirectProcess { get; set; }
        [Reactive] public string TunDirectDNS { get; set; }
        [Reactive] public string TunProxyIP { get; set; }
        [Reactive] public string TunProxyProcess { get; set; }
        [Reactive] public string TunProxyDNS { get; set; }

        #endregion Tun mode

        #region CoreType

        [Reactive] public string CoreType1 { get; set; }
        [Reactive] public string CoreType2 { get; set; }
        [Reactive] public string CoreType3 { get; set; }
        [Reactive] public string CoreType4 { get; set; }
        [Reactive] public string CoreType5 { get; set; }
        [Reactive] public string CoreType6 { get; set; }

        #endregion CoreType

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public OptionSettingViewModel(Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;

            #region Core

            var inbound = _config.inbound[0];
            localPort = inbound.localPort;
            udpEnabled = inbound.udpEnabled;
            sniffingEnabled = inbound.sniffingEnabled;
            routeOnly = inbound.routeOnly;
            allowLANConn = inbound.allowLANConn;
            newPort4LAN = inbound.newPort4LAN;
            user = inbound.user;
            pass = inbound.pass;
            muxEnabled = _config.coreBasicItem.muxEnabled;
            logEnabled = _config.coreBasicItem.logEnabled;
            loglevel = _config.coreBasicItem.loglevel;
            defAllowInsecure = _config.coreBasicItem.defAllowInsecure;
            defFingerprint = _config.coreBasicItem.defFingerprint;
            defUserAgent = _config.coreBasicItem.defUserAgent;

            #endregion Core

            #region Core DNS

            domainStrategy4Freedom = _config.domainStrategy4Freedom;
            remoteDNS = _config.remoteDNS;

            #endregion Core DNS

            #region Core KCP

            //Kcpmtu = _config.kcpItem.mtu;
            //Kcptti = _config.kcpItem.tti;
            //KcpuplinkCapacity = _config.kcpItem.uplinkCapacity;
            //KcpdownlinkCapacity = _config.kcpItem.downlinkCapacity;
            //KcpreadBufferSize = _config.kcpItem.readBufferSize;
            //KcpwriteBufferSize = _config.kcpItem.writeBufferSize;
            //Kcpcongestion = _config.kcpItem.congestion;

            #endregion Core KCP

            #region UI

            AutoRun = _config.guiItem.autoRun;
            EnableStatistics = _config.guiItem.enableStatistics;
            StatisticsFreshRate = _config.guiItem.statisticsFreshRate;
            KeepOlderDedupl = _config.guiItem.keepOlderDedupl;
            IgnoreGeoUpdateCore = _config.guiItem.ignoreGeoUpdateCore;
            EnableAutoAdjustMainLvColWidth = _config.uiItem.enableAutoAdjustMainLvColWidth;
            EnableSecurityProtocolTls13 = _config.guiItem.enableSecurityProtocolTls13;
            AutoHideStartup = _config.uiItem.autoHideStartup;
            EnableCheckPreReleaseUpdate = _config.guiItem.checkPreReleaseUpdate;
            EnableDragDropSort = _config.uiItem.enableDragDropSort;
            DoubleClick2Activate = _config.uiItem.doubleClick2Activate;
            autoUpdateInterval = _config.guiItem.autoUpdateInterval;
            trayMenuServersLimit = _config.guiItem.trayMenuServersLimit;
            currentFontFamily = _config.uiItem.currentFontFamily;
            SpeedTestTimeout = _config.speedTestItem.speedTestTimeout;
            SpeedTestUrl = _config.speedTestItem.speedTestUrl;
            EnableHWA = _config.guiItem.enableHWA;
            SubConvertUrl = _config.constItem.subConvertUrl;

            #endregion UI

            #region System proxy

            systemProxyAdvancedProtocol = _config.systemProxyAdvancedProtocol;
            systemProxyExceptions = _config.systemProxyExceptions;

            #endregion System proxy

            #region Tun mode

            TunShowWindow = _config.tunModeItem.showWindow;
            TunEnabledLog = _config.tunModeItem.enabledLog;
            TunStrictRoute = _config.tunModeItem.strictRoute;
            TunStack = _config.tunModeItem.stack;
            TunMtu = _config.tunModeItem.mtu;
            TunCustomTemplate = _config.tunModeItem.customTemplate;
            TunBypassMode = _config.tunModeItem.bypassMode;
            TunDirectIP = Utils.List2String(_config.tunModeItem.directIP, true);
            TunDirectProcess = Utils.List2String(_config.tunModeItem.directProcess, true);
            TunDirectDNS = _config.tunModeItem.directDNS;
            TunProxyIP = Utils.List2String(_config.tunModeItem.proxyIP, true);
            TunProxyProcess = Utils.List2String(_config.tunModeItem.proxyProcess, true);
            TunProxyDNS = _config.tunModeItem.proxyDNS;
            this.WhenAnyValue(
              x => x.TunBypassMode)
              .Subscribe(c => TunBypassMode2 = !TunBypassMode);

            #endregion Tun mode

            InitCoreType();

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveSetting();
            });

            Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void InitCoreType()
        {
            if (_config.coreTypeItem == null)
            {
                _config.coreTypeItem = new List<CoreTypeItem>();
            }

            foreach (EConfigType it in Enum.GetValues(typeof(EConfigType)))
            {
                if (_config.coreTypeItem.FindIndex(t => t.configType == it) >= 0)
                {
                    continue;
                }

                _config.coreTypeItem.Add(new CoreTypeItem()
                {
                    configType = it,
                    coreType = ECoreType.Xray
                });
            }
            _config.coreTypeItem.ForEach(it =>
            {
                var type = it.coreType.ToString();
                switch ((int)it.configType)
                {
                    case 1:
                        CoreType1 = type;
                        break;

                    case 2:
                        CoreType2 = type;
                        break;

                    case 3:
                        CoreType3 = type;
                        break;

                    case 4:
                        CoreType4 = type;
                        break;

                    case 5:
                        CoreType5 = type;
                        break;

                    case 6:
                        CoreType6 = type;
                        break;
                }
            });
        }

        private void SaveSetting()
        {
            if (Utils.IsNullOrEmpty(localPort.ToString()) || !Utils.IsNumberic(localPort.ToString())
               || localPort <= 0 || localPort >= Global.MaxPort)
            {
                UI.Show(ResUI.FillLocalListeningPort);
                return;
            }

            var obj = Utils.ParseJson(remoteDNS);
            if (obj?.ContainsKey("servers") == true)
            {
            }
            else
            {
                if (remoteDNS.Contains("{") || remoteDNS.Contains("}"))
                {
                    UI.Show(ResUI.FillCorrectDNSText);
                    return;
                }
            }

            //if (Utils.IsNullOrEmpty(Kcpmtu.ToString()) || !Utils.IsNumberic(Kcpmtu.ToString())
            //       || Utils.IsNullOrEmpty(Kcptti.ToString()) || !Utils.IsNumberic(Kcptti.ToString())
            //       || Utils.IsNullOrEmpty(KcpuplinkCapacity.ToString()) || !Utils.IsNumberic(KcpuplinkCapacity.ToString())
            //       || Utils.IsNullOrEmpty(KcpdownlinkCapacity.ToString()) || !Utils.IsNumberic(KcpdownlinkCapacity.ToString())
            //       || Utils.IsNullOrEmpty(KcpreadBufferSize.ToString()) || !Utils.IsNumberic(KcpreadBufferSize.ToString())
            //       || Utils.IsNullOrEmpty(KcpwriteBufferSize.ToString()) || !Utils.IsNumberic(KcpwriteBufferSize.ToString()))
            //{
            //    UI.Show(ResUI.FillKcpParameters);
            //    return;
            //}

            //Core
            _config.inbound[0].localPort = localPort;
            _config.inbound[0].udpEnabled = udpEnabled;
            _config.inbound[0].sniffingEnabled = sniffingEnabled;
            _config.inbound[0].routeOnly = routeOnly;
            _config.inbound[0].allowLANConn = allowLANConn;
            _config.inbound[0].newPort4LAN = newPort4LAN;
            _config.inbound[0].user = user;
            _config.inbound[0].pass = pass;
            if (_config.inbound.Count > 1)
            {
                _config.inbound.RemoveAt(1);
            }
            _config.coreBasicItem.logEnabled = logEnabled;
            _config.coreBasicItem.loglevel = loglevel;
            _config.coreBasicItem.muxEnabled = muxEnabled;
            _config.coreBasicItem.defAllowInsecure = defAllowInsecure;
            _config.coreBasicItem.defFingerprint = defFingerprint;
            _config.coreBasicItem.defUserAgent = defUserAgent;

            //DNS
            _config.remoteDNS = remoteDNS;
            _config.domainStrategy4Freedom = domainStrategy4Freedom;

            //Kcp
            //_config.kcpItem.mtu = Kcpmtu;
            //_config.kcpItem.tti = Kcptti;
            //_config.kcpItem.uplinkCapacity = KcpuplinkCapacity;
            //_config.kcpItem.downlinkCapacity = KcpdownlinkCapacity;
            //_config.kcpItem.readBufferSize = KcpreadBufferSize;
            //_config.kcpItem.writeBufferSize = KcpwriteBufferSize;
            //_config.kcpItem.congestion = Kcpcongestion;

            //UI
            Utils.SetAutoRun(AutoRun);
            _config.guiItem.autoRun = AutoRun;
            _config.guiItem.enableStatistics = EnableStatistics;
            _config.guiItem.statisticsFreshRate = StatisticsFreshRate;
            if (_config.guiItem.statisticsFreshRate > 100 || _config.guiItem.statisticsFreshRate < 1)
            {
                _config.guiItem.statisticsFreshRate = 1;
            }
            _config.guiItem.keepOlderDedupl = KeepOlderDedupl;
            _config.guiItem.ignoreGeoUpdateCore = IgnoreGeoUpdateCore;
            _config.uiItem.enableAutoAdjustMainLvColWidth = EnableAutoAdjustMainLvColWidth;
            _config.guiItem.enableSecurityProtocolTls13 = EnableSecurityProtocolTls13;
            _config.uiItem.autoHideStartup = AutoHideStartup;
            _config.guiItem.autoUpdateInterval = autoUpdateInterval;
            _config.guiItem.checkPreReleaseUpdate = EnableCheckPreReleaseUpdate;
            _config.uiItem.enableDragDropSort = EnableDragDropSort;
            _config.uiItem.doubleClick2Activate = DoubleClick2Activate;
            _config.guiItem.trayMenuServersLimit = trayMenuServersLimit;
            _config.uiItem.currentFontFamily = currentFontFamily;
            _config.speedTestItem.speedTestTimeout = SpeedTestTimeout;
            _config.speedTestItem.speedTestUrl = SpeedTestUrl;
            _config.guiItem.enableHWA = EnableHWA;
            _config.constItem.subConvertUrl = SubConvertUrl;

            //systemProxy
            _config.systemProxyExceptions = systemProxyExceptions;
            _config.systemProxyAdvancedProtocol = systemProxyAdvancedProtocol;

            //tun mode
            _config.tunModeItem.showWindow = TunShowWindow;
            _config.tunModeItem.enabledLog = TunEnabledLog;
            _config.tunModeItem.strictRoute = TunStrictRoute;
            _config.tunModeItem.stack = TunStack;
            _config.tunModeItem.mtu = TunMtu;
            _config.tunModeItem.customTemplate = TunCustomTemplate;
            _config.tunModeItem.bypassMode = TunBypassMode;
            _config.tunModeItem.directIP = Utils.String2List(TunDirectIP);
            _config.tunModeItem.directProcess = Utils.String2List(TunDirectProcess);
            _config.tunModeItem.directDNS = Utils.ToJson(Utils.ParseJson(TunDirectDNS));
            _config.tunModeItem.proxyIP = Utils.String2List(TunProxyIP);
            _config.tunModeItem.proxyProcess = Utils.String2List(TunProxyProcess);
            _config.tunModeItem.proxyDNS = Utils.ToJson(Utils.ParseJson(TunProxyDNS));

            //coreType
            SaveCoreType();

            if (ConfigHandler.SaveConfig(ref _config) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                _view.DialogResult = true;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        private int SaveCoreType()
        {
            for (int k = 1; k <= _config.coreTypeItem.Count; k++)
            {
                var item = _config.coreTypeItem[k - 1];
                var type = string.Empty;
                switch ((int)item.configType)
                {
                    case 1:
                        type = CoreType1;
                        break;

                    case 2:
                        type = CoreType2;
                        break;

                    case 3:
                        type = CoreType3;
                        break;

                    case 4:
                        type = CoreType4;
                        break;

                    case 5:
                        type = CoreType5;
                        break;

                    case 6:
                        type = CoreType6;
                        break;
                }
                item.coreType = (ECoreType)Enum.Parse(typeof(ECoreType), type);
            }
            return 0;
        }
    }
}