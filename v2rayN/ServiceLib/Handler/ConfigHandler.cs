using System.Data;
using System.Text.RegularExpressions;

namespace ServiceLib.Handler
{
    /// <summary>
    /// 本软件配置文件处理类
    /// </summary>
    public class ConfigHandler
    {
        private static readonly string _configRes = Global.ConfigFileName;
        private static readonly object _objLock = new();

        #region ConfigHandler

        /// <summary>
        /// 载入配置文件
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static Config? LoadConfig()
        {
            Config? config = null;
            var result = Utils.LoadResource(Utils.GetConfigPath(_configRes));
            if (Utils.IsNotEmpty(result))
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
                    config.Inbound[0].Protocol = EInboundProtocol.socks.ToString();
                }
            }

            config.RoutingBasicItem ??= new();

            if (Utils.IsNullOrEmpty(config.RoutingBasicItem.DomainStrategy))
            {
                config.RoutingBasicItem.DomainStrategy = Global.DomainStrategies[0];//"IPIfNonMatch";
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
            config.GuiItem ??= new()
            {
                EnableStatistics = false,
            };
            config.MsgUIItem ??= new();

            config.UiItem ??= new UIItem()
            {
                EnableAutoAdjustMainLvColWidth = true
            };
            config.UiItem.MainColumnItem ??= new();

            if (Utils.IsNullOrEmpty(config.UiItem.CurrentLanguage))
            {
                if (Thread.CurrentThread.CurrentCulture.Name.Equals("zh-cn", StringComparison.CurrentCultureIgnoreCase))
                {
                    config.UiItem.CurrentLanguage = Global.Languages[0];
                }
                else
                {
                    config.UiItem.CurrentLanguage = Global.Languages[2];
                }
            }

            config.ConstItem ??= new ConstItem();
            if (Utils.IsNullOrEmpty(config.ConstItem.DefIEProxyExceptions))
            {
                config.ConstItem.DefIEProxyExceptions = Global.IEProxyExceptions;
            }

            config.SpeedTestItem ??= new();
            if (config.SpeedTestItem.SpeedTestTimeout < 10)
            {
                config.SpeedTestItem.SpeedTestTimeout = 10;
            }
            if (Utils.IsNullOrEmpty(config.SpeedTestItem.SpeedTestUrl))
            {
                config.SpeedTestItem.SpeedTestUrl = Global.SpeedTestUrls[0];
            }
            if (Utils.IsNullOrEmpty(config.SpeedTestItem.SpeedPingTestUrl))
            {
                config.SpeedTestItem.SpeedPingTestUrl = Global.SpeedPingTestUrl;
            }

            config.Mux4RayItem ??= new()
            {
                Concurrency = 8,
                XudpConcurrency = 16,
                XudpProxyUDP443 = "reject"
            };

            config.Mux4SboxItem ??= new()
            {
                Protocol = Global.SingboxMuxs[0],
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

            return config;
        }

        /// <summary>
        /// 保参数
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static async Task<int> SaveConfig(Config config)
        {
            lock (_objLock)
            {
                try
                {
                    //save temp file
                    var resPath = Utils.GetConfigPath(_configRes);
                    var tempPath = $"{resPath}_temp";
                    if (JsonUtils.ToFile(config, tempPath) != 0)
                    {
                        return -1;
                    }

                    if (File.Exists(resPath))
                    {
                        File.Delete(resPath);
                    }
                    //rename
                    File.Move(tempPath, resPath);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ToJsonFile", ex);
                    return -1;
                }
            }

            return 0;
        }

        #endregion ConfigHandler

        #region Server

        public static async Task<int> AddServer(Config config, ProfileItem profileItem)
        {
            var item = await AppHandler.Instance.GetProfileItem(profileItem.IndexId);
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

                item.Id = profileItem.Id;
                item.AlterId = profileItem.AlterId;
                item.Security = profileItem.Security;
                item.Flow = profileItem.Flow;

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
                item.Extra = profileItem.Extra;
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
                _ => -1,
            };
            return ret;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
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
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="indexes"></param>
        /// <returns></returns>
        public static async Task<int> RemoveServer(Config config, List<ProfileItem> indexes)
        {
            var subid = "TempRemoveSubId";
            foreach (var item in indexes)
            {
                item.Subid = subid;
            }

            await SQLiteHelper.Instance.UpdateAllAsync(indexes);
            await RemoveServerViaSubid(config, subid, false);

            return 0;
        }

        /// <summary>
        /// 克隆服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static async Task<int> CopyServer(Config config, List<ProfileItem> indexes)
        {
            foreach (var it in indexes)
            {
                var item = await AppHandler.Instance.GetProfileItem(it.IndexId);
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
        /// 设置活动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static async Task<int> SetDefaultServerIndex(Config config, string? indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return -1;
            }

            config.IndexId = indexId;

            await SaveConfig(config);

            return 0;
        }

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

        public static async Task<ProfileItem?> GetDefaultServer(Config config)
        {
            var item = await AppHandler.Instance.GetProfileItem(config.IndexId);
            if (item is null)
            {
                var item2 = await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync();
                await SetDefaultServerIndex(config, item2?.IndexId);
                return item2;
            }

            return item;
        }

        /// <summary>
        /// 移动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="lstProfile"></param>
        /// <param name="index"></param>
        /// <param name="eMove"></param>
        /// <returns></returns>
        public static async Task<int> MoveServer(Config config, List<ProfileItem> lstProfile, int index, EMove eMove, int pos = -1)
        {
            int count = lstProfile.Count;
            if (index < 0 || index > lstProfile.Count - 1)
            {
                return -1;
            }

            for (int i = 0; i < lstProfile.Count; i++)
            {
                ProfileExHandler.Instance.SetSort(lstProfile[i].IndexId, (i + 1) * 10);
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
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[0].IndexId) - 1;

                        break;
                    }
                case EMove.Up:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[index - 1].IndexId) - 1;

