namespace ServiceLib.Handler;

public sealed class AppHandler
{
    #region Property

    private static readonly Lazy<AppHandler> _instance = new(() => new());
    private Config _config;
    private int? _statePort;
    private int? _statePort2;
    private Job? _processJob;
    private bool? _isAdministrator;
    public static AppHandler Instance => _instance.Value;
    public Config Config => _config;

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

    #endregion Property

    #region Init

    public bool InitApp()
    {
        if (Utils.HasWritePermission() == false)
        {
            Environment.SetEnvironmentVariable(Global.LocalAppData, "1", EnvironmentVariableTarget.Process);
        }

        Logging.Setup();
        var config = ConfigHandler.LoadConfig();
        if (config == null)
        {
            return false;
        }
        _config = config;
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
        return true;
    }

    public bool InitComponents()
    {
        Logging.SaveLog($"v2rayN start up | {Utils.GetRuntimeInfo()}");
        Logging.LoggingEnabled(_config.GuiItem.EnableLog);

        //First determine the port value
        _ = StatePort;
        _ = StatePort2;

        return true;
    }

    public bool Reset()
    {
        _statePort = null;
        _statePort2 = null;
        return true;
    }

    #endregion Init

    #region Config

    public int GetLocalPort(EInboundProtocol protocol)
    {
        var localPort = _config.Inbound.FirstOrDefault(t => t.Protocol == nameof(EInboundProtocol.socks))?.LocalPort ?? 10808;
        return localPort + (int)protocol;
    }

    public void AddProcess(IntPtr processHandle)
    {
        if (Utils.IsWindows())
        {
            _processJob ??= new();
            try
            {
                _processJob?.AddProcess(processHandle);
            }
            catch
            {
            }
        }
    }

    #endregion Config

    #region SqliteHelper

    public async Task<List<SubItem>?> SubItems()
    {
        return await SQLiteHelper.Instance.TableAsync<SubItem>().OrderBy(t => t.Sort).ToListAsync();
    }

    public async Task<SubItem?> GetSubItem(string? subid)
    {
        return await SQLiteHelper.Instance.TableAsync<SubItem>().FirstOrDefaultAsync(t => t.Id == subid);
    }

    public async Task<List<ProfileItem>?> ProfileItems(string subid)
    {
        if (subid.IsNullOrEmpty())
        {
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>().ToListAsync();
        }
        else
        {
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.Subid == subid).ToListAsync();
        }
    }

    public async Task<List<string>?> ProfileItemIndexes(string subid)
    {
        return (await ProfileItems(subid))?.Select(t => t.IndexId)?.ToList();
    }

    public async Task<List<ProfileItemModel>?> ProfileItems(string subid, string filter)
    {
        var sql = @$"select a.*
                           ,b.remarks subRemarks
                        from ProfileItem a
                        left join SubItem b on a.subid = b.id
                        where 1=1 ";
        if (subid.IsNotEmpty())
        {
            sql += $" and a.subid = '{subid}'";
        }
        if (filter.IsNotEmpty())
        {
            if (filter.Contains('\''))
            {
                filter = filter.Replace("'", "");
            }
            sql += string.Format(" and (a.remarks like '%{0}%' or a.address like '%{0}%') ", filter);
        }

        return await SQLiteHelper.Instance.QueryAsync<ProfileItemModel>(sql);
    }

    public async Task<ProfileItem?> GetProfileItem(string indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return null;
        }
        return await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(it => it.IndexId == indexId);
    }

    public async Task<ProfileItem?> GetProfileItemViaRemarks(string? remarks)
    {
        if (remarks.IsNullOrEmpty())
        {
            return null;
        }
        return await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(it => it.Remarks == remarks);
    }

    public async Task<List<RoutingItem>?> RoutingItems()
    {
        return await SQLiteHelper.Instance.TableAsync<RoutingItem>().OrderBy(t => t.Sort).ToListAsync();
    }

    public async Task<RoutingItem?> GetRoutingItem(string id)
    {
        return await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync(it => it.Id == id);
    }

    public async Task<List<DNSItem>?> DNSItems()
    {
        return await SQLiteHelper.Instance.TableAsync<DNSItem>().ToListAsync();
    }

    public async Task<DNSItem?> GetDNSItem(ECoreType eCoreType)
    {
        return await SQLiteHelper.Instance.TableAsync<DNSItem>().FirstOrDefaultAsync(it => it.CoreType == eCoreType);
    }

    #endregion SqliteHelper

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

    public ECoreType GetCoreType(ProfileItem profileItem, EConfigType eConfigType)
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
