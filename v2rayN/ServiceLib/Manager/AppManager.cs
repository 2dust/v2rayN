namespace ServiceLib.Manager;

public sealed class AppManager
{
    #region Property

    private static readonly Lazy<AppManager> _instance = new(() => new());
    private Config _config;
    private int? _statePort;
    private int? _statePort2;
    public static AppManager Instance => _instance.Value;
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

    #endregion Property

    #region App

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
            await MigrateProfileExtra();
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

            await ConfigHandler.SaveConfig(_config);
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

    public async Task<List<ProfileItemModel>?> ProfileModels(string subid, string filter)
    {
        var sql = @$"select a.IndexId
                           ,a.ConfigType
                           ,a.Remarks
                           ,a.Address
                           ,a.Port
                           ,a.Network
                           ,a.StreamSecurity
                           ,a.Subid
                           ,b.remarks as subRemarks
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

    public async Task<List<ProfileItem>> GetProfileItemsByIndexIds(IEnumerable<string> indexIds)
    {
        var ids = indexIds.Where(id => !id.IsNullOrEmpty()).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }
        return await SQLiteHelper.Instance.TableAsync<ProfileItem>()
            .Where(it => ids.Contains(it.IndexId))
            .ToListAsync();
    }

    public async Task<Dictionary<string, ProfileItem>> GetProfileItemsByIndexIdsAsMap(IEnumerable<string> indexIds)
    {
        var items = await GetProfileItemsByIndexIds(indexIds);
        return items.ToDictionary(it => it.IndexId);
    }

