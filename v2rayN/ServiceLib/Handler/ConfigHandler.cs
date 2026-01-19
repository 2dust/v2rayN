using System.Data;

namespace ServiceLib.Handler;

public static class ConfigHandler
{
    private static readonly string _configRes = Global.ConfigFileName;
    private static readonly string _tag = "ConfigHandler";

    #region ConfigHandler

    /// <summary>
    /// Load the application configuration file
    /// If the file exists, deserialize it from JSON
    /// If not found, create a new Config object with default settings
    /// Initialize default values for missing configuration sections
    /// </summary>
    /// <returns>Config object containing application settings or null if there's an error</returns>
    public static Config? LoadConfig()
    {
        Config? config = null;
        var result = EmbedUtils.LoadResource(Utils.GetConfigPath(_configRes));
        if (result.IsNotEmpty())
        {
            config = JsonUtils.Deserialize<Config>(result);
        }
        else
        {
            if (File.Exists(Utils.GetConfigPath(_configRes)))
            {
                Logging.SaveLog("LoadConfig Exception");
                return null;
            }
        }

        config ??= new Config();

        config.CoreBasicItem ??= new()
        {
            LogEnabled = false,
            Loglevel = "warning",
            MuxEnabled = false,
        };

        if (config.Inbound == null)
        {
            config.Inbound = new List<InItem>();
            InItem inItem = new()
            {
                Protocol = EInboundProtocol.socks.ToString(),
                LocalPort = 10808,
                UdpEnabled = true,
                SniffingEnabled = true,
                RouteOnly = false,
            };

            config.Inbound.Add(inItem);
        }
        else
        {
            if (config.Inbound.Count > 0)
            {
                config.Inbound.First().Protocol = EInboundProtocol.socks.ToString();
            }
        }

        config.RoutingBasicItem ??= new();
        if (config.RoutingBasicItem.DomainStrategy.IsNullOrEmpty())
        {
            config.RoutingBasicItem.DomainStrategy = Global.DomainStrategies.First();
        }

        config.KcpItem ??= new KcpItem
        {
            Mtu = 1350,
            Tti = 50,
            UplinkCapacity = 12,
            DownlinkCapacity = 100,
            ReadBufferSize = 2,
            WriteBufferSize = 2,
            Congestion = false
        };
        config.GrpcItem ??= new GrpcItem
        {
            IdleTimeout = 60,
            HealthCheckTimeout = 20,
            PermitWithoutStream = false,
            InitialWindowsSize = 0,
        };
        config.TunModeItem ??= new TunModeItem
        {
            EnableTun = false,
            Mtu = 9000,
        };
        config.GuiItem ??= new();
        config.MsgUIItem ??= new();

        config.UiItem ??= new UIItem()
        {
            EnableUpdateSubOnlyRemarksExist = true
        };
        config.UiItem.MainColumnItem ??= new();
        config.UiItem.WindowSizeItem ??= new();

        if (config.UiItem.CurrentLanguage.IsNullOrEmpty())
        {
            config.UiItem.CurrentLanguage = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.CurrentCultureIgnoreCase)
                ? Global.Languages.First()
                : Global.Languages[2];
        }

        config.ConstItem ??= new ConstItem();

        config.SimpleDNSItem ??= InitBuiltinSimpleDNS();
        config.SimpleDNSItem.GlobalFakeIp ??= true;
        config.SimpleDNSItem.BootstrapDNS ??= Global.DomainPureIPDNSAddress.FirstOrDefault();
        config.SimpleDNSItem.ServeStale ??= false;
        config.SimpleDNSItem.ParallelQuery ??= false;

        config.SpeedTestItem ??= new();
        if (config.SpeedTestItem.SpeedTestTimeout < 10)
        {
            config.SpeedTestItem.SpeedTestTimeout = 10;
        }
        if (config.SpeedTestItem.SpeedTestUrl.IsNullOrEmpty())
        {
            config.SpeedTestItem.SpeedTestUrl = Global.SpeedTestUrls.First();
        }
        if (config.SpeedTestItem.SpeedPingTestUrl.IsNullOrEmpty())
        {
            config.SpeedTestItem.SpeedPingTestUrl = Global.SpeedPingTestUrls.First();
        }
        if (config.SpeedTestItem.MixedConcurrencyCount < 1)
        {
            config.SpeedTestItem.MixedConcurrencyCount = 5;
        }

        config.Mux4RayItem ??= new()
        {
            Concurrency = 8,
            XudpConcurrency = 16,
            XudpProxyUDP443 = "reject"
        };

        config.Mux4SboxItem ??= new()
        {
            Protocol = Global.SingboxMuxs.First(),
            MaxConnections = 8
        };

        config.HysteriaItem ??= new()
        {
            UpMbps = 100,
            DownMbps = 100
        };
        config.ClashUIItem ??= new();
        config.SystemProxyItem ??= new();
        config.WebDavItem ??= new();
        config.CheckUpdateItem ??= new();
        config.Fragment4RayItem ??= new()
        {
            Packets = "tlshello",
            Length = "100-200",
            Interval = "10-20"
        };
        config.GlobalHotkeys ??= new();

        if (config.SystemProxyItem.SystemProxyExceptions.IsNullOrEmpty())
        {
            config.SystemProxyItem.SystemProxyExceptions = Utils.IsWindows() ? Global.SystemProxyExceptionsWindows : Global.SystemProxyExceptionsLinux;
        }

