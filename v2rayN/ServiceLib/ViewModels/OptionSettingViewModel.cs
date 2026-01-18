namespace ServiceLib.ViewModels;

public class OptionSettingViewModel : MyReactiveObject
{
    #region Core

    [Reactive] public int localPort { get; set; }
    [Reactive] public bool SecondLocalPortEnabled { get; set; }
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
    [Reactive] public bool DisplayRealTimeSpeed { get; set; }
    [Reactive] public bool EnableAutoAdjustMainLvColWidth { get; set; }
    [Reactive] public bool EnableUpdateSubOnlyRemarksExist { get; set; }
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
    [Reactive] public int MixedConcurrencyCount { get; set; }
    [Reactive] public bool EnableHWA { get; set; }
    [Reactive] public string SubConvertUrl { get; set; }
    [Reactive] public int MainGirdOrientation { get; set; }
    [Reactive] public string GeoFileSourceUrl { get; set; }
    [Reactive] public string SrsFileSourceUrl { get; set; }
    [Reactive] public string RoutingRulesSourceUrl { get; set; }
    [Reactive] public string IPAPIUrl { get; set; }

    #endregion UI

    #region UI visibility

    [Reactive] public bool BlIsWindows { get; set; }
    [Reactive] public bool BlIsLinux { get; set; }
    [Reactive] public bool BlIsIsMacOS { get; set; }
    [Reactive] public bool BlIsNonWindows { get; set; }

    #endregion UI visibility

    #region System proxy

    [Reactive] public bool notProxyLocalAddress { get; set; }
    [Reactive] public string systemProxyAdvancedProtocol { get; set; }
    [Reactive] public string systemProxyExceptions { get; set; }
    [Reactive] public string CustomSystemProxyPacPath { get; set; }
    [Reactive] public string CustomSystemProxyScriptPath { get; set; }

    #endregion System proxy

    #region Tun mode

    [Reactive] public bool TunAutoRoute { get; set; }
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
    [Reactive] public string CoreType7 { get; set; }
    [Reactive] public string CoreType9 { get; set; }

    #endregion CoreType

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public OptionSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        Config = AppManager.Instance.Config;
        UpdateView = updateView;
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
        await UpdateView?.Invoke(EViewAction.InitSettingFont, null);

        #region Core

        var inbound = Config.Inbound.First();
        localPort = inbound.LocalPort;
        SecondLocalPortEnabled = inbound.SecondLocalPortEnabled;
        udpEnabled = inbound.UdpEnabled;
        sniffingEnabled = inbound.SniffingEnabled;
        routeOnly = inbound.RouteOnly;
        allowLANConn = inbound.AllowLANConn;
        newPort4LAN = inbound.NewPort4LAN;
        user = inbound.User;
        pass = inbound.Pass;
        muxEnabled = Config.CoreBasicItem.MuxEnabled;
        logEnabled = Config.CoreBasicItem.LogEnabled;
        loglevel = Config.CoreBasicItem.Loglevel;
        defAllowInsecure = Config.CoreBasicItem.DefAllowInsecure;
        defFingerprint = Config.CoreBasicItem.DefFingerprint;
        defUserAgent = Config.CoreBasicItem.DefUserAgent;
        mux4SboxProtocol = Config.Mux4SboxItem.Protocol;
        enableCacheFile4Sbox = Config.CoreBasicItem.EnableCacheFile4Sbox;
        hyUpMbps = Config.HysteriaItem.UpMbps;
        hyDownMbps = Config.HysteriaItem.DownMbps;
        enableFragment = Config.CoreBasicItem.EnableFragment;

        #endregion Core

        #region Core KCP

        //Kcpmtu = Config.kcpItem.mtu;
        //Kcptti = Config.kcpItem.tti;
        //KcpuplinkCapacity = Config.kcpItem.uplinkCapacity;
        //KcpdownlinkCapacity = Config.kcpItem.downlinkCapacity;
        //KcpreadBufferSize = Config.kcpItem.readBufferSize;
        //KcpwriteBufferSize = Config.kcpItem.writeBufferSize;
        //Kcpcongestion = Config.kcpItem.congestion;

        #endregion Core KCP

        #region UI

