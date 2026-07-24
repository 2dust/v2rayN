namespace ServiceLib.ViewModels;

public class OptionSettingViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    #region Core

    [Reactive] public int LocalPort { get; set; }
    [Reactive] public bool SecondLocalPortEnabled { get; set; }
    [Reactive] public bool UdpEnabled { get; set; }
    [Reactive] public bool SniffingEnabled { get; set; }
    public IList<string> DestOverride { get; set; }
    [Reactive] public bool RouteOnly { get; set; }
    [Reactive] public bool AllowLANConn { get; set; }
    [Reactive] public bool NewPort4LAN { get; set; }
    [Reactive] public string User { get; set; }
    [Reactive] public string Pass { get; set; }
    [Reactive] public bool LogEnabled { get; set; }
    [Reactive] public string Loglevel { get; set; }
    [Reactive] public string DefFingerprint { get; set; }
    [Reactive] public string DefUserAgent { get; set; }
    [Reactive] public string SendThrough { get; set; }
    [Reactive] public string BindInterface { get; set; }
    [Reactive] public string Mux4SboxProtocol { get; set; }
    [Reactive] public bool EnableCacheFile4Sbox { get; set; }
    [Reactive] public int? HyUpMbps { get; set; }
    [Reactive] public int? HyDownMbps { get; set; }
    [Reactive] public bool EnableFragment { get; set; }
    [Reactive] public bool EnableFinalFragment { get; set; }
    [Reactive] public string FragmentPackets { get; set; }
    [Reactive] public string FragmentLengths { get; set; }
    [Reactive] public string FragmentDelays { get; set; }
    [Reactive] public string FragmentMaxSplit { get; set; }

    #endregion Core

    #region UI

    [Reactive] public bool AutoRun { get; set; }
    [Reactive] public bool EnableStatistics { get; set; }
    [Reactive] public bool KeepOlderDedupl { get; set; }
    [Reactive] public bool DisplayRealTimeSpeed { get; set; }
    [Reactive] public bool EnableAutoAdjustMainLvColWidth { get; set; }
    [Reactive] public bool AutoHideStartup { get; set; }
    [Reactive] public bool Hide2TrayWhenClose { get; set; }
    [Reactive] public bool MacOSShowInDock { get; set; }
    [Reactive] public bool EnableDragDropSort { get; set; }
    [Reactive] public bool DoubleClick2Activate { get; set; }
    [Reactive] public int AutoUpdateInterval { get; set; }
    [Reactive] public int TrayMenuServersLimit { get; set; }
    [Reactive] public string CurrentFontFamily { get; set; }
    [Reactive] public int SpeedTestTimeout { get; set; }
    [Reactive] public string SpeedTestUrl { get; set; }
    [Reactive] public string SpeedPingTestUrl { get; set; }
    [Reactive] public string UdpTestTarget { get; set; }
    [Reactive] public int MixedConcurrencyCount { get; set; }
    [Reactive] public bool EnableHWA { get; set; }
    [Reactive] public string SubConvertUrl { get; set; }
    [Reactive] public int MainGirdOrientation { get; set; }
    [Reactive] public string GeoFileSourceUrl { get; set; }
    [Reactive] public string SrsFileSourceUrl { get; set; }
    [Reactive] public string RoutingRulesSourceUrl { get; set; }
    [Reactive] public string IPAPIUrl { get; set; }
    [Reactive] public string RootCertProvider { get; set; }

    #endregion UI

    #region UI visibility

    [Reactive] public bool BlIsWindows { get; set; }
    [Reactive] public bool BlIsLinux { get; set; }
    [Reactive] public bool BlIsIsMacOS { get; set; }
    [Reactive] public bool BlIsNonWindows { get; set; }

    #endregion UI visibility

    #region System proxy

    [Reactive] public bool NotProxyLocalAddress { get; set; }
    [Reactive] public string SystemProxyAdvancedProtocol { get; set; }
    [Reactive] public string SystemProxyExceptions { get; set; }
    [Reactive] public string CustomSystemProxyPacPath { get; set; }
    [Reactive] public string CustomSystemProxyScriptPath { get; set; }

    #endregion System proxy

    #region Tun mode

    [Reactive] public bool TunAutoRoute { get; set; }
    [Reactive] public bool TunStrictRoute { get; set; }
    [Reactive] public string TunStack { get; set; }
    [Reactive] public int TunMtu { get; set; }
    [Reactive] public bool TunEnableIPv6Address { get; set; }
    [Reactive] public string TunIcmpRouting { get; set; }
    [Reactive] public bool TunEnableLegacyProtect { get; set; }
    [Reactive] public string TunRouteExcludeAddress { get; set; }
    [Reactive] public string TunIpv4Address { get; set; }
    [Reactive] public string TunIpv6Address { get; set; }

    #endregion Tun mode

    #region CoreType

    [Reactive] public string CoreType1 { get; set; }
    [Reactive] public string CoreType2 { get; set; }
    [Reactive] public string CoreType3 { get; set; }
    [Reactive] public string CoreType4 { get; set; }
    [Reactive] public string CoreType5 { get; set; }
    [Reactive] public string CoreType6 { get; set; }
    [Reactive] public string CoreType7 { get; set; }
    [Reactive] public string CoreType9 { get; set; }

    #endregion CoreType

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public OptionSettingViewModel()
    {
        _config = AppManager.Instance.Config;
        BlIsWindows = Utils.IsWindows();
        BlIsLinux = Utils.IsLinux();
        BlIsIsMacOS = Utils.IsMacOS();
        BlIsNonWindows = Utils.IsNonWindows();

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSettingAsync();
        });

        _ = Init();
    }

    private async Task Init()
    {
        #region Core

        var inbound = _config.Inbound.First();
        LocalPort = inbound.LocalPort;
        SecondLocalPortEnabled = inbound.SecondLocalPortEnabled;
        UdpEnabled = inbound.UdpEnabled;
        SniffingEnabled = inbound.SniffingEnabled;
        DestOverride = inbound.DestOverride ?? [];
        RouteOnly = inbound.RouteOnly;
        AllowLANConn = inbound.AllowLANConn;
        NewPort4LAN = inbound.NewPort4LAN;
        User = inbound.User;
        Pass = inbound.Pass;
        LogEnabled = _config.CoreBasicItem.LogEnabled;
        Loglevel = _config.CoreBasicItem.Loglevel;
        DefFingerprint = _config.CoreBasicItem.DefFingerprint;
        DefUserAgent = _config.CoreBasicItem.DefUserAgent;
        SendThrough = _config.CoreBasicItem.SendThrough ?? string.Empty;
        BindInterface = _config.CoreBasicItem.BindInterface ?? string.Empty;
        Mux4SboxProtocol = _config.Mux4SboxItem.Protocol;
        EnableCacheFile4Sbox = _config.CoreBasicItem.EnableCacheFile4Sbox;
        HyUpMbps = _config.HysteriaItem.UpMbps;
        HyDownMbps = _config.HysteriaItem.DownMbps;
        EnableFragment = _config.CoreBasicItem.EnableFragment;
        EnableFinalFragment = _config.CoreBasicItem.EnableFinalFragment;
        FragmentPackets = _config.Fragment4RayItem?.Packets;
        FragmentLengths = Utils.List2String(_config.Fragment4RayItem?.Lengths);
        FragmentDelays = Utils.List2String(_config.Fragment4RayItem?.Delays);
        FragmentMaxSplit = _config.Fragment4RayItem?.MaxSplit;

        #endregion Core

        #region UI

        AutoRun = _config.GuiItem.AutoRun;
        EnableStatistics = _config.GuiItem.EnableStatistics;
        DisplayRealTimeSpeed = _config.GuiItem.DisplayRealTimeSpeed;
        KeepOlderDedupl = _config.GuiItem.KeepOlderDedupl;
        EnableAutoAdjustMainLvColWidth = _config.UiItem.EnableAutoAdjustMainLvColWidth;
        AutoHideStartup = _config.UiItem.AutoHideStartup;
        Hide2TrayWhenClose = _config.UiItem.Hide2TrayWhenClose;
        MacOSShowInDock = _config.UiItem.MacOSShowInDock;
        EnableDragDropSort = _config.UiItem.EnableDragDropSort;
        DoubleClick2Activate = _config.UiItem.DoubleClick2Activate;
        AutoUpdateInterval = _config.GuiItem.AutoUpdateInterval;
        TrayMenuServersLimit = _config.GuiItem.TrayMenuServersLimit;
        CurrentFontFamily = _config.UiItem.CurrentFontFamily;
        SpeedTestTimeout = _config.SpeedTestItem.SpeedTestTimeout;
        SpeedTestUrl = _config.SpeedTestItem.SpeedTestUrl;
        MixedConcurrencyCount = _config.SpeedTestItem.MixedConcurrencyCount;
        SpeedPingTestUrl = _config.SpeedTestItem.SpeedPingTestUrl;
        UdpTestTarget = _config.SpeedTestItem.UdpTestTarget;
        EnableHWA = _config.GuiItem.EnableHWA;
        SubConvertUrl = _config.ConstItem.SubConvertUrl;
        MainGirdOrientation = (int)_config.UiItem.MainGirdOrientation;
        GeoFileSourceUrl = _config.ConstItem.GeoSourceUrl;
        SrsFileSourceUrl = _config.ConstItem.SrsSourceUrl;
        RoutingRulesSourceUrl = _config.ConstItem.RouteRulesTemplateSourceUrl;
        IPAPIUrl = _config.SpeedTestItem.IPAPIUrl;
        RootCertProvider = _config.GuiItem.RootCertProvider;

        #endregion UI

        #region System proxy

        NotProxyLocalAddress = _config.SystemProxyItem.NotProxyLocalAddress;
        SystemProxyAdvancedProtocol = _config.SystemProxyItem.SystemProxyAdvancedProtocol;
        SystemProxyExceptions = _config.SystemProxyItem.SystemProxyExceptions;
        CustomSystemProxyPacPath = _config.SystemProxyItem.CustomSystemProxyPacPath;
        CustomSystemProxyScriptPath = _config.SystemProxyItem.CustomSystemProxyScriptPath;

        #endregion System proxy

        #region Tun mode

        TunAutoRoute = _config.TunModeItem.AutoRoute;
        TunStrictRoute = _config.TunModeItem.StrictRoute;
        TunStack = _config.TunModeItem.Stack;
        TunMtu = _config.TunModeItem.Mtu;
        TunEnableIPv6Address = _config.TunModeItem.EnableIPv6Address;
        TunIcmpRouting = _config.TunModeItem.IcmpRouting;
        TunEnableLegacyProtect = _config.TunModeItem.EnableLegacyProtect;
        TunRouteExcludeAddress = Utils.List2String(_config.TunModeItem.RouteExcludeAddress, true);
        TunIpv4Address = _config.TunModeItem.Ipv4Address;
        TunIpv6Address = _config.TunModeItem.Ipv6Address;

        #endregion Tun mode

        await InitCoreType();
    }

    private async Task InitCoreType()
    {
        _config.CoreTypeItem ??= [];

        foreach (var it in Enum.GetValues<EConfigType>())
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

                case 7:
                    CoreType7 = type;
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
        var fragmentLengths = Utils.String2List(FragmentLengths) ?? [];
        var fragmentDelays = Utils.String2List(FragmentDelays) ?? [];
        if (fragmentLengths.Any(item => !Utils.TryParseRange(item, 0, int.MaxValue, out _, out _))
            || fragmentDelays.Any(item => !Utils.TryParseRange(item, 0, int.MaxValue, out _, out _))
            || (FragmentMaxSplit.IsNotEmpty() && !Utils.TryParseMaxSplit(FragmentMaxSplit, 0, 10000, out _, out _)))
        {
            NoticeManager.Instance.Enqueue(ResUI.FillFragmentParameterError);
            return;
        }
        var needReboot = EnableStatistics != _config.GuiItem.EnableStatistics
                          || DisplayRealTimeSpeed != _config.GuiItem.DisplayRealTimeSpeed
                        || EnableDragDropSort != _config.UiItem.EnableDragDropSort
                        || EnableHWA != _config.GuiItem.EnableHWA
                        || CurrentFontFamily != _config.UiItem.CurrentFontFamily;

        //Core
        var inbound = _config.Inbound.First();
        inbound.LocalPort = LocalPort;
        inbound.SecondLocalPortEnabled = SecondLocalPortEnabled;
        inbound.UdpEnabled = UdpEnabled;
        inbound.SniffingEnabled = SniffingEnabled;
        inbound.DestOverride = DestOverride?.ToList();
        inbound.RouteOnly = RouteOnly;
        inbound.AllowLANConn = AllowLANConn;
        inbound.NewPort4LAN = NewPort4LAN;
        inbound.User = User;
        inbound.Pass = Pass;
        if (_config.Inbound.Count > 1)
        {
            _config.Inbound.RemoveAt(1);
        }
        _config.CoreBasicItem.LogEnabled = LogEnabled;
        _config.CoreBasicItem.Loglevel = Loglevel;
        _config.CoreBasicItem.DefFingerprint = DefFingerprint;
        _config.CoreBasicItem.DefUserAgent = DefUserAgent;
        _config.CoreBasicItem.SendThrough = SendThrough.TrimEx();
        _config.CoreBasicItem.BindInterface = BindInterface.TrimEx();
        _config.Mux4SboxItem.Protocol = Mux4SboxProtocol;
        _config.CoreBasicItem.EnableCacheFile4Sbox = EnableCacheFile4Sbox;
        _config.HysteriaItem.UpMbps = HyUpMbps ?? 0;
        _config.HysteriaItem.DownMbps = HyDownMbps ?? 0;
        _config.CoreBasicItem.EnableFragment = EnableFragment;
        _config.CoreBasicItem.EnableFinalFragment = EnableFinalFragment;
        _config.Fragment4RayItem ??= new();
        _config.Fragment4RayItem.Packets = FragmentPackets;
        _config.Fragment4RayItem.Lengths = fragmentLengths;
        _config.Fragment4RayItem.Delays = fragmentDelays;
        _config.Fragment4RayItem.MaxSplit = FragmentMaxSplit;

        _config.GuiItem.AutoRun = AutoRun;
        _config.GuiItem.EnableStatistics = EnableStatistics;
        _config.GuiItem.DisplayRealTimeSpeed = DisplayRealTimeSpeed;
        _config.GuiItem.KeepOlderDedupl = KeepOlderDedupl;
        _config.UiItem.EnableAutoAdjustMainLvColWidth = EnableAutoAdjustMainLvColWidth;
        _config.UiItem.AutoHideStartup = AutoHideStartup;
        _config.UiItem.Hide2TrayWhenClose = Hide2TrayWhenClose;
        _config.UiItem.MacOSShowInDock = MacOSShowInDock;
        _config.GuiItem.AutoUpdateInterval = AutoUpdateInterval;
        _config.UiItem.EnableDragDropSort = EnableDragDropSort;
        _config.UiItem.DoubleClick2Activate = DoubleClick2Activate;
        _config.GuiItem.TrayMenuServersLimit = TrayMenuServersLimit;
        _config.UiItem.CurrentFontFamily = CurrentFontFamily;
        _config.SpeedTestItem.SpeedTestTimeout = SpeedTestTimeout;
        _config.SpeedTestItem.MixedConcurrencyCount = MixedConcurrencyCount;
        _config.SpeedTestItem.SpeedTestUrl = SpeedTestUrl;
        _config.SpeedTestItem.SpeedPingTestUrl = SpeedPingTestUrl;
        _config.SpeedTestItem.UdpTestTarget = UdpTestTarget;
        _config.GuiItem.EnableHWA = EnableHWA;
        _config.ConstItem.SubConvertUrl = SubConvertUrl;
        _config.UiItem.MainGirdOrientation = (EGirdOrientation)MainGirdOrientation;
        _config.ConstItem.GeoSourceUrl = GeoFileSourceUrl;
        _config.ConstItem.SrsSourceUrl = SrsFileSourceUrl;
        _config.ConstItem.RouteRulesTemplateSourceUrl = RoutingRulesSourceUrl;
        _config.SpeedTestItem.IPAPIUrl = IPAPIUrl;
        _config.GuiItem.RootCertProvider = RootCertProvider;

        //systemProxy
        _config.SystemProxyItem.SystemProxyExceptions = SystemProxyExceptions;
        _config.SystemProxyItem.NotProxyLocalAddress = NotProxyLocalAddress;
        _config.SystemProxyItem.SystemProxyAdvancedProtocol = SystemProxyAdvancedProtocol;
        _config.SystemProxyItem.CustomSystemProxyPacPath = CustomSystemProxyPacPath;
        _config.SystemProxyItem.CustomSystemProxyScriptPath = CustomSystemProxyScriptPath;

        //tun mode
        _config.TunModeItem.AutoRoute = TunAutoRoute;
        _config.TunModeItem.StrictRoute = TunStrictRoute;
        _config.TunModeItem.Stack = TunStack;
        _config.TunModeItem.Mtu = TunMtu;
        _config.TunModeItem.EnableIPv6Address = TunEnableIPv6Address;
        _config.TunModeItem.IcmpRouting = TunIcmpRouting;
        _config.TunModeItem.EnableLegacyProtect = TunEnableLegacyProtect;
        _config.TunModeItem.RouteExcludeAddress = Utils.String2List(TunRouteExcludeAddress);
        _config.TunModeItem.Ipv4Address = TunIpv4Address;
        _config.TunModeItem.Ipv6Address = TunIpv6Address;

        //coreType
        await SaveCoreType();

        if (await ConfigHandler.SaveConfig(_config) == 0)
        {
            await AutoStartupHandler.UpdateTask(_config);
            AppManager.Instance.Reset();

            NoticeManager.Instance.Enqueue(needReboot ? ResUI.NeedRebootTips : ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private async Task SaveCoreType()
    {
        for (var k = 1; k <= _config.CoreTypeItem.Count; k++)
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

                case 7:
                    type = CoreType7;
                    break;

                case 9:
                    type = CoreType9;
                    break;

                default:
                    continue;
            }
            item.CoreType = Enum.Parse<ECoreType>(type);
        }
        await Task.CompletedTask;
    }
}
