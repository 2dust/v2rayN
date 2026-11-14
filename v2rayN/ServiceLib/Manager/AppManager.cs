namespace ServiceLib.Manager;

public sealed class AppManager
{
    #region Property

    private static readonly Lazy<AppManager> _instance = new(() => new());
    private Config _config;
    private int? _statePort;
    private int? _statePort2;
    public static AppManager Instance => _instance.Value;
    public IWindowDialog WindowDialog { get; set; } = null!;

    public int StatePort
    {
        get
        {
            _statePort ??= Utils.GetFreePort(GetLocalPort(EInboundProtocol.api));
            return _statePort.Value;
        }
    }

    public int StatePort2
    {
        get
        {
            _statePort2 ??= Utils.GetFreePort(GetLocalPort(EInboundProtocol.api2));
            return _statePort2.Value + (_config.TunModeItem.EnableTun ? 1 : 0);
        }
    }

    public string LinuxSudoPwd { get; set; }

    public bool ShowInTaskbar { get; set; }

    public ECoreType RunningCoreType { get; set; }

    public bool IsRunningCore(ECoreType type)
    {
        switch (type)
        {
            case ECoreType.Xray when RunningCoreType is ECoreType.Xray or ECoreType.v2fly or ECoreType.v2fly_v5:
            case ECoreType.sing_box when RunningCoreType is ECoreType.sing_box or ECoreType.mihomo:
                return true;

            default:
                return false;
        }
    }

    public Dictionary<ECoreType, string> LastCheckUpdateResults { get; set; } = new();

    public void SetLastCheckUpdateResult(ECoreType coreType, string result)
    {
        LastCheckUpdateResults[coreType] = result;
    }

    #endregion Property

    #region App

    public bool InitApp()
    {
        if (Utils.HasWritePermission() == false)
        {
            Environment.SetEnvironmentVariable(Global.LocalAppData, "1", EnvironmentVariableTarget.Process);
        }

        Logging.Setup();
        _config = ConfigTextHandler.Init();
        if (_config == null)
        {
            return false;
        }
        Thread.CurrentThread.CurrentUICulture = new(_config.UiItem.CurrentLanguage);

        //Under Win10
        if (Utils.IsWindows() && Environment.OSVersion.Version.Major < 10)
        {
            Environment.SetEnvironmentVariable("DOTNET_EnableWriteXorExecute", "0", EnvironmentVariableTarget.User);
        }

        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        SQLiteHelper.Instance.CreateTable<ServerStatItem>();
        SQLiteHelper.Instance.CreateTable<RoutingItem>();
        SQLiteHelper.Instance.CreateTable<ProfileExItem>();
        SQLiteHelper.Instance.CreateTable<DNSItem>();
        SQLiteHelper.Instance.CreateTable<FullConfigTemplateItem>();
#pragma warning disable CS0618
        SQLiteHelper.Instance.CreateTable<ProfileGroupItem>();
#pragma warning restore CS0618
        return true;
    }

    public bool InitComponents()
    {
        Logging.SaveLog($"v2rayN start up | {Utils.GetRuntimeInfo()}");
        Logging.LoggingEnabled(_config.GuiItem.EnableLog);

        //First determine the port value
        _ = StatePort;
        _ = StatePort2;

        Task.Run(async () =>
        {
            await ConfigDbHandler.MigrateProfileExtra();
        }).Wait();

        return true;
    }

    public bool Reset()
    {
        _statePort = null;
        _statePort2 = null;
        return true;
    }

    public async Task AppExitAsync(bool needShutdown)
    {
        try
        {
            Logging.SaveLog("AppExitAsync Begin");

            await SysProxyHandler.UpdateSysProxy(_config, true);
            AppEvents.AppExitRequested.Publish();
            await Task.Delay(50); //Wait for AppExitRequested to be processed

            await ConfigTextHandler.SaveConfig();
            await ProfileExManager.Instance.SaveTo();
            await StatisticsManager.Instance.SaveTo();
            await CoreManager.Instance.CoreStop();
            StatisticsManager.Instance.Close();

            Logging.SaveLog("AppExitAsync End");
        }
        catch { }
        finally
        {
            if (needShutdown)
            {
                Shutdown(false);
            }
        }
    }

    public void Shutdown(bool byUser)
    {
        AppEvents.ShutdownRequested.Publish(byUser);
    }

    public async Task RebootAsAdmin()
    {
        ProcUtils.RebootAsAdmin();
        await AppManager.Instance.AppExitAsync(true);
    }

    #endregion App

    #region Config

    public int GetLocalPort(EInboundProtocol protocol)
    {
        var localPort = _config.Inbound.FirstOrDefault(t => t.Protocol == nameof(EInboundProtocol.socks))?.LocalPort ?? 10808;
        return localPort + (int)protocol;
    }

    #endregion Config

    #region Core Type

    public List<string> GetShadowsocksSecurities(ProfileItem profileItem)
    {
        var coreType = GetCoreType(profileItem, EConfigType.Shadowsocks);
        switch (coreType)
        {
            case ECoreType.v2fly:
                return Global.SsSecurities;

            case ECoreType.Xray:
                return Global.SsSecuritiesInXray;

            case ECoreType.sing_box:
                return Global.SsSecuritiesInSingbox;
        }
        return Global.SsSecuritiesInSingbox;
    }

    public ECoreType GetCoreType(ProfileItem? profileItem, EConfigType eConfigType)
    {
        if (profileItem?.CoreType != null)
        {
            return (ECoreType)profileItem.CoreType;
        }

        var item = _config.CoreTypeItem?.FirstOrDefault(it => it.ConfigType == eConfigType);
        return item?.CoreType ?? ECoreType.Xray;
    }

    #endregion Core Type
}