        AutoRun = Config.GuiItem.AutoRun;
        EnableStatistics = Config.GuiItem.EnableStatistics;
        DisplayRealTimeSpeed = Config.GuiItem.DisplayRealTimeSpeed;
        KeepOlderDedupl = Config.GuiItem.KeepOlderDedupl;
        EnableAutoAdjustMainLvColWidth = Config.UiItem.EnableAutoAdjustMainLvColWidth;
        EnableUpdateSubOnlyRemarksExist = Config.UiItem.EnableUpdateSubOnlyRemarksExist;
        AutoHideStartup = Config.UiItem.AutoHideStartup;
        Hide2TrayWhenClose = Config.UiItem.Hide2TrayWhenClose;
        MacOSShowInDock = Config.UiItem.MacOSShowInDock;
        EnableDragDropSort = Config.UiItem.EnableDragDropSort;
        DoubleClick2Activate = Config.UiItem.DoubleClick2Activate;
        AutoUpdateInterval = Config.GuiItem.AutoUpdateInterval;
        TrayMenuServersLimit = Config.GuiItem.TrayMenuServersLimit;
        CurrentFontFamily = Config.UiItem.CurrentFontFamily;
        SpeedTestTimeout = Config.SpeedTestItem.SpeedTestTimeout;
        SpeedTestUrl = Config.SpeedTestItem.SpeedTestUrl;
        MixedConcurrencyCount = Config.SpeedTestItem.MixedConcurrencyCount;
        SpeedPingTestUrl = Config.SpeedTestItem.SpeedPingTestUrl;
        EnableHWA = Config.GuiItem.EnableHWA;
        SubConvertUrl = Config.ConstItem.SubConvertUrl;
        MainGirdOrientation = (int)Config.UiItem.MainGirdOrientation;
        GeoFileSourceUrl = Config.ConstItem.GeoSourceUrl;
        SrsFileSourceUrl = Config.ConstItem.SrsSourceUrl;
        RoutingRulesSourceUrl = Config.ConstItem.RouteRulesTemplateSourceUrl;
        IPAPIUrl = Config.SpeedTestItem.IPAPIUrl;

        #endregion UI

        #region System proxy

        notProxyLocalAddress = Config.SystemProxyItem.NotProxyLocalAddress;
        systemProxyAdvancedProtocol = Config.SystemProxyItem.SystemProxyAdvancedProtocol;
        systemProxyExceptions = Config.SystemProxyItem.SystemProxyExceptions;
        CustomSystemProxyPacPath = Config.SystemProxyItem.CustomSystemProxyPacPath;
        CustomSystemProxyScriptPath = Config.SystemProxyItem.CustomSystemProxyScriptPath;

        #endregion System proxy

        #region Tun mode

        TunAutoRoute = Config.TunModeItem.AutoRoute;
        TunStrictRoute = Config.TunModeItem.StrictRoute;
        TunStack = Config.TunModeItem.Stack;
        TunMtu = Config.TunModeItem.Mtu;
        TunEnableExInbound = Config.TunModeItem.EnableExInbound;
        TunEnableIPv6Address = Config.TunModeItem.EnableIPv6Address;

        #endregion Tun mode