        return config;
    }

    /// <summary>
    /// Save the configuration to a file
    /// First writes to a temporary file, then replaces the original file
    /// </summary>
    /// <param name="config">Configuration object to be saved</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> SaveConfig(Config config)
    {
        try
        {
            //save temp file
            var resPath = Utils.GetConfigPath(_configRes);
            var tempPath = $"{resPath}_temp";

            var content = JsonUtils.Serialize(config, true, true);
            if (content.IsNullOrEmpty())
            {
                return -1;
            }
            await File.WriteAllTextAsync(tempPath, content);

            //rename
            File.Move(tempPath, resPath, true);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }

        return 0;
    }

    #endregion ConfigHandler

    #region Server

    /// <summary>
    /// Add a server profile to the configuration
    /// Dispatches the request to the appropriate method based on the config type
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Server profile to add</param>
    /// <returns>Result of the operation (0 if successful, -1 if failed)</returns>
    public static async Task<int> AddServer(Config config, ProfileItem profileItem)
    {
        var item = await AppManager.Instance.GetProfileItem(profileItem.IndexId);
        if (item is null)
        {
            item = profileItem;
        }
        else
        {
            item.CoreType = profileItem.CoreType;
            item.Remarks = profileItem.Remarks;
            item.Address = profileItem.Address;
            item.Port = profileItem.Port;
            item.Ports = profileItem.Ports;

            item.Id = profileItem.Id;
            item.AlterId = profileItem.AlterId;
            item.Security = profileItem.Security;
            item.Flow = profileItem.Flow;

            item.Password = profileItem.Password;

            item.Network = profileItem.Network;
            item.HeaderType = profileItem.HeaderType;
            item.RequestHost = profileItem.RequestHost;
            item.Path = profileItem.Path;

            item.StreamSecurity = profileItem.StreamSecurity;
            item.Sni = profileItem.Sni;
            item.AllowInsecure = profileItem.AllowInsecure;
            item.Fingerprint = profileItem.Fingerprint;
            item.Alpn = profileItem.Alpn;

            item.PublicKey = profileItem.PublicKey;
            item.ShortId = profileItem.ShortId;
            item.SpiderX = profileItem.SpiderX;
            item.Mldsa65Verify = profileItem.Mldsa65Verify;
            item.Extra = profileItem.Extra;
            item.MuxEnabled = profileItem.MuxEnabled;
            item.Cert = profileItem.Cert;
            item.CertSha = profileItem.CertSha;
            item.EchConfigList = profileItem.EchConfigList;
            item.EchForceQuery = profileItem.EchForceQuery;
            item.ProtoExtra = profileItem.ProtoExtra;
        }

        var ret = item.ConfigType switch
        {
            EConfigType.VMess => await AddVMessServer(config, item),
            EConfigType.Shadowsocks => await AddShadowsocksServer(config, item),
            EConfigType.SOCKS => await AddSocksServer(config, item),
            EConfigType.HTTP => await AddHttpServer(config, item),
            EConfigType.Trojan => await AddTrojanServer(config, item),
            EConfigType.VLESS => await AddVlessServer(config, item),
            EConfigType.Hysteria2 => await AddHysteria2Server(config, item),
            EConfigType.TUIC => await AddTuicServer(config, item),
            EConfigType.WireGuard => await AddWireguardServer(config, item),
            EConfigType.Anytls => await AddAnytlsServer(config, item),
            _ => -1,
        };
        return ret;
    }

    /// <summary>
    /// Add or edit a VMess server
    /// Validates and processes VMess-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">VMess profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddVMessServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.VMess;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.Security = profileItem.Security.TrimEx();
        profileItem.Network = profileItem.Network.TrimEx();
        profileItem.HeaderType = profileItem.HeaderType.TrimEx();
        profileItem.RequestHost = profileItem.RequestHost.TrimEx();
        profileItem.Path = profileItem.Path.TrimEx();
        profileItem.StreamSecurity = profileItem.StreamSecurity.TrimEx();

        if (!Global.VmessSecurities.Contains(profileItem.Security))
        {
            return -1;
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Remove multiple servers from the configuration
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="indexes">List of server profiles to remove</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> RemoveServers(Config config, List<ProfileItem> indexes)
    {
        var subid = "TempRemoveSubId";
        foreach (var item in indexes)
        {
            item.Subid = subid;
        }

        await SQLiteHelper.Instance.UpdateAllAsync(indexes);
        await RemoveServersViaSubid(config, subid, false);

        return 0;
    }

    /// <summary>
    /// Clone server profiles
    /// Creates copies of the specified server profiles with "-clone" appended to the remarks
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="indexes">List of server profiles to clone</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> CopyServer(Config config, List<ProfileItem> indexes)
    {
        foreach (var it in indexes)
        {
            var item = await AppManager.Instance.GetProfileItem(it.IndexId);
            if (item is null)
            {
                continue;
            }

            var profileItem = JsonUtils.DeepCopy(item);
            profileItem.IndexId = string.Empty;
            profileItem.Remarks = $"{item.Remarks}-clone";

            if (profileItem.ConfigType == EConfigType.Custom)
            {
                profileItem.Address = Utils.GetConfigPath(profileItem.Address);
                if (await AddCustomServer(config, profileItem, false) == 0)
                {
                }
            }
            else
            {
                await AddServerCommon(config, profileItem, true);
            }
        }

        return 0;
    }

    /// <summary>
    /// Set the default server by its index ID
    /// Updates the configuration to use the specified server as default
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="indexId">Index ID of the server to set as default</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> SetDefaultServerIndex(Config config, string? indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return -1;
        }

        config.IndexId = indexId;

        await SaveConfig(config);

        return 0;
    }

    /// <summary>
    /// Set a default server from the provided list of profiles
    /// Ensures there's always a valid default server selected
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="lstProfile">List of profile models to choose from</param>
    /// <returns>Result of SetDefaultServerIndex operation</returns>
    public static async Task<int> SetDefaultServer(Config config, List<ProfileItemModel> lstProfile)
    {
        if (lstProfile.Exists(t => t.IndexId == config.IndexId))
        {
            return 0;
        }

        if (await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(t => t.IndexId == config.IndexId) != null)
        {
            return 0;
        }
        if (lstProfile.Count > 0)
        {
            return await SetDefaultServerIndex(config, lstProfile.FirstOrDefault(t => t.Port > 0)?.IndexId);
        }

        var item = await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(t => t.Port > 0);
        return await SetDefaultServerIndex(config, item?.IndexId);
    }

    /// <summary>
    /// Get the current default server profile
    /// If the current default is invalid, selects a new default
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <returns>The default profile item or null if none exists</returns>
    public static async Task<ProfileItem?> GetDefaultServer(Config config)
    {
        var item = await AppManager.Instance.GetProfileItem(config.IndexId);
        if (item is null)
        {
            var item2 = await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync();
            await SetDefaultServerIndex(config, item2?.IndexId);
            return item2;
        }

        return item;
    }

    /// <summary>
    /// Move a server in the list to a different position
    /// Supports moving to top, up, down, bottom or specific position
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="lstProfile">List of server profiles</param>
    /// <param name="index">Index of the server to move</param>
    /// <param name="eMove">Direction to move the server</param>
    /// <param name="pos">Target position when using EMove.Position</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> MoveServer(Config config, List<ProfileItem> lstProfile, int index, EMove eMove, int pos = -1)
    {
        var count = lstProfile.Count;
        if (index < 0 || index > lstProfile.Count - 1)
        {
            return -1;
        }

        for (var i = 0; i < lstProfile.Count; i++)
        {
            ProfileExManager.Instance.SetSort(lstProfile[i].IndexId, (i + 1) * 10);
        }

        var sort = 0;
        switch (eMove)
        {
            case EMove.Top:
                {
                    if (index == 0)
                    {
                        return 0;
                    }
                    sort = ProfileExManager.Instance.GetSort(lstProfile.First().IndexId) - 1;

                    break;
                }
            case EMove.Up:
                {
                    if (index == 0)
                    {
                        return 0;
                    }
                    sort = ProfileExManager.Instance.GetSort(lstProfile[index - 1].IndexId) - 1;

                    break;
                }

            case EMove.Down:
                {
                    if (index == count - 1)
                    {
                        return 0;
                    }
                    sort = ProfileExManager.Instance.GetSort(lstProfile[index + 1].IndexId) + 1;

                    break;
                }
            case EMove.Bottom:
                {
                    if (index == count - 1)
                    {
                        return 0;
                    }
                    sort = ProfileExManager.Instance.GetSort(lstProfile[^1].IndexId) + 1;

                    break;
                }
            case EMove.Position:
                sort = (pos * 10) + 1;
                break;
        }

        ProfileExManager.Instance.SetSort(lstProfile[index].IndexId, sort);
        return await Task.FromResult(0);
    }

    /// <summary>
    /// Add a custom server configuration from a file
    /// Copies the configuration file to the app's config directory
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Profile item with the file path in Address</param>
    /// <param name="blDelete">Whether to delete the source file after copying</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddCustomServer(Config config, ProfileItem profileItem, bool blDelete)
    {
        var fileName = profileItem.Address;
        if (!File.Exists(fileName))
        {
            return -1;
        }
        var ext = Path.GetExtension(fileName);
        var newFileName = $"{Utils.GetGuid()}{ext}";
        //newFileName = Path.Combine(Utile.GetTempPath(), newFileName);

        try
        {
            File.Copy(fileName, Utils.GetConfigPath(newFileName));
            if (blDelete)
            {
                File.Delete(fileName);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }

        profileItem.Address = newFileName;
        profileItem.ConfigType = EConfigType.Custom;
        if (profileItem.Remarks.IsNullOrEmpty())
        {
            profileItem.Remarks = $"import custom@{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}";
        }

        await AddServerCommon(config, profileItem, true);

        return 0;
    }

    /// <summary>
    /// Edit an existing custom server configuration
    /// Updates the server's properties without changing the file
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Profile item with updated properties</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> EditCustomServer(Config config, ProfileItem profileItem)
    {
        var item = await AppManager.Instance.GetProfileItem(profileItem.IndexId);
        if (item is null)
        {
            item = profileItem;
        }
        else
        {
            item.Remarks = profileItem.Remarks;
            item.Address = profileItem.Address;
            item.CoreType = profileItem.CoreType;
            item.DisplayLog = profileItem.DisplayLog;
            item.PreSocksPort = profileItem.PreSocksPort;
        }

        if (await SQLiteHelper.Instance.UpdateAsync(item) > 0)
        {
            return 0;
        }
        else
        {
            return -1;
        }

        //ToJsonFile(config);
    }

    /// <summary>
    /// Add or edit a Shadowsocks server
    /// Validates and processes Shadowsocks-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Shadowsocks profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddShadowsocksServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.Shadowsocks;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.Security = profileItem.Security.TrimEx();

        if (!AppManager.Instance.GetShadowsocksSecurities(profileItem).Contains(profileItem.Security))
        {
            return -1;
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit a SOCKS server
    /// Processes SOCKS-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">SOCKS profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddSocksServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.SOCKS;

        profileItem.Address = profileItem.Address.TrimEx();

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit an HTTP server
    /// Processes HTTP-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">HTTP profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddHttpServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.HTTP;

        profileItem.Address = profileItem.Address.TrimEx();

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit a Trojan server
    /// Validates and processes Trojan-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Trojan profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddTrojanServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.Trojan;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        if (profileItem.StreamSecurity.IsNullOrEmpty())
        {
            profileItem.StreamSecurity = Global.StreamSecurity;
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit a Hysteria2 server
    /// Validates and processes Hysteria2-specific settings
    /// Sets the core type to sing_box as required by Hysteria2
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Hysteria2 profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddHysteria2Server(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.Hysteria2;
        //profileItem.CoreType = ECoreType.sing_box;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.Path = profileItem.Path.TrimEx();
        profileItem.Network = string.Empty;

        if (profileItem.StreamSecurity.IsNullOrEmpty())
        {
            profileItem.StreamSecurity = Global.StreamSecurity;
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit a TUIC server
    /// Validates and processes TUIC-specific settings
    /// Sets the core type to sing_box as required by TUIC
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">TUIC profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddTuicServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.TUIC;
        profileItem.CoreType = ECoreType.sing_box;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.Security = profileItem.Security.TrimEx();
        profileItem.Network = string.Empty;

        if (!Global.TuicCongestionControls.Contains(profileItem.HeaderType))
        {
            profileItem.HeaderType = Global.TuicCongestionControls.FirstOrDefault()!;
        }

        if (profileItem.StreamSecurity.IsNullOrEmpty())
        {
            profileItem.StreamSecurity = Global.StreamSecurity;
        }
        if (profileItem.Alpn.IsNullOrEmpty())
        {
            profileItem.Alpn = "h3";
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit a WireGuard server
    /// Validates and processes WireGuard-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">WireGuard profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddWireguardServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.WireGuard;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.PublicKey = profileItem.PublicKey.TrimEx();
        profileItem.Path = profileItem.Path.TrimEx();
        profileItem.RequestHost = profileItem.RequestHost.TrimEx();
        profileItem.Network = string.Empty;
        if (profileItem.ShortId.IsNullOrEmpty())
        {
            profileItem.ShortId = Global.TunMtus.First().ToString();
        }

        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Add or edit a Anytls server
    /// Validates and processes Anytls-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Anytls profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddAnytlsServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.Anytls;
        profileItem.CoreType = ECoreType.sing_box;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.Security = profileItem.Security.TrimEx();
        profileItem.Network = string.Empty;
        if (profileItem.StreamSecurity.IsNullOrEmpty())
        {
            profileItem.StreamSecurity = Global.StreamSecurity;
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }
        await AddServerCommon(config, profileItem, toFile);
        return 0;
    }

    /// <summary>
    /// Sort the server list by the specified column
    /// Updates the sort order in the profile extension data
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="subId">Subscription ID to filter servers</param>
    /// <param name="colName">Column name to sort by</param>
    /// <param name="asc">Sort in ascending order if true, descending if false</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> SortServers(Config config, string subId, string colName, bool asc)
    {
        var lstModel = await AppManager.Instance.ProfileItems(subId, "");
        if (lstModel.Count <= 0)
        {
            return -1;
        }
        var lstServerStat = (config.GuiItem.EnableStatistics ? StatisticsManager.Instance.ServerStat : null) ?? [];
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        var lstProfile = (from t in lstModel
                          join t2 in lstServerStat on t.IndexId equals t2.IndexId into t2b
                          from t22 in t2b.DefaultIfEmpty()
                          join t3 in lstProfileExs on t.IndexId equals t3.IndexId into t3b
                          from t33 in t3b.DefaultIfEmpty()
                          select new ProfileItemModel
                          {
                              IndexId = t.IndexId,
                              ConfigType = t.ConfigType,
                              Remarks = t.Remarks,
                              Address = t.Address,
                              Port = t.Port,
                              Security = t.Security,
                              Network = t.Network,
                              StreamSecurity = t.StreamSecurity,
                              Delay = t33?.Delay ?? 0,
                              Speed = t33?.Speed ?? 0,
                              Sort = t33?.Sort ?? 0,
                              TodayDown = (t22?.TodayDown ?? 0).ToString("D16"),
                              TodayUp = (t22?.TodayUp ?? 0).ToString("D16"),
                              TotalDown = (t22?.TotalDown ?? 0).ToString("D16"),
                              TotalUp = (t22?.TotalUp ?? 0).ToString("D16"),
                          }).ToList();

        Enum.TryParse(colName, true, out EServerColName name);

        if (asc)
        {
            lstProfile = name switch
            {
                EServerColName.ConfigType => lstProfile.OrderBy(t => t.ConfigType).ToList(),
                EServerColName.Remarks => lstProfile.OrderBy(t => t.Remarks).ToList(),
                EServerColName.Address => lstProfile.OrderBy(t => t.Address).ToList(),
                EServerColName.Port => lstProfile.OrderBy(t => t.Port).ToList(),
                EServerColName.Network => lstProfile.OrderBy(t => t.Network).ToList(),
                EServerColName.StreamSecurity => lstProfile.OrderBy(t => t.StreamSecurity).ToList(),
                EServerColName.DelayVal => lstProfile.OrderBy(t => t.Delay).ToList(),
                EServerColName.SpeedVal => lstProfile.OrderBy(t => t.Speed).ToList(),
                EServerColName.SubRemarks => lstProfile.OrderBy(t => t.Subid).ToList(),
                EServerColName.TodayDown => lstProfile.OrderBy(t => t.TodayDown).ToList(),
                EServerColName.TodayUp => lstProfile.OrderBy(t => t.TodayUp).ToList(),
                EServerColName.TotalDown => lstProfile.OrderBy(t => t.TotalDown).ToList(),
                EServerColName.TotalUp => lstProfile.OrderBy(t => t.TotalUp).ToList(),
                _ => lstProfile
            };
        }
        else
        {
            lstProfile = name switch
            {
                EServerColName.ConfigType => lstProfile.OrderByDescending(t => t.ConfigType).ToList(),
                EServerColName.Remarks => lstProfile.OrderByDescending(t => t.Remarks).ToList(),
                EServerColName.Address => lstProfile.OrderByDescending(t => t.Address).ToList(),
                EServerColName.Port => lstProfile.OrderByDescending(t => t.Port).ToList(),
                EServerColName.Network => lstProfile.OrderByDescending(t => t.Network).ToList(),
                EServerColName.StreamSecurity => lstProfile.OrderByDescending(t => t.StreamSecurity).ToList(),
                EServerColName.DelayVal => lstProfile.OrderByDescending(t => t.Delay).ToList(),
                EServerColName.SpeedVal => lstProfile.OrderByDescending(t => t.Speed).ToList(),
                EServerColName.SubRemarks => lstProfile.OrderByDescending(t => t.Subid).ToList(),
                EServerColName.TodayDown => lstProfile.OrderByDescending(t => t.TodayDown).ToList(),
                EServerColName.TodayUp => lstProfile.OrderByDescending(t => t.TodayUp).ToList(),
                EServerColName.TotalDown => lstProfile.OrderByDescending(t => t.TotalDown).ToList(),
                EServerColName.TotalUp => lstProfile.OrderByDescending(t => t.TotalUp).ToList(),
                _ => lstProfile
            };
        }

        for (var i = 0; i < lstProfile.Count; i++)
        {
            ProfileExManager.Instance.SetSort(lstProfile[i].IndexId, (i + 1) * 10);
        }
        switch (name)
        {
            case EServerColName.DelayVal:
                {
                    var maxSort = lstProfile.Max(t => t.Sort) + 10;
                    foreach (var item in lstProfile.Where(item => item.Delay <= 0))
                    {
                        ProfileExManager.Instance.SetSort(item.IndexId, maxSort);
                    }

                    break;
                }
            case EServerColName.SpeedVal:
                {
                    var maxSort = lstProfile.Max(t => t.Sort) + 10;
                    foreach (var item in lstProfile.Where(item => item.Speed <= 0))
                    {
                        ProfileExManager.Instance.SetSort(item.IndexId, maxSort);
                    }

                    break;
                }
        }

        return 0;
    }

    /// <summary>
    /// Add or edit a VLESS server
    /// Validates and processes VLESS-specific settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">VLESS profile to add</param>
    /// <param name="toFile">Whether to save to file</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddVlessServer(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigType = EConfigType.VLESS;

        profileItem.Address = profileItem.Address.TrimEx();
        profileItem.Id = profileItem.Id.TrimEx();
        profileItem.Security = profileItem.Security.TrimEx();
        profileItem.Network = profileItem.Network.TrimEx();
        profileItem.HeaderType = profileItem.HeaderType.TrimEx();
        profileItem.RequestHost = profileItem.RequestHost.TrimEx();
        profileItem.Path = profileItem.Path.TrimEx();
        profileItem.StreamSecurity = profileItem.StreamSecurity.TrimEx();

        var protocolExtra = profileItem.GetProtocolExtra();

        if (!Global.Flows.Contains(protocolExtra.Flow ?? string.Empty))
        {
            protocolExtra.Flow = Global.Flows.First();
        }
        if (profileItem.Id.IsNullOrEmpty())
        {
            return -1;
        }
        if (profileItem.Security.IsNullOrEmpty())
        {
            profileItem.Security = Global.None;
        }

        profileItem.SetProtocolExtra(protocolExtra);

        await AddServerCommon(config, profileItem, toFile);

        return 0;
    }

    /// <summary>
    /// Remove duplicate servers from a subscription
    /// Compares servers based on their properties rather than just names
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="subId">Subscription ID to deduplicate</param>
    /// <returns>Tuple with total count and remaining count after deduplication</returns>
    public static async Task<Tuple<int, int>> DedupServerList(Config config, string subId)
    {
        var lstProfile = await AppManager.Instance.ProfileItems(subId);
        if (lstProfile == null)
        {
            return new Tuple<int, int>(0, 0);
        }

        List<ProfileItem> lstKeep = new();
        List<ProfileItem> lstRemove = new();
        if (!config.GuiItem.KeepOlderDedupl)
        {
            lstProfile.Reverse();
        }

        foreach (var item in lstProfile)
        {
            if (!lstKeep.Exists(i => CompareProfileItem(i, item, false)))
            {
                lstKeep.Add(item);
            }
            else
            {
                lstRemove.Add(item);
            }
        }
        await RemoveServers(config, lstRemove);

        return new Tuple<int, int>(lstProfile.Count, lstKeep.Count);
    }

    /// <summary>
    /// Common server addition logic used by all server types
    /// Sets common properties and handles sorting and persistence
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="profileItem">Profile item to add</param>
    /// <param name="toFile">Whether to save to database</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> AddServerCommon(Config config, ProfileItem profileItem, bool toFile = true)
    {
        profileItem.ConfigVersion = 3;

        if (profileItem.StreamSecurity.IsNotEmpty())
        {
            if (profileItem.StreamSecurity != Global.StreamSecurity
                 && profileItem.StreamSecurity != Global.StreamSecurityReality)
            {
                profileItem.StreamSecurity = string.Empty;
            }
            else
            {
                if (profileItem.AllowInsecure.IsNullOrEmpty())
                {
                    profileItem.AllowInsecure = config.CoreBasicItem.DefAllowInsecure.ToString().ToLower();
                }
                if (profileItem.Fingerprint.IsNullOrEmpty() && profileItem.StreamSecurity == Global.StreamSecurityReality)
                {
                    profileItem.Fingerprint = config.CoreBasicItem.DefFingerprint;
                }
            }
        }

        if (profileItem.Network.IsNotEmpty() && !Global.Networks.Contains(profileItem.Network))
        {
            profileItem.Network = Global.DefaultNetwork;
        }

        var maxSort = -1;
        if (profileItem.IndexId.IsNullOrEmpty())
        {
            profileItem.IndexId = Utils.GetGuid(false);
            maxSort = ProfileExManager.Instance.GetMaxSort();
        }
        if (!toFile && maxSort < 0)
        {
            maxSort = ProfileExManager.Instance.GetMaxSort();
        }
        if (maxSort > 0)
        {
            ProfileExManager.Instance.SetSort(profileItem.IndexId, maxSort + 1);
        }

        if (toFile)
        {
            await SQLiteHelper.Instance.ReplaceAsync(profileItem);
        }
        return 0;
    }

    /// <summary>
    /// Compare two profile items to determine if they represent the same server
    /// Used for deduplication and server matching
    /// </summary>
    /// <param name="o">First profile item</param>
    /// <param name="n">Second profile item</param>
    /// <param name="remarks">Whether to compare remarks</param>
    /// <returns>True if the profiles match, false otherwise</returns>
    private static bool CompareProfileItem(ProfileItem? o, ProfileItem? n, bool remarks)
    {
        if (o == null || n == null)
        {
            return false;
        }

        var protocolExtra = o.GetProtocolExtra();

        return o.ConfigType == n.ConfigType
               && AreEqual(o.Address, n.Address)
               && o.Port == n.Port
               && AreEqual(o.Id, n.Id)
               && AreEqual(o.Security, n.Security)
               && AreEqual(o.Network, n.Network)
               && AreEqual(o.HeaderType, n.HeaderType)
               && AreEqual(o.RequestHost, n.RequestHost)
               && AreEqual(o.Path, n.Path)
               && (o.ConfigType == EConfigType.Trojan || o.StreamSecurity == n.StreamSecurity)
               && AreEqual(protocolExtra.Flow, protocolExtra.Flow)
               && AreEqual(o.Sni, n.Sni)
               && AreEqual(o.Alpn, n.Alpn)
               && AreEqual(o.Fingerprint, n.Fingerprint)
               && AreEqual(o.PublicKey, n.PublicKey)
               && AreEqual(o.ShortId, n.ShortId)
               && (!remarks || o.Remarks == n.Remarks);

        static bool AreEqual(string? a, string? b)
        {
            return string.Equals(a, b) || (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b));
        }
    }

    /// <summary>
    /// Remove a single server profile by its index ID
    /// Deletes the configuration file if it's a custom config
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="indexId">Index ID of the profile to remove</param>
    /// <returns>0 if successful</returns>
    private static async Task<int> RemoveProfileItem(Config config, string indexId)
    {
        try
        {
            var item = await AppManager.Instance.GetProfileItem(indexId);
            if (item == null)
            {
                return 0;
            }
            if (item.ConfigType == EConfigType.Custom)
            {
                File.Delete(Utils.GetConfigPath(item.Address));
            }

            await SQLiteHelper.Instance.DeleteAsync(item);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return 0;
    }

    /// <summary>
    /// Create a group server that combines multiple servers for load balancing
    /// Generates a configuration file that references multiple servers
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="selecteds">Selected servers to combine</param>
    /// <param name="coreType">Core type to use (Xray or sing_box)</param>
    /// <param name="multipleLoad">Load balancing algorithm</param>
    /// <returns>Result object with success state and data</returns>
    public static async Task<RetResult> AddGroupServer4Multiple(Config config, List<ProfileItem> selecteds, ECoreType coreType, EMultipleLoad multipleLoad, string? subId)
    {
        var result = new RetResult();

        var indexId = Utils.GetGuid(false);
        var childProfileIndexId = Utils.List2String(selecteds.Select(p => p.IndexId).ToList());

        var remark = subId.IsNullOrEmpty() ? string.Empty : $"{(await AppManager.Instance.GetSubItem(subId))?.Remarks} ";
        if (coreType == ECoreType.Xray)
        {
            remark += multipleLoad switch
            {
                EMultipleLoad.LeastPing => ResUI.menuGenGroupMultipleServerXrayLeastPing,
                EMultipleLoad.Fallback => ResUI.menuGenGroupMultipleServerXrayFallback,
                EMultipleLoad.Random => ResUI.menuGenGroupMultipleServerXrayRandom,
                EMultipleLoad.RoundRobin => ResUI.menuGenGroupMultipleServerXrayRoundRobin,
                EMultipleLoad.LeastLoad => ResUI.menuGenGroupMultipleServerXrayLeastLoad,
                _ => ResUI.menuGenGroupMultipleServerXrayRoundRobin,
            };
        }
        else if (coreType == ECoreType.sing_box)
        {
            remark += multipleLoad switch
            {
                EMultipleLoad.LeastPing => ResUI.menuGenGroupMultipleServerSingBoxLeastPing,
                EMultipleLoad.Fallback => ResUI.menuGenGroupMultipleServerSingBoxFallback,
                _ => ResUI.menuGenGroupMultipleServerSingBoxLeastPing,
            };
        }
        var profile = new ProfileItem
        {
            IndexId = indexId,
            CoreType = coreType,
            ConfigType = EConfigType.PolicyGroup,
            Remarks = remark,
            IsSub = false
        };
        if (!subId.IsNullOrEmpty())
        {
            profile.Subid = subId;
        }
        var extraItem = new ProtocolExtraItem
        {
            ChildItems = childProfileIndexId, MultipleLoad = multipleLoad,
        };
        profile.SetProtocolExtra(extraItem);
        var ret = await AddServerCommon(config, profile, true);
        result.Success = ret == 0;
        result.Data = indexId;
        return result;
    }

    /// <summary>
    /// Get a SOCKS server profile for pre-SOCKS functionality
    /// Used when TUN mode is enabled or when a custom config has a pre-SOCKS port
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="node">Server node that might need pre-SOCKS</param>
    /// <param name="coreType">Core type being used</param>
    /// <returns>A SOCKS profile item or null if not needed</returns>
    public static async Task<ProfileItem?> GetPreSocksItem(Config config, ProfileItem node, ECoreType coreType)
    {
        ProfileItem? itemSocks = null;
        if (node.ConfigType != EConfigType.Custom && coreType != ECoreType.sing_box && config.TunModeItem.EnableTun)
        {
            var tun2SocksAddress = node.Address;
            if (node.ConfigType.IsGroupType())
            {
                var lstAddresses = (await GroupProfileManager.GetAllChildDomainAddresses(node)).ToList();
                if (lstAddresses.Count > 0)
                {
                    tun2SocksAddress = Utils.List2String(lstAddresses);
                }
            }
            itemSocks = new ProfileItem()
            {
                CoreType = ECoreType.sing_box,
                ConfigType = EConfigType.SOCKS,
                Address = Global.Loopback,
                SpiderX = tun2SocksAddress, // Tun2SocksAddress
                Port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks)
            };
        }
        else if (node.ConfigType == EConfigType.Custom && node.PreSocksPort > 0)
        {
            var preCoreType = AppManager.Instance.RunningCoreType = config.TunModeItem.EnableTun ? ECoreType.sing_box : ECoreType.Xray;
            itemSocks = new ProfileItem()
            {
                CoreType = preCoreType,
                ConfigType = EConfigType.SOCKS,
                Address = Global.Loopback,
                Port = node.PreSocksPort.Value,
            };
        }
        await Task.CompletedTask;
        return itemSocks;
    }

    /// <summary>
    /// Remove servers with invalid test results (timeout)
    /// Useful for cleaning up subscription lists
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="subid">Subscription ID to filter servers</param>
    /// <returns>Number of removed servers or -1 if failed</returns>
    public static async Task<int> RemoveInvalidServerResult(Config config, string subid)
    {
        var lstModel = await AppManager.Instance.ProfileItems(subid, "");
        if (lstModel is { Count: <= 0 })
        {
            return -1;
        }
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        var lstProfile = (from t in lstModel
                          join t2 in lstProfileExs on t.IndexId equals t2.IndexId
                          where t2.Delay == -1
                          select t).ToList();

        await RemoveServers(config, JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(lstProfile)));

        return lstProfile.Count;
    }

    #endregion Server

    #region Batch add servers

    /// <summary>
    /// Add multiple servers from string data (common protocols)
    /// Parses the string data into server profiles
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="strData">String data containing server information</param>
    /// <param name="subid">Subscription ID to associate with the servers</param>
    /// <param name="isSub">Whether this is from a subscription</param>
    /// <returns>Number of successfully imported servers or -1 if failed</returns>
    private static async Task<int> AddBatchServersCommon(Config config, string strData, string subid, bool isSub)
    {
        if (strData.IsNullOrEmpty())
        {
            return -1;
        }

        var subFilter = string.Empty;
        //remove sub items
        if (isSub && subid.IsNotEmpty())
        {
            await RemoveServersViaSubid(config, subid, isSub);
            subFilter = (await AppManager.Instance.GetSubItem(subid))?.Filter ?? "";
        }

        var countServers = 0;
        List<ProfileItem> lstAdd = new();
        var arrData = strData.Split(Environment.NewLine.ToCharArray()).Where(t => !t.IsNullOrEmpty());
        if (isSub)
        {
            arrData = arrData.Distinct();
        }
        foreach (var str in arrData)
        {
            //maybe sub
            if (!isSub && (str.StartsWith(Global.HttpsProtocol) || str.StartsWith(Global.HttpProtocol)))
            {
                if (await AddSubItem(config, str) == 0)
                {
                    countServers++;
                }
                continue;
            }
            var profileItem = FmtHandler.ResolveConfig(str, out var msg);
            if (profileItem is null)
            {
                continue;
            }

            //exist sub items //filter
            if (isSub && subid.IsNotEmpty() && subFilter.IsNotEmpty())
            {
                if (!Regex.IsMatch(profileItem.Remarks, subFilter))
                {
                    continue;
                }
            }
            profileItem.Subid = subid;
            profileItem.IsSub = isSub;

            var addStatus = profileItem.ConfigType switch
            {
                EConfigType.VMess => await AddVMessServer(config, profileItem, false),
                EConfigType.Shadowsocks => await AddShadowsocksServer(config, profileItem, false),
                EConfigType.SOCKS => await AddSocksServer(config, profileItem, false),
                EConfigType.Trojan => await AddTrojanServer(config, profileItem, false),
                EConfigType.VLESS => await AddVlessServer(config, profileItem, false),
                EConfigType.Hysteria2 => await AddHysteria2Server(config, profileItem, false),
                EConfigType.TUIC => await AddTuicServer(config, profileItem, false),
                EConfigType.WireGuard => await AddWireguardServer(config, profileItem, false),
                EConfigType.Anytls => await AddAnytlsServer(config, profileItem, false),
                _ => -1,
            };

            if (addStatus == 0)
            {
                countServers++;
                lstAdd.Add(profileItem);
            }
        }

        if (lstAdd.Count > 0)
        {
            await SQLiteHelper.Instance.InsertAllAsync(lstAdd);
        }

        await SaveConfig(config);
        return countServers;
    }

    /// <summary>
    /// Add servers from custom configuration formats (sing-box, v2ray, etc.)
    /// Handles various configuration formats and imports them as custom configs
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="strData">String data containing server information</param>
    /// <param name="subid">Subscription ID to associate with the servers</param>
    /// <param name="isSub">Whether this is from a subscription</param>
    /// <returns>Number of successfully imported servers or -1 if failed</returns>
    private static async Task<int> AddBatchServers4Custom(Config config, string strData, string subid, bool isSub)
    {
        if (strData.IsNullOrEmpty())
        {
            return -1;
        }

        var subItem = await AppManager.Instance.GetSubItem(subid);
        var subRemarks = subItem?.Remarks;
        var preSocksPort = subItem?.PreSocksPort;

        List<ProfileItem>? lstProfiles = null;
        //Is sing-box array configuration
        if (lstProfiles is null || lstProfiles.Count <= 0)
        {
            lstProfiles = SingboxFmt.ResolveFullArray(strData, subRemarks);
        }
        //Is v2ray array configuration
        if (lstProfiles is null || lstProfiles.Count <= 0)
        {
            lstProfiles = V2rayFmt.ResolveFullArray(strData, subRemarks);
        }
        if (lstProfiles != null && lstProfiles.Count > 0)
        {
            if (isSub && subid.IsNotEmpty())
            {
                await RemoveServersViaSubid(config, subid, isSub);
            }
            var count = 0;
            foreach (var it in lstProfiles)
            {
                it.Subid = subid;
                it.IsSub = isSub;
                it.PreSocksPort = preSocksPort;
                if (await AddCustomServer(config, it, true) == 0)
                {
                    count++;
                }
            }
            if (count > 0)
            {
                return count;
            }
        }

        ProfileItem? profileItem = null;
        //Is sing-box configuration
        if (profileItem is null)
        {
            profileItem = SingboxFmt.ResolveFull(strData, subRemarks);
        }
        //Is v2ray configuration
        if (profileItem is null)
        {
            profileItem = V2rayFmt.ResolveFull(strData, subRemarks);
        }
        //Is Html Page
        if (profileItem is null && HtmlPageFmt.IsHtmlPage(strData))
        {
            return -1;
        }
        //Is Clash configuration
        if (profileItem is null)
        {
            profileItem = ClashFmt.ResolveFull(strData, subRemarks);
        }
        //Is hysteria configuration
        if (profileItem is null)
        {
            profileItem = Hysteria2Fmt.ResolveFull2(strData, subRemarks);
        }
        if (profileItem is null || profileItem.Address.IsNullOrEmpty())
        {
            return -1;
        }

        if (isSub && subid.IsNotEmpty())
        {
            await RemoveServersViaSubid(config, subid, isSub);
        }

        profileItem.Subid = subid;
        profileItem.IsSub = isSub;
        profileItem.PreSocksPort = preSocksPort;
        if (await AddCustomServer(config, profileItem, true) == 0)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>
    /// Add Shadowsocks servers from SIP008 format
    /// SIP008 is a JSON-based format for Shadowsocks servers
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="strData">String data in SIP008 format</param>
    /// <param name="subid">Subscription ID to associate with the servers</param>
    /// <param name="isSub">Whether this is from a subscription</param>
    /// <returns>Number of successfully imported servers or -1 if failed</returns>
    private static async Task<int> AddBatchServers4SsSIP008(Config config, string strData, string subid, bool isSub)
    {
        if (strData.IsNullOrEmpty())
        {
            return -1;
        }

        if (isSub && subid.IsNotEmpty())
        {
            await RemoveServersViaSubid(config, subid, isSub);
        }

        var lstSsServer = ShadowsocksFmt.ResolveSip008(strData);
        if (lstSsServer?.Count > 0)
        {
            var counter = 0;
            foreach (var ssItem in lstSsServer)
            {
                ssItem.Subid = subid;
                ssItem.IsSub = isSub;
                if (await AddShadowsocksServer(config, ssItem) == 0)
                {
                    counter++;
                }
            }
            await SaveConfig(config);
            return counter;
        }

        return -1;
    }

    /// <summary>
    /// Main entry point for adding batch servers from various formats
    /// Tries different parsing methods to import as many servers as possible
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="strData">String data containing server information</param>
    /// <param name="subid">Subscription ID to associate with the servers</param>
    /// <param name="isSub">Whether this is from a subscription</param>
    /// <returns>Number of successfully imported servers or -1 if failed</returns>
    public static async Task<int> AddBatchServers(Config config, string strData, string subid, bool isSub)
    {
        if (strData.IsNullOrEmpty())
        {
            return -1;
        }
        List<ProfileItem>? lstOriSub = null;
        ProfileItem? activeProfile = null;
        if (isSub && subid.IsNotEmpty())
        {
            lstOriSub = await AppManager.Instance.ProfileItems(subid);
            activeProfile = lstOriSub?.FirstOrDefault(t => t.IndexId == config.IndexId);
        }

        var counter = 0;
        if (Utils.IsBase64String(strData))
        {
            counter = await AddBatchServersCommon(config, Utils.Base64Decode(strData), subid, isSub);
        }
        if (counter < 1)
        {
            counter = await AddBatchServersCommon(config, strData, subid, isSub);
        }
        if (counter < 1)
        {
            counter = await AddBatchServersCommon(config, Utils.Base64Decode(strData), subid, isSub);
        }

        if (counter < 1)
        {
            counter = await AddBatchServers4SsSIP008(config, strData, subid, isSub);
        }

        //maybe other sub
        if (counter < 1)
        {
            counter = await AddBatchServers4Custom(config, strData, subid, isSub);
        }

        //Select active node
        if (activeProfile != null)
        {
            var lstSub = await AppManager.Instance.ProfileItems(subid);
            var existItem = lstSub?.FirstOrDefault(t => config.UiItem.EnableUpdateSubOnlyRemarksExist ? t.Remarks == activeProfile.Remarks : CompareProfileItem(t, activeProfile, true));
            if (existItem != null)
            {
                await ConfigHandler.SetDefaultServerIndex(config, existItem.IndexId);
            }
        }

        //Keep the last traffic statistics
        if (lstOriSub != null)
        {
            var lstSub = await AppManager.Instance.ProfileItems(subid);
            foreach (var item in lstSub)
            {
                var existItem = lstOriSub?.FirstOrDefault(t => config.UiItem.EnableUpdateSubOnlyRemarksExist ? t.Remarks == item.Remarks : CompareProfileItem(t, item, true));
                if (existItem != null)
                {
                    await StatisticsManager.Instance.CloneServerStatItem(existItem.IndexId, item.IndexId);
                }
            }
        }

        return counter;
    }

    #endregion Batch add servers

    #region Sub & Group

    /// <summary>
    /// Add a subscription item from URL
    /// Creates a new subscription with default settings
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="url">Subscription URL</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddSubItem(Config config, string url)
    {
        //already exists
        var count = await SQLiteHelper.Instance.TableAsync<SubItem>().CountAsync(e => e.Url == url);
        if (count > 0)
        {
            return 0;
        }
        SubItem subItem = new()
        {
            Id = string.Empty,
            Url = url
        };

        var uri = Utils.TryUri(url);
        if (uri == null)
        {
            return -1;
        }
        //Do not allow http protocol
        if (url.StartsWith(Global.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
        {
            //TODO Temporary reminder to be removed later
            NoticeManager.Instance.Enqueue(ResUI.InsecureUrlProtocol);
            //return -1;
        }

        var queryVars = Utils.ParseQueryString(uri.Query);
        subItem.Remarks = queryVars["remarks"] ?? "import_sub";

        return await AddSubItem(config, subItem);
    }

    /// <summary>
    /// Add or update a subscription item
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="subItem">Subscription item to add or update</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddSubItem(Config config, SubItem subItem)
    {
        var item = await AppManager.Instance.GetSubItem(subItem.Id);
        if (item is null)
        {
            item = subItem;
        }
        else
        {
            item.Remarks = subItem.Remarks;
            item.Url = subItem.Url;
            item.MoreUrl = subItem.MoreUrl;
            item.Enabled = subItem.Enabled;
            item.AutoUpdateInterval = subItem.AutoUpdateInterval;
            item.UserAgent = subItem.UserAgent;
            item.Sort = subItem.Sort;
            item.Filter = subItem.Filter;
            item.UpdateTime = subItem.UpdateTime;
            item.ConvertTarget = subItem.ConvertTarget;
            item.PrevProfile = subItem.PrevProfile;
            item.NextProfile = subItem.NextProfile;
            item.PreSocksPort = subItem.PreSocksPort;
            item.Memo = subItem.Memo;
        }

        if (item.Id.IsNullOrEmpty())
        {
            item.Id = Utils.GetGuid(false);

            if (item.Sort <= 0)
            {
                var maxSort = 0;
                if (await SQLiteHelper.Instance.TableAsync<SubItem>().CountAsync() > 0)
                {
                    var lstSubs = await AppManager.Instance.SubItems();
                    maxSort = lstSubs.LastOrDefault()?.Sort ?? 0;
                }
                item.Sort = maxSort + 1;
            }
        }
        if (await SQLiteHelper.Instance.ReplaceAsync(item) > 0)
        {
            return 0;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>
    /// Remove servers associated with a subscription ID
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="subid">Subscription ID</param>
    /// <param name="isSub">Whether to only remove servers marked as subscription items</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> RemoveServersViaSubid(Config config, string subid, bool isSub)
    {
        if (subid.IsNullOrEmpty())
        {
            return -1;
        }
        var customProfile = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.Subid == subid && t.ConfigType == EConfigType.Custom).ToListAsync();
        if (isSub)
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileItem where isSub = 1 and subid = '{subid}'");
        }
        else
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileItem where subid = '{subid}'");
        }
        foreach (var item in customProfile)
        {
            File.Delete(Utils.GetConfigPath(item.Address));
        }

        return 0;
    }

    /// <summary>
    /// Delete a subscription item and all its associated servers
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="id">Subscription ID to delete</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> DeleteSubItem(Config config, string id)
    {
        var item = await AppManager.Instance.GetSubItem(id);
        if (item is null)
        {
            return 0;
        }
        await SQLiteHelper.Instance.DeleteAsync(item);
        await RemoveServersViaSubid(config, id, false);

        return 0;
    }

    /// <summary>
    /// Move servers to a different group (subscription)
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="lstProfile">List of profiles to move</param>
    /// <param name="subid">Target subscription ID</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> MoveToGroup(Config config, List<ProfileItem> lstProfile, string subid)
    {
        foreach (var item in lstProfile)
        {
            item.Subid = subid;
        }
        await SQLiteHelper.Instance.UpdateAllAsync(lstProfile);

        return 0;
    }

    #endregion Sub & Group

    #region Routing

    /// <summary>
    /// Save a routing item to the database
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="item">Routing item to save</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> SaveRoutingItem(Config config, RoutingItem item)
    {
        if (item.Id.IsNullOrEmpty())
        {
            item.Id = Utils.GetGuid(false);
        }

        if (await SQLiteHelper.Instance.ReplaceAsync(item) > 0)
        {
            return 0;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>
    /// Add multiple routing rules to a routing item
    /// </summary>
    /// <param name="routingItem">Routing item to add rules to</param>
    /// <param name="strData">JSON string containing rules data</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> AddBatchRoutingRules(RoutingItem routingItem, string strData)
    {
        if (strData.IsNullOrEmpty())
        {
            return -1;
        }

        var lstRules = JsonUtils.Deserialize<List<RulesItem>>(strData);
        if (lstRules == null)
        {
            return -1;
        }

        foreach (var item in lstRules)
        {
            item.Id = Utils.GetGuid(false);
        }
        routingItem.RuleNum = lstRules.Count;
        routingItem.RuleSet = JsonUtils.Serialize(lstRules, false);

        if (routingItem.Id.IsNullOrEmpty())
        {
            routingItem.Id = Utils.GetGuid(false);
        }

        if (await SQLiteHelper.Instance.ReplaceAsync(routingItem) > 0)
        {
            return 0;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>
    /// Move a routing rule within a rules list
    /// Supports moving to top, up, down, bottom or specific position
    /// </summary>
    /// <param name="rules">List of routing rules</param>
    /// <param name="index">Index of the rule to move</param>
    /// <param name="eMove">Direction to move the rule</param>
    /// <param name="pos">Target position when using EMove.Position</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> MoveRoutingRule(List<RulesItem> rules, int index, EMove eMove, int pos = -1)
    {
        var count = rules.Count;
        if (index < 0 || index > rules.Count - 1)
        {
            return -1;
        }
        switch (eMove)
        {
            case EMove.Top:
                {
                    if (index == 0)
                    {
                        return 0;
                    }
                    var item = JsonUtils.DeepCopy(rules[index]);
                    rules.RemoveAt(index);
                    rules.Insert(0, item);

                    break;
                }
            case EMove.Up:
                {
                    if (index == 0)
                    {
                        return 0;
                    }
                    var item = JsonUtils.DeepCopy(rules[index]);
                    rules.RemoveAt(index);
                    rules.Insert(index - 1, item);

                    break;
                }

            case EMove.Down:
                {
                    if (index == count - 1)
                    {
                        return 0;
                    }
                    var item = JsonUtils.DeepCopy(rules[index]);
                    rules.RemoveAt(index);
                    rules.Insert(index + 1, item);

                    break;
                }
            case EMove.Bottom:
                {
                    if (index == count - 1)
                    {
                        return 0;
                    }
                    var item = JsonUtils.DeepCopy(rules[index]);
                    rules.RemoveAt(index);
                    rules.Add(item);

                    break;
                }
            case EMove.Position:
                {
                    var removeItem = rules[index];
                    var item = JsonUtils.DeepCopy(rules[index]);
                    rules.Insert(pos, item);
                    rules.Remove(removeItem);
                    break;
                }
        }
        return await Task.FromResult(0);
    }

    /// <summary>
    /// Set the default routing configuration
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="routingItem">Routing item to set as default</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> SetDefaultRouting(Config config, RoutingItem routingItem)
    {
        var items = await AppManager.Instance.RoutingItems();
        if (items.Any(t => t.Id == routingItem.Id && t.IsActive == true))
        {
            return -1;
        }

        foreach (var item in items)
        {
            if (item.Id == routingItem.Id)
            {
                item.IsActive = true;
            }
            else
            {
                item.IsActive = false;
            }
        }

        await SQLiteHelper.Instance.UpdateAllAsync(items);

        return 0;
    }

    /// <summary>
    /// Get the current default routing configuration
    /// If no default is set, selects the first available routing item
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <returns>The default routing item</returns>
    public static async Task<RoutingItem> GetDefaultRouting(Config config)
    {
        var item = await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync(it => it.IsActive == true);
        if (item is null)
        {
            var item2 = await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync();
            await SetDefaultRouting(config, item2);
            return item2;
        }

        return item;
    }

    /// <summary>
    /// Initialize routing rules from built-in or external templates
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="blImportAdvancedRules">Whether to import advanced rules</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> InitRouting(Config config, bool blImportAdvancedRules = false)
    {
        if (config.ConstItem.RouteRulesTemplateSourceUrl.IsNullOrEmpty())
        {
            await InitBuiltinRouting(config, blImportAdvancedRules);
        }
        else
        {
            await InitExternalRouting(config, blImportAdvancedRules);
        }

        return 0;
    }

    /// <summary>
    /// Initialize routing rules from external templates
    /// Downloads and processes routing templates from a URL
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="blImportAdvancedRules">Whether to import advanced rules</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> InitExternalRouting(Config config, bool blImportAdvancedRules = false)
    {
        var downloadHandle = new DownloadService();
        var templateContent = await downloadHandle.TryDownloadString(config.ConstItem.RouteRulesTemplateSourceUrl, true, "");
        if (templateContent.IsNullOrEmpty())
        {
            return await InitBuiltinRouting(config, blImportAdvancedRules); // fallback
        }

        var template = JsonUtils.Deserialize<RoutingTemplate>(templateContent);
        if (template == null)
        {
            return await InitBuiltinRouting(config, blImportAdvancedRules); // fallback
        }

        var items = await AppManager.Instance.RoutingItems();
        var maxSort = items.Count;
        if (!blImportAdvancedRules && items.Where(t => t.Remarks.StartsWith(template.Version)).ToList().Count > 0)
        {
            return 0;
        }
        for (var i = 0; i < template.RoutingItems.Length; i++)
        {
            var item = template.RoutingItems[i];

            if (item.Url.IsNullOrEmpty() && item.RuleSet.IsNullOrEmpty())
            {
                continue;
            }

            var ruleSetsString = !item.RuleSet.IsNullOrEmpty()
                ? item.RuleSet
                : await downloadHandle.TryDownloadString(item.Url, true, "");

            if (ruleSetsString.IsNullOrEmpty())
            {
                continue;
            }

            item.Remarks = $"{template.Version}-{item.Remarks}";
            item.Enabled = true;
            item.Sort = ++maxSort;
            item.Url = string.Empty;

            await AddBatchRoutingRules(item, ruleSetsString);

            //first rule as default at first startup
            if (!blImportAdvancedRules && i == 0)
            {
                await SetDefaultRouting(config, item);
            }
        }

        return 0;
    }

    /// <summary>
    /// Initialize built-in routing rules
    /// Creates default routing configurations (whitelist, blacklist, global)
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="blImportAdvancedRules">Whether to import advanced rules</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> InitBuiltinRouting(Config config, bool blImportAdvancedRules = false)
    {
        var ver = "V4-";
        var items = await AppManager.Instance.RoutingItems();

        //TODO Temporary code to be removed later
        var lockItem = items?.FirstOrDefault(t => t.Locked == true);
        if (lockItem != null)
        {
            await ConfigHandler.RemoveRoutingItem(lockItem);
            items = await AppManager.Instance.RoutingItems();
        }

        if (!blImportAdvancedRules && items.Count(u => u.Remarks.StartsWith(ver)) > 0)
        {
            //migrate
            //TODO Temporary code to be removed later
            if (config.RoutingBasicItem.RoutingIndexId.IsNotEmpty())
            {
                var item = items.FirstOrDefault(t => t.Id == config.RoutingBasicItem.RoutingIndexId);
                if (item != null)
                {
                    await SetDefaultRouting(config, item);
                }
                config.RoutingBasicItem.RoutingIndexId = string.Empty;
            }

            return 0;
        }

        var maxSort = items.Count;
        //Bypass the mainland
        var item2 = new RoutingItem()
        {
            Remarks = $"{ver}绕过大陆(Whitelist)",
            Url = string.Empty,
            Sort = maxSort + 1,
        };
        await AddBatchRoutingRules(item2, EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "white"));

        //Blacklist
        var item3 = new RoutingItem()
        {
            Remarks = $"{ver}黑名单(Blacklist)",
            Url = string.Empty,
            Sort = maxSort + 2,
        };
        await AddBatchRoutingRules(item3, EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "black"));

        //Global
        var item1 = new RoutingItem()
        {
            Remarks = $"{ver}全局(Global)",
            Url = string.Empty,
            Sort = maxSort + 3,
        };
        await AddBatchRoutingRules(item1, EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "global"));

        if (!blImportAdvancedRules)
        {
            await SetDefaultRouting(config, item2);
        }
        return 0;
    }

    /// <summary>
    /// Remove a routing item from the database
    /// </summary>
    /// <param name="routingItem">Routing item to remove</param>
    public static async Task RemoveRoutingItem(RoutingItem routingItem)
    {
        await SQLiteHelper.Instance.DeleteAsync(routingItem);
    }

    #endregion Routing

    #region DNS

    /// <summary>
    /// Initialize built-in DNS configurations
    /// Creates default DNS items for V2Ray and sing-box
    /// Also checks existing DNS items and disables those with empty NormalDNS
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <returns>0 if successful</returns>
    public static async Task<int> InitBuiltinDNS(Config config)
    {
        var items = await AppManager.Instance.DNSItems();

        // Check existing DNS items and disable those with empty NormalDNS
        var needsUpdate = false;
        foreach (var existingItem in items)
        {
            if (existingItem.NormalDNS.IsNullOrEmpty() && existingItem.Enabled)
            {
                existingItem.Enabled = false;
                needsUpdate = true;
            }
        }

        // Update items if any changes were made
        if (needsUpdate)
        {
            await SQLiteHelper.Instance.UpdateAllAsync(items);
        }

        if (items.Count <= 0)
        {
            var item = new DNSItem()
            {
                Remarks = "V2ray",
                CoreType = ECoreType.Xray,
                Enabled = false,
            };
            await SaveDNSItems(config, item);

            var item2 = new DNSItem()
            {
                Remarks = "sing-box",
                CoreType = ECoreType.sing_box,
                Enabled = false,
            };
            await SaveDNSItems(config, item2);
        }

        return 0;
    }

    /// <summary>
    /// Save a DNS item to the database
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="item">DNS item to save</param>
    /// <returns>0 if successful, -1 if failed</returns>
    public static async Task<int> SaveDNSItems(Config config, DNSItem item)
    {
        if (item == null)
        {
            return -1;
        }

        if (item.Id.IsNullOrEmpty())
        {
            item.Id = Utils.GetGuid(false);
        }

        if (await SQLiteHelper.Instance.ReplaceAsync(item) > 0)
        {
            return 0;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>
    /// Get an external DNS configuration from URL
    /// Downloads and processes DNS templates
    /// </summary>
    /// <param name="type">Core type (Xray or sing-box)</param>
    /// <param name="url">URL of the DNS template</param>
    /// <returns>DNS item with configuration from the URL</returns>
    public static async Task<DNSItem> GetExternalDNSItem(ECoreType type, string url)
    {
        var currentItem = await AppManager.Instance.GetDNSItem(type);

        var downloadHandle = new DownloadService();
        var templateContent = await downloadHandle.TryDownloadString(url, true, "");
        if (templateContent.IsNullOrEmpty())
        {
            return currentItem;
        }

        var template = JsonUtils.Deserialize<DNSItem>(templateContent);
        if (template == null)
        {
            return currentItem;
        }

        if (!template.NormalDNS.IsNullOrEmpty())
        {
            template.NormalDNS = await downloadHandle.TryDownloadString(template.NormalDNS, true, "");
        }

        if (!template.TunDNS.IsNullOrEmpty())
        {
            template.TunDNS = await downloadHandle.TryDownloadString(template.TunDNS, true, "");
        }

        template.Id = currentItem.Id;
        template.Enabled = currentItem.Enabled;
        template.Remarks = currentItem.Remarks;
        template.CoreType = type;

        return template;
    }

    #endregion DNS

    #region Simple DNS

    public static SimpleDNSItem InitBuiltinSimpleDNS()
    {
        return new SimpleDNSItem()
        {
            UseSystemHosts = false,
            AddCommonHosts = true,
            FakeIP = false,
            GlobalFakeIp = true,
            BlockBindingQuery = true,
            DirectDNS = Global.DomainDirectDNSAddress.FirstOrDefault(),
            RemoteDNS = Global.DomainRemoteDNSAddress.FirstOrDefault(),
            BootstrapDNS = Global.DomainPureIPDNSAddress.FirstOrDefault(),
        };
    }

    public static async Task<SimpleDNSItem> GetExternalSimpleDNSItem(string url)
    {
        var downloadHandle = new DownloadService();
        var templateContent = await downloadHandle.TryDownloadString(url, true, "");
        if (templateContent.IsNullOrEmpty())
        {
            return null;
        }

        var template = JsonUtils.Deserialize<SimpleDNSItem>(templateContent);
        if (template == null)
        {
            return null;
        }

        return template;
    }

    #endregion Simple DNS

    #region Custom Config

    public static async Task<int> InitBuiltinFullConfigTemplate(Config config)
    {
        var items = await AppManager.Instance.FullConfigTemplateItem();
        if (items.Count <= 0)
        {
            var item = new FullConfigTemplateItem()
            {
                Remarks = "V2ray",
                CoreType = ECoreType.Xray,
            };
            await SaveFullConfigTemplate(config, item);

            var item2 = new FullConfigTemplateItem()
            {
                Remarks = "sing-box",
                CoreType = ECoreType.sing_box,
            };
            await SaveFullConfigTemplate(config, item2);
        }

        return 0;
    }

    public static async Task<int> SaveFullConfigTemplate(Config config, FullConfigTemplateItem item)
    {
        if (item == null)
        {
            return -1;
        }

        if (item.Id.IsNullOrEmpty())
        {
            item.Id = Utils.GetGuid(false);
        }

        if (await SQLiteHelper.Instance.ReplaceAsync(item) > 0)
        {
            return 0;
        }
        else
        {
            return -1;
        }
    }

    #endregion Custom Config

    #region Regional Presets

    /// <summary>
    /// Apply regional presets for geo-specific configurations
    /// Sets up geo files, routing rules, and DNS for specific regions
    /// </summary>
    /// <param name="config">Current configuration</param>
    /// <param name="type">Type of preset (Default, Russia, Iran)</param>
    /// <returns>True if successful</returns>
    public static async Task<bool> ApplyRegionalPreset(Config config, EPresetType type)
    {
        switch (type)
        {
            case EPresetType.Default:
                config.ConstItem.GeoSourceUrl = "";
                config.ConstItem.SrsSourceUrl = "";
                config.ConstItem.RouteRulesTemplateSourceUrl = "";

                await SQLiteHelper.Instance.DeleteAllAsync<DNSItem>();
                await InitBuiltinDNS(config);

                config.SimpleDNSItem = InitBuiltinSimpleDNS();
                break;

            case EPresetType.Russia:
                config.ConstItem.GeoSourceUrl = Global.GeoFilesSources[1];
                config.ConstItem.SrsSourceUrl = Global.SingboxRulesetSources[1];
                config.ConstItem.RouteRulesTemplateSourceUrl = Global.RoutingRulesSources[1];

                var xrayDnsRussia = await GetExternalDNSItem(ECoreType.Xray, Global.DNSTemplateSources[1] + "v2ray.json");
                var singboxDnsRussia = await GetExternalDNSItem(ECoreType.sing_box, Global.DNSTemplateSources[1] + "sing_box.json");
                var simpleDnsRussia = await GetExternalSimpleDNSItem(Global.DNSTemplateSources[1] + "simple_dns.json");

                if (simpleDnsRussia == null)
                {
                    xrayDnsRussia.Enabled = true;
                    singboxDnsRussia.Enabled = true;
                    config.SimpleDNSItem = InitBuiltinSimpleDNS();
                }
                else
                {
                    config.SimpleDNSItem = simpleDnsRussia;
                }
                await SaveDNSItems(config, xrayDnsRussia);
                await SaveDNSItems(config, singboxDnsRussia);
                break;

            case EPresetType.Iran:
                config.ConstItem.GeoSourceUrl = Global.GeoFilesSources[2];
                config.ConstItem.SrsSourceUrl = Global.SingboxRulesetSources[2];
                config.ConstItem.RouteRulesTemplateSourceUrl = Global.RoutingRulesSources[2];

                var xrayDnsIran = await GetExternalDNSItem(ECoreType.Xray, Global.DNSTemplateSources[2] + "v2ray.json");
                var singboxDnsIran = await GetExternalDNSItem(ECoreType.sing_box, Global.DNSTemplateSources[2] + "sing_box.json");
                var simpleDnsIran = await GetExternalSimpleDNSItem(Global.DNSTemplateSources[2] + "simple_dns.json");

                if (simpleDnsIran == null)
                {
                    xrayDnsIran.Enabled = true;
                    singboxDnsIran.Enabled = true;
                    config.SimpleDNSItem = InitBuiltinSimpleDNS();
                }
                else
                {
                    config.SimpleDNSItem = simpleDnsIran;
                }
                await SaveDNSItems(config, xrayDnsIran);
                await SaveDNSItems(config, singboxDnsIran);
                break;
        }

        return true;
    }

    #endregion Regional Presets

    #region UIItem

    public static WindowSizeItem? GetWindowSizeItem(Config config, string typeName)
    {
        var sizeItem = config?.UiItem?.WindowSizeItem?.FirstOrDefault(t => t.TypeName == typeName);
        if (sizeItem == null || sizeItem.Width <= 0 || sizeItem.Height <= 0)
        {
            return null;
        }

        return sizeItem;
    }

    public static int SaveWindowSizeItem(Config config, string typeName, double width, double height)
    {
        var sizeItem = config?.UiItem?.WindowSizeItem?.FirstOrDefault(t => t.TypeName == typeName);
        if (sizeItem == null)
        {
            sizeItem = new WindowSizeItem { TypeName = typeName };
            config.UiItem.WindowSizeItem.Add(sizeItem);
        }

        sizeItem.Width = (int)width;
        sizeItem.Height = (int)height;

        return 0;
    }

    public static int SaveMainGirdHeight(Config config, double height1, double height2)
    {
        var uiItem = config.UiItem ?? new();

        uiItem.MainGirdHeight1 = (int)(height1 + 0.1);
        uiItem.MainGirdHeight2 = (int)(height2 + 0.1);

        return 0;
    }

    #endregion UIItem
}
