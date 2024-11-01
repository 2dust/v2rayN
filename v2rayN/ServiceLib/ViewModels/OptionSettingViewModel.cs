using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class OptionSettingViewModel : MyReactiveObject
    {
        #region Core

        [Reactive] public int localPort { get; set; }
        [Reactive] public bool udpEnabled { get; set; }
        [Reactive] public bool sniffingEnabled { get; set; }
        public IList<string> destOverride { get; set; }
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
        [Reactive] public string mux4SboxProtocol { get; set; }
        [Reactive] public bool enableCacheFile4Sbox { get; set; }
        [Reactive] public int hyUpMbps { get; set; }
        [Reactive] public int hyDownMbps { get; set; }
        [Reactive] public bool enableFragment { get; set; }

        #endregion Core

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
        [Reactive] public bool KeepOlderDedupl { get; set; }
        [Reactive] public bool IgnoreGeoUpdateCore { get; set; }
        [Reactive] public bool EnableAutoAdjustMainLvColWidth { get; set; }
        [Reactive] public bool EnableUpdateSubOnlyRemarksExist { get; set; }
        [Reactive] public bool EnableSecurityProtocolTls13 { get; set; }
        [Reactive] public bool AutoHideStartup { get; set; }
        [Reactive] public bool EnableCheckPreReleaseUpdate { get; set; }
        [Reactive] public bool EnableDragDropSort { get; set; }
        [Reactive] public bool DoubleClick2Activate { get; set; }
        [Reactive] public int AutoUpdateInterval { get; set; }
        [Reactive] public int TrayMenuServersLimit { get; set; }
        [Reactive] public string CurrentFontFamily { get; set; }
        [Reactive] public int SpeedTestTimeout { get; set; }
        [Reactive] public string SpeedTestUrl { get; set; }
        [Reactive] public string SpeedPingTestUrl { get; set; }
        [Reactive] public bool EnableHWA { get; set; }
        [Reactive] public string SubConvertUrl { get; set; }
        [Reactive] public int MainGirdOrientation { get; set; }
        [Reactive] public string GeoFileSourceUrl { get; set; }
        [Reactive] public string SrsFileSourceUrl { get; set; }
        [Reactive] public string RoutingRulesSourceUrl { get; set; }

        #endregion UI

        #region System proxy

        [Reactive] public bool notProxyLocalAddress { get; set; }
        [Reactive] public string systemProxyAdvancedProtocol { get; set; }
        [Reactive] public string systemProxyExceptions { get; set; }

        #endregion System proxy

        #region Tun mode

        [Reactive] public bool TunStrictRoute { get; set; }
        [Reactive] public string TunStack { get; set; }
        [Reactive] public int TunMtu { get; set; }
        [Reactive] public bool TunEnableExInbound { get; set; }
        [Reactive] public bool TunEnableIPv6Address { get; set; }

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

        public OptionSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;

            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveSettingAsync();
            });

            Init();
        }

        private async Task Init()
        {
            await _updateView?.Invoke(EViewAction.InitSettingFont, null);

            #region Core

            var inbound = _config.Inbound[0];
            localPort = inbound.LocalPort;
            udpEnabled = inbound.UdpEnabled;
            sniffingEnabled = inbound.SniffingEnabled;
            routeOnly = inbound.RouteOnly;
            allowLANConn = inbound.AllowLANConn;
            newPort4LAN = inbound.NewPort4LAN;
            user = inbound.User;
            pass = inbound.Pass;
            muxEnabled = _config.CoreBasicItem.MuxEnabled;
            logEnabled = _config.CoreBasicItem.LogEnabled;
            loglevel = _config.CoreBasicItem.Loglevel;
            defAllowInsecure = _config.CoreBasicItem.DefAllowInsecure;
            defFingerprint = _config.CoreBasicItem.DefFingerprint;
            defUserAgent = _config.CoreBasicItem.DefUserAgent;
            mux4SboxProtocol = _config.Mux4SboxItem.Protocol;
            enableCacheFile4Sbox = _config.CoreBasicItem.EnableCacheFile4Sbox;
            hyUpMbps = _config.HysteriaItem.UpMbps;
            hyDownMbps = _config.HysteriaItem.DownMbps;
            enableFragment = _config.CoreBasicItem.EnableFragment;

            #endregion Core

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

            AutoRun = _config.GuiItem.AutoRun;
            EnableStatistics = _config.GuiItem.EnableStatistics;
            KeepOlderDedupl = _config.GuiItem.KeepOlderDedupl;
            IgnoreGeoUpdateCore = _config.GuiItem.IgnoreGeoUpdateCore;
            EnableAutoAdjustMainLvColWidth = _config.UiItem.EnableAutoAdjustMainLvColWidth;
            EnableUpdateSubOnlyRemarksExist = _config.UiItem.EnableUpdateSubOnlyRemarksExist;
            EnableSecurityProtocolTls13 = _config.GuiItem.EnableSecurityProtocolTls13;
            AutoHideStartup = _config.UiItem.AutoHideStartup;
            EnableCheckPreReleaseUpdate = _config.GuiItem.CheckPreReleaseUpdate;
            EnableDragDropSort = _config.UiItem.EnableDragDropSort;
            DoubleClick2Activate = _config.UiItem.DoubleClick2Activate;
            AutoUpdateInterval = _config.GuiItem.AutoUpdateInterval;
            TrayMenuServersLimit = _config.GuiItem.TrayMenuServersLimit;
            CurrentFontFamily = _config.UiItem.CurrentFontFamily;
            SpeedTestTimeout = _config.SpeedTestItem.SpeedTestTimeout;
            SpeedTestUrl = _config.SpeedTestItem.SpeedTestUrl;
            SpeedPingTestUrl = _config.SpeedTestItem.SpeedPingTestUrl;
            EnableHWA = _config.GuiItem.EnableHWA;
            SubConvertUrl = _config.ConstItem.SubConvertUrl;
            MainGirdOrientation = (int)_config.UiItem.MainGirdOrientation;
            GeoFileSourceUrl = _config.ConstItem.GeoSourceUrl;
            SrsFileSourceUrl = _config.ConstItem.SrsSourceUrl;
            RoutingRulesSourceUrl = _config.ConstItem.RouteRulesTemplateSourceUrl;

            #endregion UI

            #region System proxy

            notProxyLocalAddress = _config.SystemProxyItem.NotProxyLocalAddress;
            systemProxyAdvancedProtocol = _config.SystemProxyItem.SystemProxyAdvancedProtocol;
            systemProxyExceptions = _config.SystemProxyItem.SystemProxyExceptions;

            #endregion System proxy

            #region Tun mode

            TunStrictRoute = _config.TunModeItem.StrictRoute;
            TunStack = _config.TunModeItem.Stack;
            TunMtu = _config.TunModeItem.Mtu;
            TunEnableExInbound = _config.TunModeItem.EnableExInbound;
            TunEnableIPv6Address = _config.TunModeItem.EnableIPv6Address;

            #endregion Tun mode

            await InitCoreType();
        }

        private async Task InitCoreType()
        {
            if (_config.CoreTypeItem == null)
            {
                _config.CoreTypeItem = new List<CoreTypeItem>();
            }

            foreach (EConfigType it in Enum.GetValues(typeof(EConfigType)))
            {
                if (_config.CoreTypeItem.FindIndex(t => t.ConfigType == it) >= 0)
                {
                    continue;
                }

                _config.CoreTypeItem.Add(new CoreTypeItem()
                {
                    ConfigType = it,
                    CoreType = ECoreType.Xray
                });
            }
            _config.CoreTypeItem.ForEach(it =>
            {
                var type = it.CoreType.ToString();
                switch ((int)it.ConfigType)
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

        private async Task SaveSettingAsync()
        {
            if (Utils.IsNullOrEmpty(localPort.ToString()) || !Utils.IsNumeric(localPort.ToString())
               || localPort <= 0 || localPort >= Global.MaxPort)
            {
                NoticeHandler.Instance.Enqueue(ResUI.FillLocalListeningPort);
                return;
            }
            var needReboot = (EnableStatistics != _config.GuiItem.EnableStatistics
                            || EnableDragDropSort != _config.UiItem.EnableDragDropSort
                            || EnableHWA != _config.GuiItem.EnableHWA
                            || CurrentFontFamily != _config.UiItem.CurrentFontFamily
                            || MainGirdOrientation != (int)_config.UiItem.MainGirdOrientation);

            //if (Utile.IsNullOrEmpty(Kcpmtu.ToString()) || !Utile.IsNumeric(Kcpmtu.ToString())
            //       || Utile.IsNullOrEmpty(Kcptti.ToString()) || !Utile.IsNumeric(Kcptti.ToString())
            //       || Utile.IsNullOrEmpty(KcpuplinkCapacity.ToString()) || !Utile.IsNumeric(KcpuplinkCapacity.ToString())
            //       || Utile.IsNullOrEmpty(KcpdownlinkCapacity.ToString()) || !Utile.IsNumeric(KcpdownlinkCapacity.ToString())
            //       || Utile.IsNullOrEmpty(KcpreadBufferSize.ToString()) || !Utile.IsNumeric(KcpreadBufferSize.ToString())
            //       || Utile.IsNullOrEmpty(KcpwriteBufferSize.ToString()) || !Utile.IsNumeric(KcpwriteBufferSize.ToString()))
            //{
            //    NoticeHandler.Instance.Enqueue(ResUI.FillKcpParameters);
            //    return;
            //}

            //Core
            _config.Inbound[0].LocalPort = localPort;
            _config.Inbound[0].UdpEnabled = udpEnabled;
            _config.Inbound[0].SniffingEnabled = sniffingEnabled;
            _config.Inbound[0].DestOverride = destOverride?.ToList();
            _config.Inbound[0].RouteOnly = routeOnly;
            _config.Inbound[0].AllowLANConn = allowLANConn;
            _config.Inbound[0].NewPort4LAN = newPort4LAN;
            _config.Inbound[0].User = user;
            _config.Inbound[0].Pass = pass;
            if (_config.Inbound.Count > 1)
            {
                _config.Inbound.RemoveAt(1);
            }
            _config.CoreBasicItem.LogEnabled = logEnabled;
            _config.CoreBasicItem.Loglevel = loglevel;
            _config.CoreBasicItem.MuxEnabled = muxEnabled;
            _config.CoreBasicItem.DefAllowInsecure = defAllowInsecure;
            _config.CoreBasicItem.DefFingerprint = defFingerprint;
            _config.CoreBasicItem.DefUserAgent = defUserAgent;
            _config.Mux4SboxItem.Protocol = mux4SboxProtocol;
            _config.CoreBasicItem.EnableCacheFile4Sbox = enableCacheFile4Sbox;
            _config.HysteriaItem.UpMbps = hyUpMbps;
            _config.HysteriaItem.DownMbps = hyDownMbps;
            _config.CoreBasicItem.EnableFragment = enableFragment;

            _config.GuiItem.AutoRun = AutoRun;
            _config.GuiItem.EnableStatistics = EnableStatistics;
            _config.GuiItem.KeepOlderDedupl = KeepOlderDedupl;
            _config.GuiItem.IgnoreGeoUpdateCore = IgnoreGeoUpdateCore;
            _config.UiItem.EnableAutoAdjustMainLvColWidth = EnableAutoAdjustMainLvColWidth;
            _config.UiItem.EnableUpdateSubOnlyRemarksExist = EnableUpdateSubOnlyRemarksExist;
            _config.GuiItem.EnableSecurityProtocolTls13 = EnableSecurityProtocolTls13;
            _config.UiItem.AutoHideStartup = AutoHideStartup;
            _config.GuiItem.AutoUpdateInterval = AutoUpdateInterval;
            _config.GuiItem.CheckPreReleaseUpdate = EnableCheckPreReleaseUpdate;
            _config.UiItem.EnableDragDropSort = EnableDragDropSort;
            _config.UiItem.DoubleClick2Activate = DoubleClick2Activate;
            _config.GuiItem.TrayMenuServersLimit = TrayMenuServersLimit;
            _config.UiItem.CurrentFontFamily = CurrentFontFamily;
            _config.SpeedTestItem.SpeedTestTimeout = SpeedTestTimeout;
            _config.SpeedTestItem.SpeedTestUrl = SpeedTestUrl;
            _config.SpeedTestItem.SpeedPingTestUrl = SpeedPingTestUrl;
            _config.GuiItem.EnableHWA = EnableHWA;
            _config.ConstItem.SubConvertUrl = SubConvertUrl;
            _config.UiItem.MainGirdOrientation = (EGirdOrientation)MainGirdOrientation;
            _config.ConstItem.GeoSourceUrl = GeoFileSourceUrl;
            _config.ConstItem.SrsSourceUrl = SrsFileSourceUrl;
            _config.ConstItem.RouteRulesTemplateSourceUrl = RoutingRulesSourceUrl;

            //systemProxy
            _config.SystemProxyItem.SystemProxyExceptions = systemProxyExceptions;
            _config.SystemProxyItem.NotProxyLocalAddress = notProxyLocalAddress;
            _config.SystemProxyItem.SystemProxyAdvancedProtocol = systemProxyAdvancedProtocol;

            //tun mode
            _config.TunModeItem.StrictRoute = TunStrictRoute;
            _config.TunModeItem.Stack = TunStack;
            _config.TunModeItem.Mtu = TunMtu;
            _config.TunModeItem.EnableExInbound = TunEnableExInbound;
            _config.TunModeItem.EnableIPv6Address = TunEnableIPv6Address;

            //coreType
            await SaveCoreType();

            if (await ConfigHandler.SaveConfig(_config) == 0)
            {
                await AutoStartupHandler.UpdateTask(_config);

                NoticeHandler.Instance.Enqueue(needReboot ? ResUI.NeedRebootTips : ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }

        private async Task SaveCoreType()
        {
            for (int k = 1; k <= _config.CoreTypeItem.Count; k++)
            {
                var item = _config.CoreTypeItem[k - 1];
                var type = string.Empty;
                switch ((int)item.ConfigType)
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

                    default:
                        continue;
                }
                item.CoreType = (ECoreType)Enum.Parse(typeof(ECoreType), type);
            }
        }
    }
}