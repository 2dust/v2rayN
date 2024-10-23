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
            config.RoutingBasicItem ??= new()
            {
                EnableRoutingAdvanced = true
            };

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
            if (config.UiItem.MainColumnItem == null)
            {
                config.UiItem.MainColumnItem = new();
            }
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

            return config;
        }

        /// <summary>
        /// 保参数
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static async Task<int> SaveConfig(Config config, bool reload = true)
        {
            await ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="config"></param>
        private static async Task ToJsonFile(Config config)
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
                        return;
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
                }
            }
        }

        #endregion ConfigHandler

        #region Server

        public static async Task<int> AddServer(Config config, ProfileItem profileItem)
        {
            var item = await AppHandler.Instance.GetProfileItem(profileItem.indexId);
            if (item is null)
            {
                item = profileItem;
            }
            else
            {
                item.coreType = profileItem.coreType;
                item.remarks = profileItem.remarks;
                item.address = profileItem.address;
                item.port = profileItem.port;

                item.id = profileItem.id;
                item.alterId = profileItem.alterId;
                item.security = profileItem.security;
                item.flow = profileItem.flow;

                item.network = profileItem.network;
                item.headerType = profileItem.headerType;
                item.requestHost = profileItem.requestHost;
                item.path = profileItem.path;

                item.streamSecurity = profileItem.streamSecurity;
                item.sni = profileItem.sni;
                item.allowInsecure = profileItem.allowInsecure;
                item.fingerprint = profileItem.fingerprint;
                item.alpn = profileItem.alpn;

                item.publicKey = profileItem.publicKey;
                item.shortId = profileItem.shortId;
                item.spiderX = profileItem.spiderX;
            }

            var ret = item.configType switch
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
            profileItem.configType = EConfigType.VMess;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.security = profileItem.security.TrimEx();
            profileItem.network = profileItem.network.TrimEx();
            profileItem.headerType = profileItem.headerType.TrimEx();
            profileItem.requestHost = profileItem.requestHost.TrimEx();
            profileItem.path = profileItem.path.TrimEx();
            profileItem.streamSecurity = profileItem.streamSecurity.TrimEx();

            if (!Global.VmessSecurities.Contains(profileItem.security))
            {
                return -1;
            }
            if (profileItem.id.IsNullOrEmpty())
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
                item.subid = subid;
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
                var item = await AppHandler.Instance.GetProfileItem(it.indexId);
                if (item is null)
                {
                    continue;
                }

                var profileItem = JsonUtils.DeepCopy(item);
                profileItem.indexId = string.Empty;
                profileItem.remarks = $"{item.remarks}-clone";

                if (profileItem.configType == EConfigType.Custom)
                {
                    profileItem.address = Utils.GetConfigPath(profileItem.address);
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

            await ToJsonFile(config);

            return 0;
        }

        public static async Task<int> SetDefaultServer(Config config, List<ProfileItemModel> lstProfile)
        {
            if (lstProfile.Exists(t => t.indexId == config.IndexId))
            {
                return 0;
            }
            var count = await SQLiteHelper.Instance.TableAsync<ProfileItem>().CountAsync(t => t.indexId == config.IndexId);
            if (count > 0)
            {
                return 0;
            }
            if (lstProfile.Count > 0)
            {
                return await SetDefaultServerIndex(config, lstProfile.FirstOrDefault(t => t.port > 0)?.indexId);
            }

            var item = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.port > 0).FirstOrDefaultAsync();
            return await SetDefaultServerIndex(config, item.indexId);
        }

        public static async Task<ProfileItem?> GetDefaultServer(Config config)
        {
            var item = await AppHandler.Instance.GetProfileItem(config.IndexId);
            if (item is null)
            {
                var item2 = await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync();
                await SetDefaultServerIndex(config, item2?.indexId);
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
                ProfileExHandler.Instance.SetSort(lstProfile[i].indexId, (i + 1) * 10);
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
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[0].indexId) - 1;

                        break;
                    }
                case EMove.Up:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[index - 1].indexId) - 1;

                        break;
                    }

                case EMove.Down:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[index + 1].indexId) + 1;

                        break;
                    }
                case EMove.Bottom:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        sort = ProfileExHandler.Instance.GetSort(lstProfile[^1].indexId) + 1;

                        break;
                    }
                case EMove.Position:
                    sort = pos * 10 + 1;
                    break;
            }

            ProfileExHandler.Instance.SetSort(lstProfile[index].indexId, sort);
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
            var fileName = profileItem.address;
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

            profileItem.address = newFileName;
            profileItem.configType = EConfigType.Custom;
            if (Utils.IsNullOrEmpty(profileItem.remarks))
            {
                profileItem.remarks = $"import custom@{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}";
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
            var item = await AppHandler.Instance.GetProfileItem(profileItem.indexId);
            if (item is null)
            {
                item = profileItem;
            }
            else
            {
                item.remarks = profileItem.remarks;
                item.address = profileItem.address;
                item.coreType = profileItem.coreType;
                item.displayLog = profileItem.displayLog;
                item.preSocksPort = profileItem.preSocksPort;
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
            profileItem.configType = EConfigType.Shadowsocks;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.security = profileItem.security.TrimEx();

            if (!AppHandler.Instance.GetShadowsocksSecurities(profileItem).Contains(profileItem.security))
            {
                return -1;
            }
            if (profileItem.id.IsNullOrEmpty())
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
            profileItem.configType = EConfigType.SOCKS;

            profileItem.address = profileItem.address.TrimEx();

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
            profileItem.configType = EConfigType.HTTP;

            profileItem.address = profileItem.address.TrimEx();

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
            profileItem.configType = EConfigType.Trojan;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            if (Utils.IsNullOrEmpty(profileItem.streamSecurity))
            {
                profileItem.streamSecurity = Global.StreamSecurity;
            }
            if (profileItem.id.IsNullOrEmpty())
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
            profileItem.configType = EConfigType.Hysteria2;
            profileItem.coreType = ECoreType.sing_box;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.path = profileItem.path.TrimEx();
            profileItem.network = string.Empty;

            if (Utils.IsNullOrEmpty(profileItem.streamSecurity))
            {
                profileItem.streamSecurity = Global.StreamSecurity;
            }
            if (profileItem.id.IsNullOrEmpty())
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
            profileItem.configType = EConfigType.TUIC;
            profileItem.coreType = ECoreType.sing_box;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.security = profileItem.security.TrimEx();
            profileItem.network = string.Empty;

            if (!Global.TuicCongestionControls.Contains(profileItem.headerType))
            {
                profileItem.headerType = Global.TuicCongestionControls.FirstOrDefault()!;
            }

            if (Utils.IsNullOrEmpty(profileItem.streamSecurity))
            {
                profileItem.streamSecurity = Global.StreamSecurity;
            }
            if (Utils.IsNullOrEmpty(profileItem.alpn))
            {
                profileItem.alpn = "h3";
            }
            if (profileItem.id.IsNullOrEmpty())
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
            profileItem.configType = EConfigType.WireGuard;
            profileItem.coreType = ECoreType.sing_box;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.publicKey = profileItem.publicKey.TrimEx();
            profileItem.path = profileItem.path.TrimEx();
            profileItem.requestHost = profileItem.requestHost.TrimEx();
            profileItem.network = string.Empty;
            if (profileItem.shortId.IsNullOrEmpty())
            {
                profileItem.shortId = Global.TunMtus.FirstOrDefault();
            }

            if (profileItem.id.IsNullOrEmpty())
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
            var lstProfileExs = ProfileExHandler.Instance.ProfileExs;
            var lstProfile = (from t in lstModel
                              join t3 in lstProfileExs on t.indexId equals t3.indexId into t3b
                              from t33 in t3b.DefaultIfEmpty()
                              select new ProfileItemModel
                              {
                                  indexId = t.indexId,
                                  configType = t.configType,
                                  remarks = t.remarks,
                                  address = t.address,
                                  port = t.port,
                                  security = t.security,
                                  network = t.network,
                                  streamSecurity = t.streamSecurity,
                                  delay = t33 == null ? 0 : t33.delay,
                                  speed = t33 == null ? 0 : t33.speed,
                                  sort = t33 == null ? 0 : t33.sort
                              }).ToList();

            Enum.TryParse(colName, true, out EServerColName name);
            var propertyName = string.Empty;
            switch (name)
            {
                case EServerColName.configType:
                case EServerColName.remarks:
                case EServerColName.address:
                case EServerColName.port:
                case EServerColName.network:
                case EServerColName.streamSecurity:
                    propertyName = name.ToString();
                    break;

                case EServerColName.delayVal:
                    propertyName = "delay";
                    break;

                case EServerColName.speedVal:
                    propertyName = "speed";
                    break;

                case EServerColName.subRemarks:
                    propertyName = "subid";
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
                ProfileExHandler.Instance.SetSort(lstProfile[i].indexId, (i + 1) * 10);
            }
            if (name == EServerColName.delayVal)
            {
                var maxSort = lstProfile.Max(t => t.sort) + 10;
                foreach (var item in lstProfile)
                {
                    if (item.delay <= 0)
                    {
                        ProfileExHandler.Instance.SetSort(item.indexId, maxSort);
                    }
                }
            }
            if (name == EServerColName.speedVal)
            {
                var maxSort = lstProfile.Max(t => t.sort) + 10;
                foreach (var item in lstProfile)
                {
                    if (item.speed <= 0)
                    {
                        ProfileExHandler.Instance.SetSort(item.indexId, maxSort);
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
            profileItem.configType = EConfigType.VLESS;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.security = profileItem.security.TrimEx();
            profileItem.network = profileItem.network.TrimEx();
            profileItem.headerType = profileItem.headerType.TrimEx();
            profileItem.requestHost = profileItem.requestHost.TrimEx();
            profileItem.path = profileItem.path.TrimEx();
            profileItem.streamSecurity = profileItem.streamSecurity.TrimEx();

            if (!Global.Flows.Contains(profileItem.flow))
            {
                profileItem.flow = Global.Flows.First();
            }
            if (profileItem.id.IsNullOrEmpty())
            {
                return -1;
            }
            if (Utils.IsNotEmpty(profileItem.security) && profileItem.security != Global.None)
            {
                profileItem.security = Global.None;
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
            profileItem.configVersion = 2;

            if (Utils.IsNotEmpty(profileItem.streamSecurity))
            {
                if (profileItem.streamSecurity != Global.StreamSecurity
                     && profileItem.streamSecurity != Global.StreamSecurityReality)
                {
                    profileItem.streamSecurity = string.Empty;
                }
                else
                {
                    if (Utils.IsNullOrEmpty(profileItem.allowInsecure))
                    {
                        profileItem.allowInsecure = config.CoreBasicItem.DefAllowInsecure.ToString().ToLower();
                    }
                    if (Utils.IsNullOrEmpty(profileItem.fingerprint) && profileItem.streamSecurity == Global.StreamSecurityReality)
                    {
                        profileItem.fingerprint = config.CoreBasicItem.DefFingerprint;
                    }
                }
            }

            if (Utils.IsNotEmpty(profileItem.network) && !Global.Networks.Contains(profileItem.network))
            {
                profileItem.network = Global.DefaultNetwork;
            }

            var maxSort = -1;
            if (Utils.IsNullOrEmpty(profileItem.indexId))
            {
                profileItem.indexId = Utils.GetGuid(false);
                maxSort = ProfileExHandler.Instance.GetMaxSort();
            }
            if (!toFile && maxSort < 0)
            {
                maxSort = ProfileExHandler.Instance.GetMaxSort();
            }
            if (maxSort > 0)
            {
                ProfileExHandler.Instance.SetSort(profileItem.indexId, maxSort + 1);
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

            return o.configType == n.configType
                && o.address == n.address
                && o.port == n.port
                && o.id == n.id
                && o.alterId == n.alterId
                && o.security == n.security
                && o.network == n.network
                && o.headerType == n.headerType
                && o.requestHost == n.requestHost
                && o.path == n.path
                && (o.configType == EConfigType.Trojan || o.streamSecurity == n.streamSecurity)
                && o.flow == n.flow
                && o.sni == n.sni
                && (!remarks || o.remarks == n.remarks);
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
                if (item.configType == EConfigType.Custom)
                {
                    File.Delete(Utils.GetConfigPath(item.address));
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
            profileItem.indexId = indexId;
            profileItem.remarks = coreType == ECoreType.sing_box ? ResUI.menuSetDefaultMultipleServer : ResUI.menuSetDefaultLoadBalanceServer;
            profileItem.address = Global.CoreMultipleLoadConfigFileName;
            profileItem.configType = EConfigType.Custom;
            profileItem.coreType = coreType;

            await AddServerCommon(config, profileItem, true);

            result.Data = indexId;
            return result;
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
        private static async Task<int> AddBatchServers(Config config, string strData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            string subFilter = string.Empty;
            //remove sub items
            if (isSub && Utils.IsNotEmpty(subid))
            {
                await RemoveServerViaSubid(config, subid, isSub);
                subFilter = (await AppHandler.Instance.GetSubItem(subid))?.filter ?? "";
            }

            int countServers = 0;
            //Check for duplicate indexId
            List<string>? lstDbIndexId = null;
            List<ProfileItem> lstAdd = new();
            var arrData = strData.Split(Environment.NewLine.ToCharArray()).Where(t => !t.IsNullOrEmpty());
            if (isSub)
            {
                arrData = arrData.Distinct();
            }
            foreach (string str in arrData)
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

                //exist sub items
                if (isSub && Utils.IsNotEmpty(subid))
                {
                    var existItem = lstOriSub?.FirstOrDefault(t => t.isSub == isSub
                                                && config.UiItem.EnableUpdateSubOnlyRemarksExist ? t.remarks == profileItem.remarks : CompareProfileItem(t, profileItem, true));
                    if (existItem != null)
                    {
                        //Check for duplicate indexId
                        if (lstDbIndexId is null)
                        {
                            lstDbIndexId = await AppHandler.Instance.ProfileItemIndexes("");
                        }
                        if (lstAdd.Any(t => t.indexId == existItem.indexId)
                            || lstDbIndexId.Any(t => t == existItem.indexId))
                        {
                            profileItem.indexId = string.Empty;
                        }
                        else
                        {
                            profileItem.indexId = existItem.indexId;
                        }
                    }
                    //filter
                    if (Utils.IsNotEmpty(subFilter))
                    {
                        if (!Regex.IsMatch(profileItem.remarks, subFilter))
                        {
                            continue;
                        }
                    }
                }
                profileItem.subid = subid;
                profileItem.isSub = isSub;

                var addStatus = profileItem.configType switch
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

            await ToJsonFile(config);
            return countServers;
        }

        private static async Task<int> AddBatchServers4Custom(Config config, string strData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            var subItem = await AppHandler.Instance.GetSubItem(subid);
            var subRemarks = subItem?.remarks;
            var preSocksPort = subItem?.preSocksPort;

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
                    it.subid = subid;
                    it.isSub = isSub;
                    it.preSocksPort = preSocksPort;
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
            if (profileItem is null || Utils.IsNullOrEmpty(profileItem.address))
            {
                return -1;
            }

            if (isSub && Utils.IsNotEmpty(subid))
            {
                await RemoveServerViaSubid(config, subid, isSub);
            }
            if (isSub && lstOriSub?.Count == 1)
            {
                profileItem.indexId = lstOriSub[0].indexId;
            }
            profileItem.subid = subid;
            profileItem.isSub = isSub;
            profileItem.preSocksPort = preSocksPort;
            if (await AddCustomServer(config, profileItem, true) == 0)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }

        private static async Task<int> AddBatchServers4SsSIP008(Config config, string strData, string subid, bool isSub, List<ProfileItem> lstOriSub)
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
                    ssItem.subid = subid;
                    ssItem.isSub = isSub;
                    if (await AddShadowsocksServer(config, ssItem) == 0)
                    {
                        counter++;
                    }
                }
                await ToJsonFile(config);
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
            if (isSub && Utils.IsNotEmpty(subid))
            {
                lstOriSub = await AppHandler.Instance.ProfileItems(subid);
            }

            var counter = 0;
            if (Utils.IsBase64String(strData))
            {
                counter = await AddBatchServers(config, Utils.Base64Decode(strData), subid, isSub, lstOriSub);
            }
            if (counter < 1)
            {
                counter = await AddBatchServers(config, strData, subid, isSub, lstOriSub);
            }
            if (counter < 1)
            {
                counter = await AddBatchServers(config, Utils.Base64Decode(strData), subid, isSub, lstOriSub);
            }

            if (counter < 1)
            {
                counter = await AddBatchServers4SsSIP008(config, strData, subid, isSub, lstOriSub);
            }

            //maybe other sub
            if (counter < 1)
            {
                counter = await AddBatchServers4Custom(config, strData, subid, isSub, lstOriSub);
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
            var count = await SQLiteHelper.Instance.TableAsync<SubItem>().CountAsync(e => e.url == url);
            if (count > 0)
            {
                return 0;
            }
            SubItem subItem = new()
            {
                id = string.Empty,
                url = url
            };

            try
            {
                var uri = new Uri(url);
                var queryVars = Utils.ParseQueryString(uri.Query);
                subItem.remarks = queryVars["remarks"] ?? "import_sub";
            }
            catch (UriFormatException)
            {
                return 0;
            }

            return await AddSubItem(config, subItem);
        }

        public static async Task<int> AddSubItem(Config config, SubItem subItem)
        {
            var item = await AppHandler.Instance.GetSubItem(subItem.id);
            if (item is null)
            {
                item = subItem;
            }
            else
            {
                item.remarks = subItem.remarks;
                item.url = subItem.url;
                item.moreUrl = subItem.moreUrl;
                item.enabled = subItem.enabled;
                item.autoUpdateInterval = subItem.autoUpdateInterval;
                item.userAgent = subItem.userAgent;
                item.sort = subItem.sort;
                item.filter = subItem.filter;
                item.updateTime = subItem.updateTime;
                item.convertTarget = subItem.convertTarget;
                item.prevProfile = subItem.prevProfile;
                item.nextProfile = subItem.nextProfile;
                item.preSocksPort = subItem.preSocksPort;
            }

            if (Utils.IsNullOrEmpty(item.id))
            {
                item.id = Utils.GetGuid(false);

                if (item.sort <= 0)
                {
                    var maxSort = 0;
                    if (await SQLiteHelper.Instance.TableAsync<SubItem>().CountAsync() > 0)
                    {
                        var lstSubs = (await AppHandler.Instance.SubItems());
                        maxSort = lstSubs.LastOrDefault()?.sort ?? 0;
                    }
                    item.sort = maxSort + 1;
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
            var customProfile = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.subid == subid && t.configType == EConfigType.Custom).ToListAsync();
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
                File.Delete(Utils.GetConfigPath(item.address));
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
                item.subid = subid;
            }
            await SQLiteHelper.Instance.UpdateAllAsync(lstProfile);

            return 0;
        }

        #endregion Sub & Group

        #region Routing

        public static async Task<int> SaveRoutingItem(Config config, RoutingItem item)
        {
            if (Utils.IsNullOrEmpty(item.id))
            {
                item.id = Utils.GetGuid(false);
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
                item.id = Utils.GetGuid(false);
            }
            routingItem.ruleNum = lstRules.Count;
            routingItem.ruleSet = JsonUtils.Serialize(lstRules, false);

            if (Utils.IsNullOrEmpty(routingItem.id))
            {
                routingItem.id = Utils.GetGuid(false);
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
            if (await SQLiteHelper.Instance.TableAsync<RoutingItem>().Where(t => t.id == routingItem.id).CountAsync() > 0)
            {
                config.RoutingBasicItem.RoutingIndexId = routingItem.id;
            }

            await ToJsonFile(config);

            return 0;
        }

        public static async Task<RoutingItem> GetDefaultRouting(Config config)
        {
            var item = await AppHandler.Instance.GetRoutingItem(config.RoutingBasicItem.RoutingIndexId);
            if (item is null)
            {
                var item2 = await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync(t => t.locked == false);
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
            if (!blImportAdvancedRules && items.Where(t => t.remarks.StartsWith(template.version)).ToList().Count > 0)
            {
                return 0;
            }
            for (var i = 0; i < template.routingItems.Length; i++)
            {
                var item = template.routingItems[i];

                if (string.IsNullOrEmpty(item.url) && string.IsNullOrEmpty(item.ruleSet))
                    continue;

                var ruleSetsString = !string.IsNullOrEmpty(item.ruleSet)
                    ? item.ruleSet
                    : await downloadHandle.TryDownloadString(item.url, true, "");

                if (string.IsNullOrEmpty(ruleSetsString))
                    continue;

                item.remarks = $"{template.version}-{item.remarks}";
                item.enabled = true;
                item.sort = ++maxSort;
                item.url = string.Empty;

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
            if (!blImportAdvancedRules && items.Where(t => t.remarks.StartsWith(ver)).ToList().Count > 0)
            {
                return 0;
            }

            var maxSort = items.Count;
            //Bypass the mainland
            var item2 = new RoutingItem()
            {
                remarks = $"{ver}绕过大陆(Whitelist)",
                url = string.Empty,
                sort = maxSort + 1,
            };
            await AddBatchRoutingRules(item2, Utils.GetEmbedText(Global.CustomRoutingFileName + "white"));

            //Blacklist
            var item3 = new RoutingItem()
            {
                remarks = $"{ver}黑名单(Blacklist)",
                url = string.Empty,
                sort = maxSort + 2,
            };
            await AddBatchRoutingRules(item3, Utils.GetEmbedText(Global.CustomRoutingFileName + "black"));

            //Global
            var item1 = new RoutingItem()
            {
                remarks = $"{ver}全局(Global)",
                url = string.Empty,
                sort = maxSort + 3,
            };
            await AddBatchRoutingRules(item1, Utils.GetEmbedText(Global.CustomRoutingFileName + "global"));

            if (!blImportAdvancedRules)
            {
                await SetDefaultRouting(config, item2);
            }
            return 0;
        }

        public static async Task<RoutingItem?> GetLockedRoutingItem(Config config)
        {
            return await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync(it => it.locked == true);
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
                    remarks = "V2ray",
                    coreType = ECoreType.Xray,
                };
                await SaveDNSItems(config, item);

                var item2 = new DNSItem()
                {
                    remarks = "sing-box",
                    coreType = ECoreType.sing_box,
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

            if (Utils.IsNullOrEmpty(item.id))
            {
                item.id = Utils.GetGuid(false);
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

            if (!string.IsNullOrEmpty(template.normalDNS))
                template.normalDNS = await downloadHandle.TryDownloadString(template.normalDNS, true, "");

            if (!string.IsNullOrEmpty(template.tunDNS))
                template.tunDNS = await downloadHandle.TryDownloadString(template.tunDNS, true, "");

            template.id = currentItem.id;
            template.enabled = currentItem.enabled;
            template.remarks = currentItem.remarks;
            template.coreType = type;

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