        await InitCoreType();
    }

    private async Task InitCoreType()
    {
        if (Config.CoreTypeItem is null)
        {
            Config.CoreTypeItem = new List<CoreTypeItem>();
        }

        foreach (var it in Enum.GetValues<EConfigType>().Select(v => v))
        {
            if (Config.CoreTypeItem.FindIndex(t => t.ConfigType == it) >= 0)
            {
                continue;
            }

            Config.CoreTypeItem.Add(new CoreTypeItem()
            {
                ConfigType = it,
                CoreType = ECoreType.Xray
            });
        }
        Config.CoreTypeItem.ForEach(it =>
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
        if (localPort.ToString().IsNullOrEmpty() || !Utils.IsNumeric(localPort.ToString())
           || localPort <= 0 || localPort >= AppConfig.MaxPort)
        {
            NoticeManager.Instance.Enqueue(ResUI.FillLocalListeningPort);
            return;
        }
        var needReboot = EnableStatistics != Config.GuiItem.EnableStatistics
                          || DisplayRealTimeSpeed != Config.GuiItem.DisplayRealTimeSpeed
                        || EnableDragDropSort != Config.UiItem.EnableDragDropSort
                        || EnableHWA != Config.GuiItem.EnableHWA
                        || CurrentFontFamily != Config.UiItem.CurrentFontFamily
                        || MainGirdOrientation != (int)Config.UiItem.MainGirdOrientation;

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
        Config.Inbound.First().LocalPort = localPort;
        Config.Inbound.First().SecondLocalPortEnabled = SecondLocalPortEnabled;
        Config.Inbound.First().UdpEnabled = udpEnabled;
        Config.Inbound.First().SniffingEnabled = sniffingEnabled;
        Config.Inbound.First().DestOverride = destOverride?.ToList();
        Config.Inbound.First().RouteOnly = routeOnly;
        Config.Inbound.First().AllowLANConn = allowLANConn;
        Config.Inbound.First().NewPort4LAN = newPort4LAN;
        Config.Inbound.First().User = user;
        Config.Inbound.First().Pass = pass;
        if (Config.Inbound.Count > 1)
        {
            Config.Inbound.RemoveAt(1);
        }
        Config.CoreBasicItem.LogEnabled = logEnabled;
        Config.CoreBasicItem.Loglevel = loglevel;
        Config.CoreBasicItem.MuxEnabled = muxEnabled;
        Config.CoreBasicItem.DefAllowInsecure = defAllowInsecure;
        Config.CoreBasicItem.DefFingerprint = defFingerprint;
        Config.CoreBasicItem.DefUserAgent = defUserAgent;
        Config.Mux4SboxItem.Protocol = mux4SboxProtocol;
        Config.CoreBasicItem.EnableCacheFile4Sbox = enableCacheFile4Sbox;
        Config.HysteriaItem.UpMbps = hyUpMbps;
        Config.HysteriaItem.DownMbps = hyDownMbps;
        Config.CoreBasicItem.EnableFragment = enableFragment;

        Config.GuiItem.AutoRun = AutoRun;
        Config.GuiItem.EnableStatistics = EnableStatistics;
        Config.GuiItem.DisplayRealTimeSpeed = DisplayRealTimeSpeed;
        Config.GuiItem.KeepOlderDedupl = KeepOlderDedupl;
        Config.UiItem.EnableAutoAdjustMainLvColWidth = EnableAutoAdjustMainLvColWidth;
        Config.UiItem.EnableUpdateSubOnlyRemarksExist = EnableUpdateSubOnlyRemarksExist;
        Config.UiItem.AutoHideStartup = AutoHideStartup;
        Config.UiItem.Hide2TrayWhenClose = Hide2TrayWhenClose;
        Config.UiItem.MacOSShowInDock = MacOSShowInDock;
        Config.GuiItem.AutoUpdateInterval = AutoUpdateInterval;
        Config.UiItem.EnableDragDropSort = EnableDragDropSort;
        Config.UiItem.DoubleClick2Activate = DoubleClick2Activate;
        Config.GuiItem.TrayMenuServersLimit = TrayMenuServersLimit;
        Config.UiItem.CurrentFontFamily = CurrentFontFamily;
        Config.SpeedTestItem.SpeedTestTimeout = SpeedTestTimeout;
        Config.SpeedTestItem.MixedConcurrencyCount = MixedConcurrencyCount;
        Config.SpeedTestItem.SpeedTestUrl = SpeedTestUrl;
        Config.SpeedTestItem.SpeedPingTestUrl = SpeedPingTestUrl;
        Config.GuiItem.EnableHWA = EnableHWA;
        Config.ConstItem.SubConvertUrl = SubConvertUrl;
        Config.UiItem.MainGirdOrientation = (EGirdOrientation)MainGirdOrientation;
        Config.ConstItem.GeoSourceUrl = GeoFileSourceUrl;
        Config.ConstItem.SrsSourceUrl = SrsFileSourceUrl;
        Config.ConstItem.RouteRulesTemplateSourceUrl = RoutingRulesSourceUrl;
        Config.SpeedTestItem.IPAPIUrl = IPAPIUrl;

        //systemProxy
        Config.SystemProxyItem.SystemProxyExceptions = systemProxyExceptions;
        Config.SystemProxyItem.NotProxyLocalAddress = notProxyLocalAddress;
        Config.SystemProxyItem.SystemProxyAdvancedProtocol = systemProxyAdvancedProtocol;
        Config.SystemProxyItem.CustomSystemProxyPacPath = CustomSystemProxyPacPath;
        Config.SystemProxyItem.CustomSystemProxyScriptPath = CustomSystemProxyScriptPath;

        //tun mode
        Config.TunModeItem.AutoRoute = TunAutoRoute;
        Config.TunModeItem.StrictRoute = TunStrictRoute;
        Config.TunModeItem.Stack = TunStack;
        Config.TunModeItem.Mtu = TunMtu;
        Config.TunModeItem.EnableExInbound = TunEnableExInbound;
        Config.TunModeItem.EnableIPv6Address = TunEnableIPv6Address;

        //coreType
        await SaveCoreType();

        if (await ConfigHandler.SaveConfig(Config) == 0)
        {
            await AutoStartupHandler.UpdateTask(Config);
            AppManager.Instance.Reset();

            NoticeManager.Instance.Enqueue(needReboot ? ResUI.NeedRebootTips : ResUI.OperationSuccess);
            UpdateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private async Task SaveCoreType()
    {
        for (var k = 1; k <= Config.CoreTypeItem.Count; k++)
        {
            var item = Config.CoreTypeItem[k - 1];
            string type;
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
            item.CoreType = (ECoreType)Enum.Parse(typeof(ECoreType), type);
        }
        await Task.CompletedTask;
    }
}