                        break;
                    }

                case EMove.Down:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[index + 1].IndexId) + 1;

                        break;
                    }
                case EMove.Bottom:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[^1].IndexId) + 1;

                        break;
                    }
                case EMove.Position:
                    sort = (pos * 10) + 1;
                    break;
            }

            ProfileExHandler.Instance.SetSort(lstProfile[index].IndexId, sort);
            return 0;
        }

        /// <summary>
        /// 添加自定义服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddCustomServer(Config config, ProfileItem profileItem, bool blDelete)
        {
            var fileName = profileItem.Address;
            if (!File.Exists(fileName))
            {
                return -1;
            }
            var ext = Path.GetExtension(fileName);
            string newFileName = $"{Utils.GetGuid()}{ext}";
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
                Logging.SaveLog(ex.Message, ex);
                return -1;
            }

            profileItem.Address = newFileName;
            profileItem.ConfigType = EConfigType.Custom;
            if (Utils.IsNullOrEmpty(profileItem.Remarks))
            {
                profileItem.Remarks = $"import custom@{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}";
            }

            await AddServerCommon(config, profileItem, true);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> EditCustomServer(Config config, ProfileItem profileItem)
        {
            var item = await AppHandler.Instance.GetProfileItem(profileItem.IndexId);
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
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddShadowsocksServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigType = EConfigType.Shadowsocks;

            profileItem.Address = profileItem.Address.TrimEx();
            profileItem.Id = profileItem.Id.TrimEx();
            profileItem.Security = profileItem.Security.TrimEx();

            if (!AppHandler.Instance.GetShadowsocksSecurities(profileItem).Contains(profileItem.Security))
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
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddSocksServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigType = EConfigType.SOCKS;

            profileItem.Address = profileItem.Address.TrimEx();

            await AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddHttpServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigType = EConfigType.HTTP;

            profileItem.Address = profileItem.Address.TrimEx();

            await AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddTrojanServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigType = EConfigType.Trojan;

            profileItem.Address = profileItem.Address.TrimEx();
            profileItem.Id = profileItem.Id.TrimEx();
            if (Utils.IsNullOrEmpty(profileItem.StreamSecurity))
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
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddHysteria2Server(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigType = EConfigType.Hysteria2;
            profileItem.CoreType = ECoreType.sing_box;

            profileItem.Address = profileItem.Address.TrimEx();
            profileItem.Id = profileItem.Id.TrimEx();
            profileItem.Path = profileItem.Path.TrimEx();
            profileItem.Network = string.Empty;

            if (Utils.IsNullOrEmpty(profileItem.StreamSecurity))
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
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
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

            if (Utils.IsNullOrEmpty(profileItem.StreamSecurity))
            {
                profileItem.StreamSecurity = Global.StreamSecurity;
            }
            if (Utils.IsNullOrEmpty(profileItem.Alpn))
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
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static async Task<int> AddWireguardServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigType = EConfigType.WireGuard;
            profileItem.CoreType = ECoreType.sing_box;

            profileItem.Address = profileItem.Address.TrimEx();
            profileItem.Id = profileItem.Id.TrimEx();
            profileItem.PublicKey = profileItem.PublicKey.TrimEx();
            profileItem.Path = profileItem.Path.TrimEx();
            profileItem.RequestHost = profileItem.RequestHost.TrimEx();
            profileItem.Network = string.Empty;
            if (profileItem.ShortId.IsNullOrEmpty())
            {
                profileItem.ShortId = Global.TunMtus.FirstOrDefault();
            }

            if (profileItem.Id.IsNullOrEmpty())
            {
                return -1;
            }

            await AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        public static async Task<int> SortServers(Config config, string subId, string colName, bool asc)
        {
            var lstModel = await AppHandler.Instance.ProfileItems(subId, "");
            if (lstModel.Count <= 0)
            {
                return -1;
            }
            var lstProfileExs = await ProfileExHandler.Instance.GetProfileExs();
            var lstProfile = (from t in lstModel
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
                                  Delay = t33 == null ? 0 : t33.Delay,
                                  Speed = t33 == null ? 0 : t33.Speed,
                                  Sort = t33 == null ? 0 : t33.Sort
                              }).ToList();

            Enum.TryParse(colName, true, out EServerColName name);
            var propertyName = string.Empty;
            switch (name)
            {
                case EServerColName.ConfigType:
                case EServerColName.Remarks:
                case EServerColName.Address:
                case EServerColName.Port:
                case EServerColName.Network:
                case EServerColName.StreamSecurity:
                    propertyName = name.ToString();
                    break;

                case EServerColName.DelayVal:
                    propertyName = "Delay";
                    break;

                case EServerColName.SpeedVal:
                    propertyName = "Speed";
                    break;

                case EServerColName.SubRemarks:
                    propertyName = "Subid";
                    break;

                default:
                    return -1;
            }

            var items = lstProfile.AsQueryable();

            if (asc)
            {
                lstProfile = items.OrderBy(propertyName).ToList();
            }
            else
            {
                lstProfile = items.OrderByDescending(propertyName).ToList();
            }
            for (int i = 0; i < lstProfile.Count; i++)
            {
                ProfileExHandler.Instance.SetSort(lstProfile[i].IndexId, (i + 1) * 10);
            }
            if (name == EServerColName.DelayVal)
            {
                var maxSort = lstProfile.Max(t => t.Sort) + 10;
                foreach (var item in lstProfile)
                {
                    if (item.Delay <= 0)
                    {
                        ProfileExHandler.Instance.SetSort(item.IndexId, maxSort);
                    }
                }
            }
            if (name == EServerColName.SpeedVal)
            {
                var maxSort = lstProfile.Max(t => t.Sort) + 10;
                foreach (var item in lstProfile)
                {
                    if (item.Speed <= 0)
                    {
                        ProfileExHandler.Instance.SetSort(item.IndexId, maxSort);
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
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

            if (!Global.Flows.Contains(profileItem.Flow))
            {
                profileItem.Flow = Global.Flows.First();
            }
            if (profileItem.Id.IsNullOrEmpty())
            {
                return -1;
            }
            if (Utils.IsNotEmpty(profileItem.Security) && profileItem.Security != Global.None)
            {
                profileItem.Security = Global.None;
            }

            await AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        public static async Task<Tuple<int, int>> DedupServerList(Config config, string subId)
        {
            var lstProfile = await AppHandler.Instance.ProfileItems(subId);

            List<ProfileItem> lstKeep = new();
            List<ProfileItem> lstRemove = new();
            if (!config.GuiItem.KeepOlderDedupl) lstProfile.Reverse();

            foreach (ProfileItem item in lstProfile)
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
            await RemoveServer(config, lstRemove);

            return new Tuple<int, int>(lstProfile.Count, lstKeep.Count);
        }

        public static async Task<int> AddServerCommon(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.ConfigVersion = 2;

            if (Utils.IsNotEmpty(profileItem.StreamSecurity))
            {
                if (profileItem.StreamSecurity != Global.StreamSecurity
                     && profileItem.StreamSecurity != Global.StreamSecurityReality)
                {
                    profileItem.StreamSecurity = string.Empty;
                }
                else
                {
                    if (Utils.IsNullOrEmpty(profileItem.AllowInsecure))
                    {
                        profileItem.AllowInsecure = config.CoreBasicItem.DefAllowInsecure.ToString().ToLower();
                    }
                    if (Utils.IsNullOrEmpty(profileItem.Fingerprint) && profileItem.StreamSecurity == Global.StreamSecurityReality)
                    {
                        profileItem.Fingerprint = config.CoreBasicItem.DefFingerprint;
                    }
                }
            }

            if (Utils.IsNotEmpty(profileItem.Network) && !Global.Networks.Contains(profileItem.Network))
            {
                profileItem.Network = Global.DefaultNetwork;
            }

            var maxSort = -1;
            if (Utils.IsNullOrEmpty(profileItem.IndexId))
            {
                profileItem.IndexId = Utils.GetGuid(false);
                maxSort = ProfileExHandler.Instance.GetMaxSort();
            }
            if (!toFile && maxSort < 0)
            {
                maxSort = ProfileExHandler.Instance.GetMaxSort();
            }
            if (maxSort > 0)
            {
                ProfileExHandler.Instance.SetSort(profileItem.IndexId, maxSort + 1);
            }

            if (toFile)
            {
                await SQLiteHelper.Instance.ReplaceAsync(profileItem);
            }
            return 0;
        }

        private static bool CompareProfileItem(ProfileItem o, ProfileItem n, bool remarks)
        {
            if (o == null || n == null)
            {
                return false;
            }

            return o.ConfigType == n.ConfigType
                && o.Address == n.Address
                && o.Port == n.Port
                && o.Id == n.Id
                && o.Security == n.Security
                && o.Network == n.Network
                && o.HeaderType == n.HeaderType
                && o.RequestHost == n.RequestHost
                && o.Path == n.Path
                && (o.ConfigType == EConfigType.Trojan || o.StreamSecurity == n.StreamSecurity)
                && o.Flow == n.Flow
                && o.Sni == n.Sni
                && o.Alpn == n.Alpn
                && o.Fingerprint == n.Fingerprint
                && o.PublicKey == n.PublicKey
                && o.ShortId == n.ShortId
                && (!remarks || o.Remarks == n.Remarks);
        }

        private static async Task<int> RemoveProfileItem(Config config, string indexId)
        {
            try
            {
                var item = await AppHandler.Instance.GetProfileItem(indexId);
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
                Logging.SaveLog("Remove Item", ex);
            }

            return 0;
        }

        public static async Task<RetResult> AddCustomServer4Multiple(Config config, List<ProfileItem> selecteds, ECoreType coreType)
        {
            var indexId = Utils.GetMd5(Global.CoreMultipleLoadConfigFileName);
            var configPath = Utils.GetConfigPath(Global.CoreMultipleLoadConfigFileName);

            var result = await CoreConfigHandler.GenerateClientMultipleLoadConfig(config, configPath, selecteds, coreType);
            if (result.Success != true)
            {
                return result;
            }

            var fileName = configPath;
            if (!File.Exists(fileName))
            {
                return result;
            }

            var profileItem = await AppHandler.Instance.GetProfileItem(indexId) ?? new();
            profileItem.IndexId = indexId;
            profileItem.Remarks = coreType == ECoreType.sing_box ? ResUI.menuSetDefaultMultipleServer : ResUI.menuSetDefaultLoadBalanceServer;
            profileItem.Address = Global.CoreMultipleLoadConfigFileName;
            profileItem.ConfigType = EConfigType.Custom;
            profileItem.CoreType = coreType;

            await AddServerCommon(config, profileItem, true);

            result.Data = indexId;
            return result;
        }

        public static async Task<ProfileItem?> GetPreSocksItem(Config config, ProfileItem node, ECoreType coreType)
        {
            ProfileItem? itemSocks = null;
            var preCoreType = ECoreType.sing_box;
            if (node.ConfigType != EConfigType.Custom && coreType != ECoreType.sing_box && config.TunModeItem.EnableTun)
            {
                itemSocks = new ProfileItem()
                {
                    CoreType = preCoreType,
                    ConfigType = EConfigType.SOCKS,
                    Address = Global.Loopback,
                    Sni = node.Address, //Tun2SocksAddress
                    Port = AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)
                };
            }
            else if ((node.ConfigType == EConfigType.Custom && node.PreSocksPort > 0))
            {
                preCoreType = config.TunModeItem.EnableTun ? ECoreType.sing_box : ECoreType.Xray;
                itemSocks = new ProfileItem()
                {
                    CoreType = preCoreType,
                    ConfigType = EConfigType.SOCKS,
                    Address = Global.Loopback,
                    Port = node.PreSocksPort.Value,
                };
            }

            return itemSocks;
        }

        #endregion Server

        #region Batch add servers

        /// <summary>
        /// 批量添加服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="strData"></param>
        /// <param name="subid"></param>
        /// <returns>成功导入的数量</returns>
        private static async Task<int> AddBatchServersCommon(Config config, string strData, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            var subFilter = string.Empty;
            //remove sub items
            if (isSub && Utils.IsNotEmpty(subid))
            {
                await RemoveServerViaSubid(config, subid, isSub);
                subFilter = (await AppHandler.Instance.GetSubItem(subid))?.Filter ?? "";
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
                var profileItem = FmtHandler.ResolveConfig(str, out string msg);
                if (profileItem is null)
                {
                    continue;
                }

                //exist sub items //filter
                if (isSub && Utils.IsNotEmpty(subid) && Utils.IsNotEmpty(subFilter))
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

        private static async Task<int> AddBatchServers4Custom(Config config, string strData, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            var subItem = await AppHandler.Instance.GetSubItem(subid);
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
                if (isSub && Utils.IsNotEmpty(subid))
                {
                    await RemoveServerViaSubid(config, subid, isSub);
                }
                int count = 0;
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
            if (profileItem is null)
            {
                profileItem = Hysteria2Fmt.ResolveFull(strData, subRemarks);
            }
            //Is naiveproxy configuration
            if (profileItem is null)
            {
                profileItem = NaiveproxyFmt.ResolveFull(strData, subRemarks);
            }
            if (profileItem is null || Utils.IsNullOrEmpty(profileItem.Address))
            {
                return -1;
            }

            if (isSub && Utils.IsNotEmpty(subid))
            {
                await RemoveServerViaSubid(config, subid, isSub);
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

        private static async Task<int> AddBatchServers4SsSIP008(Config config, string strData, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            if (isSub && Utils.IsNotEmpty(subid))
            {
                await RemoveServerViaSubid(config, subid, isSub);
            }

            var lstSsServer = ShadowsocksFmt.ResolveSip008(strData);
            if (lstSsServer?.Count > 0)
            {
                int counter = 0;
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

        public static async Task<int> AddBatchServers(Config config, string strData, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }
            List<ProfileItem>? lstOriSub = null;
            ProfileItem? activeProfile = null;
            if (isSub && Utils.IsNotEmpty(subid))
            {
                lstOriSub = await AppHandler.Instance.ProfileItems(subid);
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
                var lstSub = await AppHandler.Instance.ProfileItems(subid);
                var existItem = lstSub?.FirstOrDefault(t => config.UiItem.EnableUpdateSubOnlyRemarksExist ? t.Remarks == activeProfile.Remarks : CompareProfileItem(t, activeProfile, true));
                if (existItem != null)
                {
                    await ConfigHandler.SetDefaultServerIndex(config, existItem.IndexId);
                }
            }

            //Keep the last traffic statistics
            if (lstOriSub != null)
            {
                var lstSub = await AppHandler.Instance.ProfileItems(subid);
                foreach (var item in lstSub)
                {
                    var existItem = lstOriSub?.FirstOrDefault(t => config.UiItem.EnableUpdateSubOnlyRemarksExist ? t.Remarks == item.Remarks : CompareProfileItem(t, item, true));
                    if (existItem != null)
                    {
                        await StatisticsHandler.Instance.CloneServerStatItem(existItem.IndexId, item.IndexId);
                    }
                }
            }

            return counter;
        }

        #endregion Batch add servers

        #region Sub & Group

        /// <summary>
        /// add sub
        /// </summary>
        /// <param name="config"></param>
        /// <param name="url"></param>
        /// <returns></returns>
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
            if (uri == null) return -1;
            //Do not allow http protocol
            if (url.StartsWith(Global.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
            {
                //TODO Temporary reminder to be removed later
                NoticeHandler.Instance.Enqueue(ResUI.InsecureUrlProtocol);
                //return -1;
            }

            var queryVars = Utils.ParseQueryString(uri.Query);
            subItem.Remarks = queryVars["remarks"] ?? "import_sub";

            return await AddSubItem(config, subItem);
        }

        public static async Task<int> AddSubItem(Config config, SubItem subItem)
        {
            var item = await AppHandler.Instance.GetSubItem(subItem.Id);
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

            if (Utils.IsNullOrEmpty(item.Id))
            {
                item.Id = Utils.GetGuid(false);

                if (item.Sort <= 0)
                {
                    var maxSort = 0;
                    if (await SQLiteHelper.Instance.TableAsync<SubItem>().CountAsync() > 0)
                    {
                        var lstSubs = (await AppHandler.Instance.SubItems());
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
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="subid"></param>
        /// <returns></returns>
        public static async Task<int> RemoveServerViaSubid(Config config, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(subid))
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

        public static async Task<int> DeleteSubItem(Config config, string id)
        {
            var item = await AppHandler.Instance.GetSubItem(id);
            if (item is null)
            {
                return 0;
            }
            await SQLiteHelper.Instance.DeleteAsync(item);
            await RemoveServerViaSubid(config, id, false);

            return 0;
        }

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

        public static async Task<int> SaveRoutingItem(Config config, RoutingItem item)
        {
            if (Utils.IsNullOrEmpty(item.Id))
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
        /// AddBatchRoutingRules
        /// </summary>
        /// <param name="config"></param>
        /// <param name="strData"></param>
        /// <returns></returns>
        public static async Task<int> AddBatchRoutingRules(RoutingItem routingItem, string strData)
        {
            if (Utils.IsNullOrEmpty(strData))
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

            if (Utils.IsNullOrEmpty(routingItem.Id))
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
        /// MoveRoutingRule
        /// </summary>
        /// <param name="routingItem"></param>
        /// <param name="index"></param>
        /// <param name="eMove"></param>
        /// <returns></returns>
        public static async Task<int> MoveRoutingRule(List<RulesItem> rules, int index, EMove eMove, int pos = -1)
        {
            int count = rules.Count;
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
            return 0;
        }

        public static async Task<int> SetDefaultRouting(Config config, RoutingItem routingItem)
        {
            if (await SQLiteHelper.Instance.TableAsync<RoutingItem>().Where(t => t.Id == routingItem.Id).CountAsync() > 0)
            {
                config.RoutingBasicItem.RoutingIndexId = routingItem.Id;
            }

            await SaveConfig(config);

            return 0;
        }

        public static async Task<RoutingItem> GetDefaultRouting(Config config)
        {
            var item = await AppHandler.Instance.GetRoutingItem(config.RoutingBasicItem.RoutingIndexId);
            if (item is null)
            {
                var item2 = await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync();
                await SetDefaultRouting(config, item2);
                return item2;
            }

            return item;
        }

        public static async Task<int> InitRouting(Config config, bool blImportAdvancedRules = false)
        {
            if (string.IsNullOrEmpty(config.ConstItem.RouteRulesTemplateSourceUrl))
            {
                await InitBuiltinRouting(config, blImportAdvancedRules);
            }
            else
            {
                await InitExternalRouting(config, blImportAdvancedRules);
            }

            return 0;
        }

        public static async Task<int> InitExternalRouting(Config config, bool blImportAdvancedRules = false)
        {
            var downloadHandle = new DownloadService();
            var templateContent = await downloadHandle.TryDownloadString(config.ConstItem.RouteRulesTemplateSourceUrl, true, "");
            if (string.IsNullOrEmpty(templateContent))
                return await InitBuiltinRouting(config, blImportAdvancedRules); // fallback

            var template = JsonUtils.Deserialize<RoutingTemplate>(templateContent);
            if (template == null)
                return await InitBuiltinRouting(config, blImportAdvancedRules); // fallback

            var items = await AppHandler.Instance.RoutingItems();
            var maxSort = items.Count;
            if (!blImportAdvancedRules && items.Where(t => t.Remarks.StartsWith(template.Version)).ToList().Count > 0)
            {
                return 0;
            }
            for (var i = 0; i < template.RoutingItems.Length; i++)
            {
                var item = template.RoutingItems[i];

                if (string.IsNullOrEmpty(item.Url) && string.IsNullOrEmpty(item.RuleSet))
                    continue;

                var ruleSetsString = !string.IsNullOrEmpty(item.RuleSet)
                    ? item.RuleSet
                    : await downloadHandle.TryDownloadString(item.Url, true, "");

                if (string.IsNullOrEmpty(ruleSetsString))
                    continue;

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

        public static async Task<int> InitBuiltinRouting(Config config, bool blImportAdvancedRules = false)
        {
            var ver = "V3-";
            var items = await AppHandler.Instance.RoutingItems();

            //TODO Temporary code to be removed later
            var lockItem = items?.FirstOrDefault(t => t.Locked == true);
            if (lockItem != null)
            {
                await ConfigHandler.RemoveRoutingItem(lockItem);
                items = await AppHandler.Instance.RoutingItems();
            }

            if (!blImportAdvancedRules && items.Where(t => t.Remarks.StartsWith(ver)).ToList().Count > 0)
            {
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
            await AddBatchRoutingRules(item2, Utils.GetEmbedText(Global.CustomRoutingFileName + "white"));

            //Blacklist
            var item3 = new RoutingItem()
            {
                Remarks = $"{ver}黑名单(Blacklist)",
                Url = string.Empty,
                Sort = maxSort + 2,
            };
            await AddBatchRoutingRules(item3, Utils.GetEmbedText(Global.CustomRoutingFileName + "black"));

            //Global
            var item1 = new RoutingItem()
            {
                Remarks = $"{ver}全局(Global)",
                Url = string.Empty,
                Sort = maxSort + 3,
            };
            await AddBatchRoutingRules(item1, Utils.GetEmbedText(Global.CustomRoutingFileName + "global"));

            if (!blImportAdvancedRules)
            {
                await SetDefaultRouting(config, item2);
            }
            return 0;
        }

        public static async Task RemoveRoutingItem(RoutingItem routingItem)
        {
            await SQLiteHelper.Instance.DeleteAsync(routingItem);
        }

        #endregion Routing

        #region DNS

        public static async Task<int> InitBuiltinDNS(Config config)
        {
            var items = await AppHandler.Instance.DNSItems();
            if (items.Count <= 0)
            {
                var item = new DNSItem()
                {
                    Remarks = "V2ray",
                    CoreType = ECoreType.Xray,
                };
                await SaveDNSItems(config, item);

                var item2 = new DNSItem()
                {
                    Remarks = "sing-box",
                    CoreType = ECoreType.sing_box,
                };
                await SaveDNSItems(config, item2);
            }

            return 0;
        }

        public static async Task<int> SaveDNSItems(Config config, DNSItem item)
        {
            if (item == null)
            {
                return -1;
            }

            if (Utils.IsNullOrEmpty(item.Id))
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

        public static async Task<DNSItem> GetExternalDNSItem(ECoreType type, string url)
        {
            var currentItem = await AppHandler.Instance.GetDNSItem(type);

            var downloadHandle = new DownloadService();
            var templateContent = await downloadHandle.TryDownloadString(url, true, "");
            if (string.IsNullOrEmpty(templateContent))
                return currentItem;

            var template = JsonUtils.Deserialize<DNSItem>(templateContent);
            if (template == null)
                return currentItem;

            if (!string.IsNullOrEmpty(template.NormalDNS))
                template.NormalDNS = await downloadHandle.TryDownloadString(template.NormalDNS, true, "");

            if (!string.IsNullOrEmpty(template.TunDNS))
                template.TunDNS = await downloadHandle.TryDownloadString(template.TunDNS, true, "");

            template.Id = currentItem.Id;
            template.Enabled = currentItem.Enabled;
            template.Remarks = currentItem.Remarks;
            template.CoreType = type;

            return template;
        }

        #endregion DNS

        #region Regional Presets

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

                    return true;

                case EPresetType.Russia:
                    config.ConstItem.GeoSourceUrl = Global.GeoFilesSources[1];
                    config.ConstItem.SrsSourceUrl = Global.SingboxRulesetSources[1];
                    config.ConstItem.RouteRulesTemplateSourceUrl = Global.RoutingRulesSources[1];

                    await SaveDNSItems(config, await GetExternalDNSItem(ECoreType.Xray, Global.DNSTemplateSources[1] + "v2ray.json"));
                    await SaveDNSItems(config, await GetExternalDNSItem(ECoreType.sing_box, Global.DNSTemplateSources[1] + "sing_box.json"));

                    return true;
            }

            return false;
        }

        #endregion Regional Presets
    }
}