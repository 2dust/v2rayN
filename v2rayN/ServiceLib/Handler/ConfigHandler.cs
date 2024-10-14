﻿using System.Data;
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
        public static int LoadConfig(ref Config? config)
        {
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
                    return -1;
                }
            }

            config ??= new Config();

            config.coreBasicItem ??= new()
            {
                logEnabled = false,
                loglevel = "warning",
                muxEnabled = false,
            };

            if (config.inbound == null)
            {
                config.inbound = new List<InItem>();
                InItem inItem = new()
                {
                    protocol = EInboundProtocol.socks.ToString(),
                    localPort = 10808,
                    udpEnabled = true,
                    sniffingEnabled = true,
                    routeOnly = false,
                };

                config.inbound.Add(inItem);
            }
            else
            {
                if (config.inbound.Count > 0)
                {
                    config.inbound[0].protocol = EInboundProtocol.socks.ToString();
                }
            }
            config.routingBasicItem ??= new()
            {
                enableRoutingAdvanced = true
            };

            if (Utils.IsNullOrEmpty(config.routingBasicItem.domainStrategy))
            {
                config.routingBasicItem.domainStrategy = Global.DomainStrategies[0];//"IPIfNonMatch";
            }

            config.kcpItem ??= new KcpItem
            {
                mtu = 1350,
                tti = 50,
                uplinkCapacity = 12,
                downlinkCapacity = 100,
                readBufferSize = 2,
                writeBufferSize = 2,
                congestion = false
            };
            config.grpcItem ??= new GrpcItem
            {
                idle_timeout = 60,
                health_check_timeout = 20,
                permit_without_stream = false,
                initial_windows_size = 0,
            };
            config.tunModeItem ??= new TunModeItem
            {
                enableTun = false,
                mtu = 9000,
            };
            config.guiItem ??= new()
            {
                enableStatistics = false,
            };
            config.msgUIItem ??= new();

            config.uiItem ??= new UIItem()
            {
                enableAutoAdjustMainLvColWidth = true
            };
            if (config.uiItem.mainColumnItem == null)
            {
                config.uiItem.mainColumnItem = new();
            }
            if (Utils.IsNullOrEmpty(config.uiItem.currentLanguage))
            {
                if (Thread.CurrentThread.CurrentCulture.Name.Equals("zh-cn", StringComparison.CurrentCultureIgnoreCase))
                {
                    config.uiItem.currentLanguage = Global.Languages[0];
                }
                else
                {
                    config.uiItem.currentLanguage = Global.Languages[2];
                }
            }

            config.constItem ??= new ConstItem();
            if (Utils.IsNullOrEmpty(config.constItem.defIEProxyExceptions))
            {
                config.constItem.defIEProxyExceptions = Global.IEProxyExceptions;
            }

            config.speedTestItem ??= new();
            if (config.speedTestItem.speedTestTimeout < 10)
            {
                config.speedTestItem.speedTestTimeout = 10;
            }
            if (Utils.IsNullOrEmpty(config.speedTestItem.speedTestUrl))
            {
                config.speedTestItem.speedTestUrl = Global.SpeedTestUrls[0];
            }
            if (Utils.IsNullOrEmpty(config.speedTestItem.speedPingTestUrl))
            {
                config.speedTestItem.speedPingTestUrl = Global.SpeedPingTestUrl;
            }

            config.mux4RayItem ??= new()
            {
                concurrency = 8,
                xudpConcurrency = 16,
                xudpProxyUDP443 = "reject"
            };

            config.mux4SboxItem ??= new()
            {
                protocol = Global.SingboxMuxs[0],
                max_connections = 8
            };

            config.hysteriaItem ??= new()
            {
                up_mbps = 100,
                down_mbps = 100
            };
            config.clashUIItem ??= new();
            config.systemProxyItem ??= new();
            config.webDavItem ??= new();

            return 0;
        }

        /// <summary>
        /// 保参数
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int SaveConfig(Config config, bool reload = true)
        {
            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="config"></param>
        private static void ToJsonFile(Config config)
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

        public static int AddServer(Config config, ProfileItem profileItem)
        {
            var item = AppHandler.Instance.GetProfileItem(profileItem.indexId);
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
                EConfigType.VMess => AddVMessServer(config, item),
                EConfigType.Shadowsocks => AddShadowsocksServer(config, item),
                EConfigType.SOCKS => AddSocksServer(config, item),
                EConfigType.HTTP => AddHttpServer(config, item),
                EConfigType.Trojan => AddTrojanServer(config, item),
                EConfigType.VLESS => AddVlessServer(config, item),
                EConfigType.Hysteria2 => AddHysteria2Server(config, item),
                EConfigType.TUIC => AddTuicServer(config, item),
                EConfigType.WireGuard => AddWireguardServer(config, item),
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
        public static int AddVMessServer(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="indexes"></param>
        /// <returns></returns>
        public static int RemoveServer(Config config, List<ProfileItem> indexes)
        {
            var subid = "TempRemoveSubId";
            foreach (var item in indexes)
            {
                item.subid = subid;
            }

            SQLiteHelper.Instance.UpdateAll(indexes);
            RemoveServerViaSubid(config, subid, false);

            return 0;
        }

        /// <summary>
        /// 克隆服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int CopyServer(Config config, List<ProfileItem> indexes)
        {
            foreach (var it in indexes)
            {
                var item = AppHandler.Instance.GetProfileItem(it.indexId);
                if (item is null)
                {
                    continue;
                }

                ProfileItem profileItem = JsonUtils.DeepCopy(item);
                profileItem.indexId = string.Empty;
                profileItem.remarks = $"{item.remarks}-clone";

                if (profileItem.configType == EConfigType.Custom)
                {
                    profileItem.address = Utils.GetConfigPath(profileItem.address);
                    if (AddCustomServer(config, profileItem, false) == 0)
                    {
                    }
                }
                else
                {
                    AddServerCommon(config, profileItem, true);
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
        public static int SetDefaultServerIndex(Config config, string? indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return -1;
            }

            config.indexId = indexId;

            ToJsonFile(config);

            return 0;
        }

        public static int SetDefaultServer(Config config, List<ProfileItemModel> lstProfile)
        {
            if (lstProfile.Exists(t => t.indexId == config.indexId))
            {
                return 0;
            }
            if (SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.indexId == config.indexId).Any())
            {
                return 0;
            }
            if (lstProfile.Count > 0)
            {
                return SetDefaultServerIndex(config, lstProfile.Where(t => t.port > 0).FirstOrDefault()?.indexId);
            }
            return SetDefaultServerIndex(config, SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.port > 0).Select(t => t.indexId).FirstOrDefault());
        }

        public static ProfileItem? GetDefaultServer(Config config)
        {
            var item = AppHandler.Instance.GetProfileItem(config.indexId);
            if (item is null)
            {
                var item2 = SQLiteHelper.Instance.Table<ProfileItem>().FirstOrDefault();
                SetDefaultServerIndex(config, item2?.indexId);
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
        public static int MoveServer(Config config, ref List<ProfileItem> lstProfile, int index, EMove eMove, int pos = -1)
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
        public static int AddCustomServer(Config config, ProfileItem profileItem, bool blDelete)
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

            AddServerCommon(config, profileItem, true);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int EditCustomServer(Config config, ProfileItem profileItem)
        {
            var item = AppHandler.Instance.GetProfileItem(profileItem.indexId);
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

            if (SQLiteHelper.Instance.Update(item) > 0)
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
        public static int AddShadowsocksServer(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddSocksServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.configType = EConfigType.SOCKS;

            profileItem.address = profileItem.address.TrimEx();

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddHttpServer(Config config, ProfileItem profileItem, bool toFile = true)
        {
            profileItem.configType = EConfigType.HTTP;

            profileItem.address = profileItem.address.TrimEx();

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddTrojanServer(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddHysteria2Server(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddTuicServer(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        /// <summary>
        /// Add or edit server
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddWireguardServer(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        public static int SortServers(Config config, string subId, string colName, bool asc)
        {
            var lstModel = AppHandler.Instance.ProfileItems(subId, "");
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
        public static int AddVlessServer(Config config, ProfileItem profileItem, bool toFile = true)
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

            AddServerCommon(config, profileItem, toFile);

            return 0;
        }

        public static Tuple<int, int> DedupServerList(Config config, string subId)
        {
            var lstProfile = AppHandler.Instance.ProfileItems(subId);

            List<ProfileItem> lstKeep = new();
            List<ProfileItem> lstRemove = new();
            if (!config.guiItem.keepOlderDedupl) lstProfile.Reverse();

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
            RemoveServer(config, lstRemove);

            return new Tuple<int, int>(lstProfile.Count, lstKeep.Count);
        }

        public static int AddServerCommon(Config config, ProfileItem profileItem, bool toFile = true)
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
                        profileItem.allowInsecure = config.coreBasicItem.defAllowInsecure.ToString().ToLower();
                    }
                    if (Utils.IsNullOrEmpty(profileItem.fingerprint) && profileItem.streamSecurity == Global.StreamSecurityReality)
                    {
                        profileItem.fingerprint = config.coreBasicItem.defFingerprint;
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
                SQLiteHelper.Instance.Replace(profileItem);
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

        private static int RemoveProfileItem(Config config, string indexId)
        {
            try
            {
                var item = AppHandler.Instance.GetProfileItem(indexId);
                if (item == null)
                {
                    return 0;
                }
                if (item.configType == EConfigType.Custom)
                {
                    File.Delete(Utils.GetConfigPath(item.address));
                }

                SQLiteHelper.Instance.Delete(item);
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Remove Item", ex);
            }

            return 0;
        }

        public static int AddCustomServer4Multiple(Config config, List<ProfileItem> selecteds, ECoreType coreType, out string indexId)
        {
            indexId = Utils.GetMd5(Global.CoreMultipleLoadConfigFileName);
            string configPath = Utils.GetConfigPath(Global.CoreMultipleLoadConfigFileName);
            if (CoreConfigHandler.GenerateClientMultipleLoadConfig(config, configPath, selecteds, coreType, out string msg) != 0)
            {
                return -1;
            }

            var fileName = configPath;
            if (!File.Exists(fileName))
            {
                return -1;
            }

            var profileItem = AppHandler.Instance.GetProfileItem(indexId) ?? new();
            profileItem.indexId = indexId;
            profileItem.remarks = coreType == ECoreType.sing_box ? ResUI.menuSetDefaultMultipleServer : ResUI.menuSetDefaultLoadBalanceServer;
            profileItem.address = Global.CoreMultipleLoadConfigFileName;
            profileItem.configType = EConfigType.Custom;
            profileItem.coreType = coreType;

            AddServerCommon(config, profileItem, true);

            return 0;
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
        private static int AddBatchServers(Config config, string strData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            string subFilter = string.Empty;
            //remove sub items
            if (isSub && Utils.IsNotEmpty(subid))
            {
                RemoveServerViaSubid(config, subid, isSub);
                subFilter = AppHandler.Instance.GetSubItem(subid)?.filter ?? "";
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
                    if (AddSubItem(config, str) == 0)
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
                                                && config.uiItem.enableUpdateSubOnlyRemarksExist ? t.remarks == profileItem.remarks : CompareProfileItem(t, profileItem, true));
                    if (existItem != null)
                    {
                        //Check for duplicate indexId
                        if (lstDbIndexId is null)
                        {
                            lstDbIndexId = AppHandler.Instance.ProfileItemIndexes("");
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
                    EConfigType.VMess => AddVMessServer(config, profileItem, false),
                    EConfigType.Shadowsocks => AddShadowsocksServer(config, profileItem, false),
                    EConfigType.SOCKS => AddSocksServer(config, profileItem, false),
                    EConfigType.Trojan => AddTrojanServer(config, profileItem, false),
                    EConfigType.VLESS => AddVlessServer(config, profileItem, false),
                    EConfigType.Hysteria2 => AddHysteria2Server(config, profileItem, false),
                    EConfigType.TUIC => AddTuicServer(config, profileItem, false),
                    EConfigType.WireGuard => AddWireguardServer(config, profileItem, false),
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
                SQLiteHelper.Instance.InsertAll(lstAdd);
            }

            ToJsonFile(config);
            return countServers;
        }

        private static int AddBatchServers4Custom(Config config, string strData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            var subItem = AppHandler.Instance.GetSubItem(subid);
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
                    RemoveServerViaSubid(config, subid, isSub);
                }
                int count = 0;
                foreach (var it in lstProfiles)
                {
                    it.subid = subid;
                    it.isSub = isSub;
                    it.preSocksPort = preSocksPort;
                    if (AddCustomServer(config, it, true) == 0)
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
                RemoveServerViaSubid(config, subid, isSub);
            }
            if (isSub && lstOriSub?.Count == 1)
            {
                profileItem.indexId = lstOriSub[0].indexId;
            }
            profileItem.subid = subid;
            profileItem.isSub = isSub;
            profileItem.preSocksPort = preSocksPort;
            if (AddCustomServer(config, profileItem, true) == 0)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }

        private static int AddBatchServers4SsSIP008(Config config, string strData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }

            if (isSub && Utils.IsNotEmpty(subid))
            {
                RemoveServerViaSubid(config, subid, isSub);
            }

            var lstSsServer = ShadowsocksFmt.ResolveSip008(strData);
            if (lstSsServer?.Count > 0)
            {
                int counter = 0;
                foreach (var ssItem in lstSsServer)
                {
                    ssItem.subid = subid;
                    ssItem.isSub = isSub;
                    if (AddShadowsocksServer(config, ssItem) == 0)
                    {
                        counter++;
                    }
                }
                ToJsonFile(config);
                return counter;
            }

            return -1;
        }

        public static int AddBatchServers(Config config, string strData, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(strData))
            {
                return -1;
            }
            List<ProfileItem>? lstOriSub = null;
            if (isSub && Utils.IsNotEmpty(subid))
            {
                lstOriSub = AppHandler.Instance.ProfileItems(subid);
            }

            var counter = 0;
            if (Utils.IsBase64String(strData))
            {
                counter = AddBatchServers(config, Utils.Base64Decode(strData), subid, isSub, lstOriSub);
            }
            if (counter < 1)
            {
                counter = AddBatchServers(config, strData, subid, isSub, lstOriSub);
            }
            if (counter < 1)
            {
                counter = AddBatchServers(config, Utils.Base64Decode(strData), subid, isSub, lstOriSub);
            }

            if (counter < 1)
            {
                counter = AddBatchServers4SsSIP008(config, strData, subid, isSub, lstOriSub);
            }

            //maybe other sub
            if (counter < 1)
            {
                counter = AddBatchServers4Custom(config, strData, subid, isSub, lstOriSub);
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
        public static int AddSubItem(Config config, string url)
        {
            //already exists
            if (SQLiteHelper.Instance.Table<SubItem>().Any(e => e.url == url))
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

            return AddSubItem(config, subItem);
        }

        public static int AddSubItem(Config config, SubItem subItem)
        {
            var item = AppHandler.Instance.GetSubItem(subItem.id);
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
                    if (SQLiteHelper.Instance.Table<SubItem>().Count() > 0)
                    {
                        maxSort = SQLiteHelper.Instance.Table<SubItem>().Max(t => t == null ? 0 : t.sort);
                    }
                    item.sort = maxSort + 1;
                }
            }
            if (SQLiteHelper.Instance.Replace(item) > 0)
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
        public static int RemoveServerViaSubid(Config config, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return -1;
            }
            var customProfile = SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid && t.configType == EConfigType.Custom).ToList();
            if (isSub)
            {
                SQLiteHelper.Instance.Execute($"delete from ProfileItem where isSub = 1 and subid = '{subid}'");
            }
            else
            {
                SQLiteHelper.Instance.Execute($"delete from ProfileItem where subid = '{subid}'");
            }
            foreach (var item in customProfile)
            {
                File.Delete(Utils.GetConfigPath(item.address));
            }

            return 0;
        }

        public static int DeleteSubItem(Config config, string id)
        {
            var item = AppHandler.Instance.GetSubItem(id);
            if (item is null)
            {
                return 0;
            }
            SQLiteHelper.Instance.Delete(item);
            RemoveServerViaSubid(config, id, false);

            return 0;
        }

        public static int MoveToGroup(Config config, List<ProfileItem> lstProfile, string subid)
        {
            foreach (var item in lstProfile)
            {
                item.subid = subid;
            }
            SQLiteHelper.Instance.UpdateAll(lstProfile);

            return 0;
        }

        #endregion Sub & Group

        #region Routing

        public static int SaveRoutingItem(Config config, RoutingItem item)
        {
            if (Utils.IsNullOrEmpty(item.id))
            {
                item.id = Utils.GetGuid(false);
            }

            if (SQLiteHelper.Instance.Replace(item) > 0)
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
        public static int AddBatchRoutingRules(ref RoutingItem routingItem, string strData)
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

            if (SQLiteHelper.Instance.Replace(routingItem) > 0)
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
        public static int MoveRoutingRule(List<RulesItem> rules, int index, EMove eMove, int pos = -1)
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

        public static int SetDefaultRouting(Config config, RoutingItem routingItem)
        {
            if (SQLiteHelper.Instance.Table<RoutingItem>().Where(t => t.id == routingItem.id).Count() > 0)
            {
                config.routingBasicItem.routingIndexId = routingItem.id;
            }

            ToJsonFile(config);

            return 0;
        }

        public static RoutingItem GetDefaultRouting(Config config)
        {
            var item = AppHandler.Instance.GetRoutingItem(config.routingBasicItem.routingIndexId);
            if (item is null)
            {
                var item2 = SQLiteHelper.Instance.Table<RoutingItem>().FirstOrDefault(t => t.locked == false);
                SetDefaultRouting(config, item2);
                return item2;
            }

            return item;
        }

        public static int InitBuiltinRouting(Config config, bool blImportAdvancedRules = false)
        {
            var ver = "V3-";
            var items = AppHandler.Instance.RoutingItems();
            if (blImportAdvancedRules || items.Where(t => t.remarks.StartsWith(ver)).ToList().Count <= 0)
            {
                var maxSort = items.Count;
                //Bypass the mainland
                var item2 = new RoutingItem()
                {
                    remarks = $"{ver}绕过大陆(Whitelist)",
                    url = string.Empty,
                    sort = maxSort + 1,
                };
                AddBatchRoutingRules(ref item2, Utils.GetEmbedText(Global.CustomRoutingFileName + "white"));

                //Blacklist
                var item3 = new RoutingItem()
                {
                    remarks = $"{ver}黑名单(Blacklist)",
                    url = string.Empty,
                    sort = maxSort + 2,
                };
                AddBatchRoutingRules(ref item3, Utils.GetEmbedText(Global.CustomRoutingFileName + "black"));

                //Global
                var item1 = new RoutingItem()
                {
                    remarks = $"{ver}全局(Global)",
                    url = string.Empty,
                    sort = maxSort + 3,
                };
                AddBatchRoutingRules(ref item1, Utils.GetEmbedText(Global.CustomRoutingFileName + "global"));

                if (!blImportAdvancedRules)
                {
                    SetDefaultRouting(config, item2);
                }
            }

            if (GetLockedRoutingItem(config) == null)
            {
                var item1 = new RoutingItem()
                {
                    remarks = "locked",
                    url = string.Empty,
                    locked = true,
                };
                AddBatchRoutingRules(ref item1, Utils.GetEmbedText(Global.CustomRoutingFileName + "locked"));
            }
            return 0;
        }

        public static RoutingItem GetLockedRoutingItem(Config config)
        {
            return SQLiteHelper.Instance.Table<RoutingItem>().FirstOrDefault(it => it.locked == true);
        }

        public static void RemoveRoutingItem(RoutingItem routingItem)
        {
            SQLiteHelper.Instance.Delete(routingItem);
        }

        #endregion Routing

        #region DNS

        public static int InitBuiltinDNS(Config config)
        {
            var items = AppHandler.Instance.DNSItems();
            if (items.Count <= 0)
            {
                var item = new DNSItem()
                {
                    remarks = "V2ray",
                    coreType = ECoreType.Xray,
                };
                SaveDNSItems(config, item);

                var item2 = new DNSItem()
                {
                    remarks = "sing-box",
                    coreType = ECoreType.sing_box,
                };
                SaveDNSItems(config, item2);
            }

            return 0;
        }

        public static int SaveDNSItems(Config config, DNSItem item)
        {
            if (Utils.IsNullOrEmpty(item.id))
            {
                item.id = Utils.GetGuid(false);
            }

            if (SQLiteHelper.Instance.Replace(item) > 0)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }

        #endregion DNS
    }
}