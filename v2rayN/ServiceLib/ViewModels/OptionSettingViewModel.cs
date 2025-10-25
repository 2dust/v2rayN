namespace ServiceLib.ViewModels;

public partial class OptionSettingViewModel : MyReactiveObject
{
    #region Core

    [Reactive] private int _localPort;
    [Reactive] private bool _secondLocalPortEnabled;
    [Reactive] private bool _udpEnabled;
    [Reactive] private bool _sniffingEnabled;
    public IList<string> DestOverride { get; set; }
    [Reactive] private bool _routeOnly;
    [Reactive] private bool _allowLANConn;
    [Reactive] private bool _newPort4LAN;
    [Reactive] private string _user;
    [Reactive] private string _pass;
    [Reactive] private bool _muxEnabled;
    [Reactive] private bool _logEnabled;
    [Reactive] private string _logLevel;
    [Reactive] private bool _defAllowInsecure;
    [Reactive] private string _defFingerprint;
    [Reactive] private string _defUserAgent;
    [Reactive] private string _mux4SboxProtocol;
    [Reactive] private bool _enableCacheFile4Sbox;
    [Reactive] private int _hyUpMbps;
    [Reactive] private int _hyDownMbps;
    [Reactive] private bool _enableFragment;

    #endregion Core

    #region Core KCP

    //[Reactive] private int _kcpmtu;
    //[Reactive] private int _kcptti;
    //[Reactive] private int _kcpuplinkCapacity;
    //[Reactive] private int _kcpdownlinkCapacity;
    //[Reactive] private int _kcpreadBufferSize;
    //[Reactive] private int _kcpwriteBufferSize;
    //[Reactive] private bool _kcpcongestion;

    #endregion Core KCP

    #region UI

    [Reactive] private bool _autoRun;
    [Reactive] private bool _enableStatistics;
    [Reactive] private bool _keepOlderDedupl;
    [Reactive] private bool _displayRealTimeSpeed;
    [Reactive] private bool _enableAutoAdjustMainLvColWidth;
    [Reactive] private bool _enableUpdateSubOnlyRemarksExist;
    [Reactive] private bool _autoHideStartup;
    [Reactive] private bool _hide2TrayWhenClose;
    [Reactive] private bool _enableDragDropSort;
    [Reactive] private bool _doubleClick2Activate;
    [Reactive] private int _autoUpdateInterval;
    [Reactive] private int _trayMenuServersLimit;
    [Reactive] private string _currentFontFamily;
    [Reactive] private int _speedTestTimeout;
    [Reactive] private string _speedTestUrl;
    [Reactive] private string _speedPingTestUrl;
    [Reactive] private int _mixedConcurrencyCount;
    [Reactive] private bool _enableHWA;
    [Reactive] private string _subConvertUrl;
    [Reactive] private int _mainGirdOrientation;
    [Reactive] private string _geoFileSourceUrl;
    [Reactive] private string _srsFileSourceUrl;
    [Reactive] private string _routingRulesSourceUrl;
    [Reactive] private string _ipAPIUrl;

    #endregion UI

    #region System proxy

    [Reactive] private bool _notProxyLocalAddress;
    [Reactive] private string _systemProxyAdvancedProtocol;
    [Reactive] private string _systemProxyExceptions;

    #endregion System proxy

    #region Tun mode

    [Reactive] private bool _tunAutoRoute;
    [Reactive] private bool _tunStrictRoute;
    [Reactive] private string _tunStack;
    [Reactive] private int _tunMtu;
    [Reactive] private bool _tunEnableExInbound;
    [Reactive] private bool _tunEnableIPv6Address;

    #endregion Tun mode

    #region CoreType

    [Reactive] private string _coreType1;
    [Reactive] private string _coreType2;
    [Reactive] private string _coreType3;
    [Reactive] private string _coreType4;
    [Reactive] private string _coreType5;
    [Reactive] private string _coreType6;
    [Reactive] private string _coreType9;