    public async Task<List<ProfileItem>> GetProfileItemsOrderedByIndexIds(IEnumerable<string> indexIds)
    {
        var idList = indexIds.Where(id => !id.IsNullOrEmpty()).Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var items = await SQLiteHelper.Instance.TableAsync<ProfileItem>()
            .Where(it => idList.Contains(it.IndexId))
            .ToListAsync();
        var itemMap = items.ToDictionary(it => it.IndexId);

        return idList.Select(id => itemMap.GetValueOrDefault(id))
            .Where(item => item != null)
            .ToList();
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

    public async Task<List<FullConfigTemplateItem>?> FullConfigTemplateItem()
    {
        return await SQLiteHelper.Instance.TableAsync<FullConfigTemplateItem>().ToListAsync();
    }

    public async Task<FullConfigTemplateItem?> GetFullConfigTemplateItem(ECoreType eCoreType)
    {
        return await SQLiteHelper.Instance.TableAsync<FullConfigTemplateItem>().FirstOrDefaultAsync(it => it.CoreType == eCoreType);
    }

    public async Task MigrateProfileExtra()
    {
        await MigrateProfileExtraGroup();

#pragma warning disable CS0618

        const int pageSize = 100;
        var offset = 0;

        while (true)
        {
            var sql = $"SELECT * FROM ProfileItem " +
                $"WHERE ConfigVersion < 3 " +
                $"AND ConfigType NOT IN ({(int)EConfigType.PolicyGroup}, {(int)EConfigType.ProxyChain}) " +
                $"LIMIT {pageSize} OFFSET {offset}";
            var batch = await SQLiteHelper.Instance.QueryAsync<ProfileItem>(sql);
            if (batch is null || batch.Count == 0)
            {
                break;
            }

            var batchSuccessCount = await MigrateProfileExtraSub(batch);

            // Only increment offset by the number of failed items that remain in the result set
            // Successfully updated items are automatically excluded from future queries due to ConfigVersion = 3
            offset += batch.Count - batchSuccessCount;
        }

        //await ProfileGroupItemManager.Instance.ClearAll();
#pragma warning restore CS0618
    }

    private async Task<int> MigrateProfileExtraSub(List<ProfileItem> batch)
    {
        var updateProfileItems = new List<ProfileItem>();

        foreach (var item in batch)
        {
            try
            {
                var extra = item.GetProtocolExtra();
                switch (item.ConfigType)
                {
                    case EConfigType.Shadowsocks:
                        extra = extra with { SsMethod = item.Security.NullIfEmpty() };
                        break;

                    case EConfigType.VMess:
                        extra = extra with
                        {
                            AlterId = item.AlterId.ToString(),
                            VmessSecurity = item.Security.NullIfEmpty(),
                        };
                        break;

                    case EConfigType.VLESS:
                        extra = extra with
                        {
                            Flow = item.Flow.NullIfEmpty(),
                            VlessEncryption = item.Security,
                        };
                        break;

                    case EConfigType.Hysteria2:
                        extra = extra with
                        {
                            SalamanderPass = item.Path.NullIfEmpty(),
                            Ports = item.Ports.NullIfEmpty(),
                            UpMbps = _config.HysteriaItem.UpMbps,
                            DownMbps = _config.HysteriaItem.DownMbps,
                            HopInterval = _config.HysteriaItem.HopInterval.ToString(),
                        };
                        break;

                    case EConfigType.TUIC:
                        item.Username = item.Id;
                        item.Id = item.Security;
                        item.Password = item.Security;
                        break;

                    case EConfigType.HTTP:
                    case EConfigType.SOCKS:
                        item.Username = item.Security;
                        break;

                    case EConfigType.WireGuard:
                        extra = extra with
                        {
                            WgPublicKey = item.PublicKey.NullIfEmpty(),
                            WgInterfaceAddress = item.RequestHost.NullIfEmpty(),
                            WgReserved = item.Path.NullIfEmpty(),
                            WgMtu = int.TryParse(item.ShortId, out var mtu) ? mtu : 1280
                        };
                        break;
                }

                item.SetProtocolExtra(extra);

                item.Password = item.Id;

                item.ConfigVersion = 3;

                updateProfileItems.Add(item);
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"MigrateProfileExtra Error: {ex}");
            }
        }

        if (updateProfileItems.Count > 0)
        {
            try
            {
                var count = await SQLiteHelper.Instance.UpdateAllAsync(updateProfileItems);
                return count;
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"MigrateProfileExtraGroup update error: {ex}");
                return 0;
            }
        }
        else
        {
            return 0;
        }
    }

    private async Task<bool> MigrateProfileExtraGroup()
    {
#pragma warning disable CS0618
        var list = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().ToListAsync();
        var groupItems = new ConcurrentDictionary<string, ProfileGroupItem>(list.Where(t => !string.IsNullOrEmpty(t.IndexId)).ToDictionary(t => t.IndexId!));

        var sql = $"SELECT * FROM ProfileItem WHERE ConfigVersion < 3 AND ConfigType IN ({(int)EConfigType.PolicyGroup}, {(int)EConfigType.ProxyChain})";
        var items = await SQLiteHelper.Instance.QueryAsync<ProfileItem>(sql);

        if (items is null || items.Count == 0)
        {
            Logging.SaveLog("MigrateProfileExtraGroup: No items to migrate.");
            return true;
        }

        Logging.SaveLog($"MigrateProfileExtraGroup: Found {items.Count} group items to migrate.");

        var updateProfileItems = new List<ProfileItem>();

        foreach (var item in items)
        {
            try
            {
                var extra = item.GetProtocolExtra();

                extra = extra with { GroupType = nameof(item.ConfigType) };
                groupItems.TryGetValue(item.IndexId, out var groupItem);
                if (groupItem != null && !groupItem.NotHasChild())
                {
                    extra = extra with
                    {
                        ChildItems = groupItem.ChildItems,
                        SubChildItems = groupItem.SubChildItems,
                        Filter = groupItem.Filter,
                        MultipleLoad = groupItem.MultipleLoad,
                    };
                }

                item.SetProtocolExtra(extra);

                item.ConfigVersion = 3;
                updateProfileItems.Add(item);
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"MigrateProfileExtraGroup item error [{item.IndexId}]: {ex}");
            }
        }

        if (updateProfileItems.Count > 0)
        {
            try
            {
                var count = await SQLiteHelper.Instance.UpdateAllAsync(updateProfileItems);
                Logging.SaveLog($"MigrateProfileExtraGroup: Successfully updated {updateProfileItems.Count} items.");
                return updateProfileItems.Count == count;
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"MigrateProfileExtraGroup update error: {ex}");
                return false;
            }
        }

        return true;

        //await ProfileGroupItemManager.Instance.ClearAll();
#pragma warning restore CS0618
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