    #endregion CoreType

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public OptionSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSettingAsync();
        });

        _ = Init();
    }

    private async Task Init()
    {
        await _updateView?.Invoke(EViewAction.InitSettingFont, null);

        #region Core

        var inbound = _config.Inbound.First();
        LocalPort = inbound.LocalPort;
        SecondLocalPortEnabled = inbound.SecondLocalPortEnabled;
        UdpEnabled = inbound.UdpEnabled;
        SniffingEnabled = inbound.SniffingEnabled;
        RouteOnly = inbound.RouteOnly;
        AllowLANConn = inbound.AllowLANConn;
        NewPort4LAN = inbound.NewPort4LAN;
        User = inbound.User;
        Pass = inbound.Pass;
        MuxEnabled = _config.CoreBasicItem.MuxEnabled;
        LogEnabled = _config.CoreBasicItem.LogEnabled;
        LogLevel = _config.CoreBasicItem.Loglevel;
        DefAllowInsecure = _config.CoreBasicItem.DefAllowInsecure;
        DefFingerprint = _config.CoreBasicItem.DefFingerprint;
        DefUserAgent = _config.CoreBasicItem.DefUserAgent;
        Mux4SboxProtocol = _config.Mux4SboxItem.Protocol;
        EnableCacheFile4Sbox = _config.CoreBasicItem.EnableCacheFile4Sbox;
        HyUpMbps = _config.HysteriaItem.UpMbps;
        HyDownMbps = _config.HysteriaItem.DownMbps;
        EnableFragment = _config.CoreBasicItem.EnableFragment;

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
        DisplayRealTimeSpeed = _config.GuiItem.DisplayRealTimeSpeed;
        KeepOlderDedupl = _config.GuiItem.KeepOlderDedupl;
        EnableAutoAdjustMainLvColWidth = _config.UiItem.EnableAutoAdjustMainLvColWidth;
        EnableUpdateSubOnlyRemarksExist = _config.UiItem.EnableUpdateSubOnlyRemarksExist;
        AutoHideStartup = _config.UiItem.AutoHideStartup;
        Hide2TrayWhenClose = _config.UiItem.Hide2TrayWhenClose;
        EnableDragDropSort = _config.UiItem.EnableDragDropSort;
        DoubleClick2Activate = _config.UiItem.DoubleClick2Activate;
        AutoUpdateInterval = _config.GuiItem.AutoUpdateInterval;
        TrayMenuServersLimit = _config.GuiItem.TrayMenuServersLimit;
        CurrentFontFamily = _config.UiItem.CurrentFontFamily;
        SpeedTestTimeout = _config.SpeedTestItem.SpeedTestTimeout;
        SpeedTestUrl = _config.SpeedTestItem.SpeedTestUrl;
        MixedConcurrencyCount = _config.SpeedTestItem.MixedConcurrencyCount;
        SpeedPingTestUrl = _config.SpeedTestItem.SpeedPingTestUrl;
        EnableHWA = _config.GuiItem.EnableHWA;
        SubConvertUrl = _config.ConstItem.SubConvertUrl;
        MainGirdOrientation = (int)_config.UiItem.MainGirdOrientation;
        GeoFileSourceUrl = _config.ConstItem.GeoSourceUrl;
        SrsFileSourceUrl = _config.ConstItem.SrsSourceUrl;
        RoutingRulesSourceUrl = _config.ConstItem.RouteRulesTemplateSourceUrl;
        IpAPIUrl = _config.SpeedTestItem.IPAPIUrl;

        #endregion UI

        #region System proxy

        NotProxyLocalAddress = _config.SystemProxyItem.NotProxyLocalAddress;
        SystemProxyAdvancedProtocol = _config.SystemProxyItem.SystemProxyAdvancedProtocol;
        SystemProxyExceptions = _config.SystemProxyItem.SystemProxyExceptions;

        #endregion System proxy

        #region Tun mode

        TunAutoRoute = _config.TunModeItem.AutoRoute;
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

                case 9:
                    CoreType9 = type;
                    break;
            }
        });
        await Task.CompletedTask;
    }

    private async Task SaveSettingAsync()
    {
        if (LocalPort.ToString().IsNullOrEmpty() || !Utils.IsNumeric(LocalPort.ToString())
           || LocalPort <= 0 || LocalPort >= Global.MaxPort)
        {
            NoticeManager.Instance.Enqueue(ResUI.FillLocalListeningPort);
            return;
        }
        var needReboot = (EnableStatistics != _config.GuiItem.EnableStatistics
                          || DisplayRealTimeSpeed != _config.GuiItem.DisplayRealTimeSpeed
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
        _config.Inbound.First().LocalPort = LocalPort;
        _config.Inbound.First().SecondLocalPortEnabled = SecondLocalPortEnabled;
        _config.Inbound.First().UdpEnabled = UdpEnabled;
        _config.Inbound.First().SniffingEnabled = SniffingEnabled;
        _config.Inbound.First().DestOverride = DestOverride?.ToList();
        _config.Inbound.First().RouteOnly = RouteOnly;
        _config.Inbound.First().AllowLANConn = AllowLANConn;
        _config.Inbound.First().NewPort4LAN = NewPort4LAN;
        _config.Inbound.First().User = User;
        _config.Inbound.First().Pass = Pass;
        if (_config.Inbound.Count > 1)
        {
            _config.Inbound.RemoveAt(1);
        }
        _config.CoreBasicItem.LogEnabled = LogEnabled;
        _config.CoreBasicItem.Loglevel = LogLevel;
        _config.CoreBasicItem.MuxEnabled = MuxEnabled;
        _config.CoreBasicItem.DefAllowInsecure = DefAllowInsecure;
        _config.CoreBasicItem.DefFingerprint = DefFingerprint;
        _config.CoreBasicItem.DefUserAgent = DefUserAgent;
        _config.Mux4SboxItem.Protocol = Mux4SboxProtocol;
        _config.CoreBasicItem.EnableCacheFile4Sbox = EnableCacheFile4Sbox;
        _config.HysteriaItem.UpMbps = HyUpMbps;
        _config.HysteriaItem.DownMbps = HyDownMbps;
        _config.CoreBasicItem.EnableFragment = EnableFragment;

        _config.GuiItem.AutoRun = AutoRun;
        _config.GuiItem.EnableStatistics = EnableStatistics;
        _config.GuiItem.DisplayRealTimeSpeed = DisplayRealTimeSpeed;
        _config.GuiItem.KeepOlderDedupl = KeepOlderDedupl;
        _config.UiItem.EnableAutoAdjustMainLvColWidth = EnableAutoAdjustMainLvColWidth;
        _config.UiItem.EnableUpdateSubOnlyRemarksExist = EnableUpdateSubOnlyRemarksExist;
        _config.UiItem.AutoHideStartup = AutoHideStartup;
        _config.UiItem.Hide2TrayWhenClose = Hide2TrayWhenClose;
        _config.GuiItem.AutoUpdateInterval = AutoUpdateInterval;
        _config.UiItem.EnableDragDropSort = EnableDragDropSort;
        _config.UiItem.DoubleClick2Activate = DoubleClick2Activate;
        _config.GuiItem.TrayMenuServersLimit = TrayMenuServersLimit;
        _config.UiItem.CurrentFontFamily = CurrentFontFamily;
        _config.SpeedTestItem.SpeedTestTimeout = SpeedTestTimeout;
        _config.SpeedTestItem.MixedConcurrencyCount = MixedConcurrencyCount;
        _config.SpeedTestItem.SpeedTestUrl = SpeedTestUrl;
        _config.SpeedTestItem.SpeedPingTestUrl = SpeedPingTestUrl;
        _config.GuiItem.EnableHWA = EnableHWA;
        _config.ConstItem.SubConvertUrl = SubConvertUrl;
        _config.UiItem.MainGirdOrientation = (EGirdOrientation)MainGirdOrientation;
        _config.ConstItem.GeoSourceUrl = GeoFileSourceUrl;
        _config.ConstItem.SrsSourceUrl = SrsFileSourceUrl;
        _config.ConstItem.RouteRulesTemplateSourceUrl = RoutingRulesSourceUrl;
        _config.SpeedTestItem.IPAPIUrl = IpAPIUrl;

        //systemProxy
        _config.SystemProxyItem.SystemProxyExceptions = SystemProxyExceptions;
        _config.SystemProxyItem.NotProxyLocalAddress = NotProxyLocalAddress;
        _config.SystemProxyItem.SystemProxyAdvancedProtocol = SystemProxyAdvancedProtocol;

        //tun mode
        _config.TunModeItem.AutoRoute = TunAutoRoute;
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
            AppManager.Instance.Reset();

            NoticeManager.Instance.Enqueue(needReboot ? ResUI.NeedRebootTips : ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
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

                case 9:
                    type = CoreType9;
                    break;

                default:
                    continue;
            }
            item.CoreType = (ECoreType)Enum.Parse(typeof(ECoreType), type);
        }
        await Task.CompletedTask;
    }
}
