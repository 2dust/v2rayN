using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Tool;

namespace v2rayN.Handler
{
    /// <summary>
    /// 本软件配置文件处理类
    /// </summary>
    class ConfigHandler
    {
        private static string configRes = Global.ConfigFileName;
        private static readonly object objLock = new object();

        #region ConfigHandler

        /// <summary>
        /// 载入配置文件
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int LoadConfig(ref Config config)
        {
            //载入配置文件 
            string result = Utils.LoadResource(Utils.GetConfigPath(configRes));
            if (!Utils.IsNullOrEmpty(result))
            {
                //转成Json
                config = Utils.FromJson<Config>(result);
            }
            else
            {
                if (File.Exists(Utils.GetConfigPath(configRes)))
                {
                    Utils.SaveLog("LoadConfig Exception");
                    return -1;
                }
            }

            if (config == null)
            {
                config = new Config
                {
                   
                    enableRoutingAdvanced = true
                };
            }
            if (config.coreBasicItem == null)
            {
                config.coreBasicItem = new()
                {
                    logEnabled = false,
                    loglevel = "warning",

                    muxEnabled = false,
                };
            }

            //本地监听
            if (config.inbound == null)
            {
                config.inbound = new List<InItem>();
                InItem inItem = new InItem
                {
                    protocol = Global.InboundSocks,
                    localPort = 10808,
                    udpEnabled = true,
                    sniffingEnabled = true,
                    routeOnly = false,
                };

                config.inbound.Add(inItem);

                //inItem = new InItem();
                //inItem.protocol = "http";
                //inItem.localPort = 1081;
                //inItem.udpEnabled = true;

                //config.inbound.Add(inItem);
            }
            else
            {
                if (config.inbound.Count > 0)
                {
                    config.inbound[0].protocol = Global.InboundSocks;
                }
            }
            //路由规则
            if (Utils.IsNullOrEmpty(config.domainStrategy))
            {
                config.domainStrategy = Global.domainStrategys[0];//"IPIfNonMatch";
            }
            //if (Utils.IsNullOrEmpty(config.domainMatcher))
            //{
            //    config.domainMatcher = "linear";
            //}

            //kcp
            if (config.kcpItem == null)
            {
                config.kcpItem = new KcpItem
                {
                    mtu = 1350,
                    tti = 50,
                    uplinkCapacity = 12,
                    downlinkCapacity = 100,
                    readBufferSize = 2,
                    writeBufferSize = 2,
                    congestion = false
                };
            }
            if (config.grpcItem == null)
            {
                config.grpcItem = new GrpcItem
                {
                    idle_timeout = 60,
                    health_check_timeout = 20,
                    permit_without_stream = false,
                    initial_windows_size = 0,
                };
            }
            if (config.tunModeItem == null)
            {
                config.tunModeItem = new TunModeItem
                {
                    enableTun = false,
                    showWindow = true,
                    mtu = 9000,
                };
            }
            if (config.guiItem == null)
            {
                config.guiItem = new()
                {
                    enableStatistics = false,
                    statisticsFreshRate = 1,
                };
            }
            if (config.uiItem == null)
            {
                config.uiItem = new UIItem()
                {
                    enableAutoAdjustMainLvColWidth = true
                };
            }
            if (config.uiItem.mainLvColWidth == null)
            {
                config.uiItem.mainLvColWidth = new Dictionary<string, int>();
            }
            if (Utils.IsNullOrEmpty(config.uiItem.currentLanguage))
            {
                config.uiItem.currentLanguage = Global.Languages[0];
            }

            if (config.constItem == null)
            {
                config.constItem = new ConstItem();
            }
            if (Utils.IsNullOrEmpty(config.constItem.defIEProxyExceptions))
            {
                config.constItem.defIEProxyExceptions = Global.IEProxyExceptions;
            }
            //if (Utils.IsNullOrEmpty(config.remoteDNS))
            //{
            //    config.remoteDNS = "1.1.1.1";
            //}

            if (config.speedTestItem == null)
            {
                config.speedTestItem = new();
            }
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

            if (config.guiItem.statisticsFreshRate > 100 || config.guiItem.statisticsFreshRate < 1)
            {
                config.guiItem.statisticsFreshRate = 1;
            }

            if (config == null)
            {
                Global.reloadCore = false;
            }
            else
            {
                Global.reloadCore = true;

            }

            LazyConfig.Instance.SetConfig(ref config);
            return 0;
        }
        /// <summary>
        /// 保参数
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int SaveConfig(ref Config config, bool reload = true)
        {
            Global.reloadCore = reload;

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="config"></param>
        private static void ToJsonFile(Config config)
        {
            lock (objLock)
            {
                try
                {

                    //save temp file
                    var resPath = Utils.GetConfigPath(configRes);
                    var tempPath = $"{resPath}_temp";
                    if (Utils.ToJsonFile(config, tempPath) != 0)
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
                    Utils.SaveLog("ToJsonFile", ex);
                }
            }
        }

        public static int ImportOldGuiConfig(ref Config config, string fileName)
        {
            string result = Utils.LoadResource(fileName);
            if (Utils.IsNullOrEmpty(result))
            {
                return -1;
            }

            var configOld = Utils.FromJson<ConfigOld>(result);
            if (configOld == null)
            {
                return -1;
            }

            var subItem = Utils.FromJson<List<SubItem>>(Utils.ToJson(configOld.subItem));
            foreach (var it in subItem)
            {
                if (Utils.IsNullOrEmpty(it.id))
                {
                    it.id = Utils.GetGUID(false);
                }
                SqliteHelper.Instance.Replace(it);
            }

            var profileItems = Utils.FromJson<List<ProfileItem>>(Utils.ToJson(configOld.vmess));
            foreach (var it in profileItems)
            {
                if (Utils.IsNullOrEmpty(it.indexId))
                {
                    it.indexId = Utils.GetGUID(false);
                }
                SqliteHelper.Instance.Replace(it);
            }

            foreach (var it in configOld.routings)
            {
                if (it.locked)
                {
                    continue;
                }
                var routing = Utils.FromJson<RoutingItem>(Utils.ToJson(it));
                foreach (var it2 in it.rules)
                {
                    it2.id = Utils.GetGUID(false);
                }
                routing.ruleNum = it.rules.Count;
                routing.ruleSet = Utils.ToJson(it.rules, false);

                if (Utils.IsNullOrEmpty(routing.id))
                {
                    routing.id = Utils.GetGUID(false);
                }
                SqliteHelper.Instance.Replace(routing);
            }

            config = Utils.FromJson<Config>(Utils.ToJson(configOld));
            if (config.tunModeItem == null)
            {
                config.tunModeItem = new TunModeItem
                {
                    enableTun = false,
                    showWindow = true,
                    mtu = 9000,
                };
            }

            GetDefaultServer(ref config);
            GetDefaultRouting(ref config);
            SaveConfig(ref config);
            LazyConfig.Instance.SetConfig(ref config);

            return 0;
        }
        #endregion

        #region Server

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddServer(ref Config config, ProfileItem profileItem)
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

            if (!Global.vmessSecuritys.Contains(profileItem.security))
            {
                return -1;
            }

            AddServerCommon(ref config, profileItem);

            return 0;
        }

        /// <summary>
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="indexs"></param>
        /// <returns></returns>
        public static int RemoveServer(Config config, List<ProfileItem> indexs)
        {
            foreach (var item in indexs)
            {
                RemoveProfileItem(config, item.indexId);
            }

            return 0;
        }

        /// <summary>
        /// 克隆服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int CopyServer(ref Config config, List<ProfileItem> indexs)
        {
            foreach (var item in indexs)
            {
                ProfileItem profileItem = Utils.DeepCopy(item);
                profileItem.indexId = string.Empty;
                profileItem.remarks = $"{item.remarks}-clone";

                if (profileItem.configType == EConfigType.Custom)
                {
                    profileItem.address = Utils.GetConfigPath(profileItem.address);
                    if (AddCustomServer(ref config, profileItem, false) == 0)
                    {
                    }
                }
                else
                {
                    AddServerCommon(ref config, profileItem);
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
        public static int SetDefaultServer(ref Config config, ProfileItem item)
        {
            if (item == null)
            {
                return -1;
            }

            config.indexId = item.indexId;
            Global.reloadCore = true;

            ToJsonFile(config);

            return 0;
        }

        public static int SetDefaultServer(Config config, List<ProfileItem> lstProfile)
        {
            if (lstProfile.Exists(t => t.indexId == config.indexId))
            {
                return 0;
            }

            if (SqliteHelper.Instance.Table<ProfileItem>().Where(t => t.indexId == config.indexId).Count() > 0)
            {
                return 0;
            }
            if (lstProfile.Count > 0)
            {
                return SetDefaultServer(ref config, lstProfile[0]);
            }
            if (SqliteHelper.Instance.Table<ProfileItem>().Count() > 0)
            {
                return SetDefaultServer(ref config, SqliteHelper.Instance.Table<ProfileItem>().FirstOrDefault());
            }
            return -1;
        }
        public static ProfileItem GetDefaultServer(ref Config config)
        {
            var item = LazyConfig.Instance.GetProfileItem(config.indexId);
            if (item is null)
            {
                var item2 = SqliteHelper.Instance.Table<ProfileItem>().FirstOrDefault();
                SetDefaultServer(ref config, item2);
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
        public static int MoveServer(ref Config config, ref List<ProfileItem> lstProfile, int index, EMove eMove, int pos = -1)
        {
            int count = lstProfile.Count;
            if (index < 0 || index > lstProfile.Count - 1)
            {
                return -1;
            }

            for (int i = 0; i < lstProfile.Count; i++)
            {
                lstProfile[i].sort = (i + 1) * 10;
            }

            switch (eMove)
            {
                case EMove.Top:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        lstProfile[index].sort = lstProfile[0].sort - 1;

                        break;
                    }
                case EMove.Up:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        lstProfile[index].sort = lstProfile[index - 1].sort - 1;

                        break;
                    }

                case EMove.Down:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        lstProfile[index].sort = lstProfile[index + 1].sort + 1;

                        break;
                    }
                case EMove.Bottom:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        lstProfile[index].sort = lstProfile[lstProfile.Count - 1].sort + 1;

                        break;
                    }
                case EMove.Position:
                    lstProfile[index].sort = pos * 10 + 1;
                    break;
            }

            SqliteHelper.Instance.UpdateAll(lstProfile);


            return 0;
        }

        /// <summary>
        /// 添加自定义服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddCustomServer(ref Config config, ProfileItem profileItem, bool blDelete)
        {
            var fileName = profileItem.address;
            if (!File.Exists(fileName))
            {
                return -1;
            }
            var ext = Path.GetExtension(fileName);
            string newFileName = $"{Utils.GetGUID()}{ext}";
            //newFileName = Path.Combine(Utils.GetTempPath(), newFileName);

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
                Utils.SaveLog(ex.Message, ex);
                return -1;
            }

            profileItem.address = newFileName;
            profileItem.configType = EConfigType.Custom;
            if (Utils.IsNullOrEmpty(profileItem.remarks))
            {
                profileItem.remarks = $"import custom@{DateTime.Now.ToShortDateString()}";
            }


            AddServerCommon(ref config, profileItem);


            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int EditCustomServer(ref Config config, ProfileItem profileItem)
        {
            if (SqliteHelper.Instance.Update(profileItem) > 0)
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
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddShadowsocksServer(ref Config config, ProfileItem profileItem)
        {
            profileItem.configType = EConfigType.Shadowsocks;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            profileItem.security = profileItem.security.TrimEx();

            if (!LazyConfig.Instance.GetShadowsocksSecuritys(profileItem).Contains(profileItem.security))
            {
                return -1;
            }

            AddServerCommon(ref config, profileItem);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddSocksServer(ref Config config, ProfileItem profileItem)
        {
            profileItem.configType = EConfigType.Socks;

            profileItem.address = profileItem.address.TrimEx();

            AddServerCommon(ref config, profileItem);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddTrojanServer(ref Config config, ProfileItem profileItem)
        {
            profileItem.configType = EConfigType.Trojan;

            profileItem.address = profileItem.address.TrimEx();
            profileItem.id = profileItem.id.TrimEx();
            if (Utils.IsNullOrEmpty(profileItem.streamSecurity))
            {
                profileItem.streamSecurity = Global.StreamSecurity;
            }
            if (Utils.IsNullOrEmpty(profileItem.allowInsecure))
            {
                profileItem.allowInsecure = config.coreBasicItem.defAllowInsecure.ToString().ToLower();
            }

            AddServerCommon(ref config, profileItem);

            return 0;
        }


        public static int SortServers(ref Config config, string subId, EServerColName name, bool asc)
        {
            var lstModel = LazyConfig.Instance.ProfileItems(subId, "");
            var lstProfile = Utils.FromJson<List<ProfileItem>>(Utils.ToJson(lstModel));
            if (lstProfile.Count <= 0)
            {
                return -1;
            }
            var propertyName = string.Empty;
            switch (name)
            {
                case EServerColName.configType:
                case EServerColName.remarks:
                case EServerColName.address:
                case EServerColName.port:
                case EServerColName.security:
                case EServerColName.network:
                case EServerColName.streamSecurity:
                case EServerColName.delay:
                case EServerColName.speed:
                    propertyName = name.ToString();
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
                lstProfile[i].sort = (i + 1) * 10;
            }
            if (name == EServerColName.delay)
            {
                var maxSort = lstProfile.Max(t => t.sort) + 10;
                foreach (var item in lstProfile)
                {
                    if (item.delay <= 0)
                    {
                        item.sort = maxSort;
                    }
                }
            }
            if (name == EServerColName.speed)
            {
                var maxSort = lstProfile.Max(t => t.sort) + 10;
                foreach (var item in lstProfile)
                {
                    if (item.speed <= 0)
                    {
                        item.sort = maxSort;
                    }
                }
            }

            SqliteHelper.Instance.UpdateAll(lstProfile);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="profileItem"></param>
        /// <returns></returns>
        public static int AddVlessServer(ref Config config, ProfileItem profileItem)
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

            AddServerCommon(ref config, profileItem);

            return 0;
        }

        public static int DedupServerList(ref Config config, ref List<ProfileItem> lstProfile)
        {
            List<ProfileItem> source = lstProfile;
            bool keepOlder = config.guiItem.keepOlderDedupl;

            List<ProfileItem> list = new List<ProfileItem>();
            if (!keepOlder) source.Reverse(); // Remove the early items first

            foreach (ProfileItem item in source)
            {
                if (!list.Exists(i => CompareProfileItem(i, item, false)))
                {
                    list.Add(item);
                }
                else
                {
                    RemoveProfileItem(config, item.indexId);
                }
            }
            //if (!keepOlder) list.Reverse();
            //config.vmess = list;

            return list.Count;
        }

        public static int AddServerCommon(ref Config config, ProfileItem profileItem)
        {
            profileItem.configVersion = 2;
            if (Utils.IsNullOrEmpty(profileItem.allowInsecure))
            {
                profileItem.allowInsecure = config.coreBasicItem.defAllowInsecure.ToString().ToLower();
            }
            if (!Utils.IsNullOrEmpty(profileItem.network) && !Global.networks.Contains(profileItem.network))
            {
                profileItem.network = Global.DefaultNetwork;
            }

            if (Utils.IsNullOrEmpty(profileItem.indexId))
            {
                profileItem.indexId = Utils.GetGUID(false);
                if (profileItem.sort <= 0)
                {
                    var maxSort = 0;
                    if (SqliteHelper.Instance.Table<ProfileItem>().Count() > 0)
                    {
                        maxSort = SqliteHelper.Instance.Table<ProfileItem>().Max(t => t == null ? 0 : t.sort);
                    }
                    profileItem.sort = maxSort + 1;
                }
            }
            else if (profileItem.indexId == config.indexId)
            {
                Global.reloadCore = true;
            }

            if (SqliteHelper.Instance.Replace(profileItem) > 0)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }

        private static bool CompareProfileItem(ProfileItem o, ProfileItem n, bool remarks)
        {
            if (o == null || n == null)
            {
                return false;
            }

            return o.configVersion == n.configVersion
                && o.configType == n.configType
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
                var item = LazyConfig.Instance.GetProfileItem(indexId);
                if (item == null)
                {
                    return 0;
                }
                if (item.configType == EConfigType.Custom)
                {
                    File.Delete(Utils.GetConfigPath(item.address));
                }

                SqliteHelper.Instance.Delete(item);
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Remove Item", ex);
            }

            return 0;
        }
        #endregion

        #region Batch add servers

        /// <summary>
        /// 批量添加服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="clipboardData"></param>
        /// <param name="subid"></param>
        /// <returns>成功导入的数量</returns>
        private static int AddBatchServers(ref Config config, string clipboardData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            string subFilter = string.Empty;
            //remove sub items
            if (isSub && !Utils.IsNullOrEmpty(subid))
            {
                RemoveServerViaSubid(ref config, subid, isSub);
                subFilter = LazyConfig.Instance.GetSubItem(subid)?.filter ?? "";
            }

            int countServers = 0;
            var maxSort = 0;
            if (SqliteHelper.Instance.Table<ProfileItem>().Count() > 0)
            {
                maxSort = SqliteHelper.Instance.Table<ProfileItem>().Max(t => t.sort);
            }
            string[] arrData = clipboardData.Split(Environment.NewLine.ToCharArray());
            foreach (string str in arrData)
            {
                //maybe sub
                if (Utils.IsNullOrEmpty(subid) && (str.StartsWith(Global.httpsProtocol) || str.StartsWith(Global.httpProtocol)))
                {
                    if (AddSubItem(ref config, str) == 0)
                    {
                        countServers++;
                    }
                    continue;
                }
                ProfileItem profileItem = ShareHandler.ImportFromClipboardConfig(str, out string msg);
                if (profileItem == null)
                {
                    continue;
                }

                //exist sub items
                if (isSub && !Utils.IsNullOrEmpty(subid))
                {
                    var existItem = lstOriSub?.FirstOrDefault(t => CompareProfileItem(t, profileItem, true));
                    if (existItem != null)
                    {
                        profileItem = existItem;
                    }
                    //filter
                    if (!Utils.IsNullOrEmpty(subFilter))
                    {
                        if (!Regex.IsMatch(profileItem.remarks, subFilter))
                        {
                            continue;
                        }
                    }
                }
                profileItem.subid = subid;
                profileItem.isSub = isSub;
                profileItem.sort = maxSort + countServers + 1;

                if (profileItem.configType == EConfigType.VMess)
                {
                    if (AddServer(ref config, profileItem) == 0)
                    {
                        countServers++;
                    }
                }
                else if (profileItem.configType == EConfigType.Shadowsocks)
                {
                    if (AddShadowsocksServer(ref config, profileItem) == 0)
                    {
                        countServers++;
                    }
                }
                else if (profileItem.configType == EConfigType.Socks)
                {
                    if (AddSocksServer(ref config, profileItem) == 0)
                    {
                        countServers++;
                    }
                }
                else if (profileItem.configType == EConfigType.Trojan)
                {
                    if (AddTrojanServer(ref config, profileItem) == 0)
                    {
                        countServers++;
                    }
                }
                else if (profileItem.configType == EConfigType.VLESS)
                {
                    if (AddVlessServer(ref config, profileItem) == 0)
                    {
                        countServers++;
                    }
                }
            }

            ToJsonFile(config);
            return countServers;
        }

        private static int AddBatchServers4Custom(ref Config config, string clipboardData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            ProfileItem profileItem = new ProfileItem();
            //Is v2ray configuration
            V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(clipboardData);
            if (v2rayConfig != null
                && v2rayConfig.inbounds != null
                && v2rayConfig.inbounds.Count > 0
                && v2rayConfig.outbounds != null
                && v2rayConfig.outbounds.Count > 0)
            {
                var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.json");
                File.WriteAllText(fileName, clipboardData);

                profileItem.coreType = ECoreType.Xray;
                profileItem.address = fileName;
                profileItem.remarks = "v2ray_custom";
            }
            //Is Clash configuration
            else if (clipboardData.IndexOf("port") >= 0
                && clipboardData.IndexOf("socks-port") >= 0
                && clipboardData.IndexOf("proxies") >= 0)
            {
                var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.yaml");
                File.WriteAllText(fileName, clipboardData);

                profileItem.coreType = ECoreType.clash;
                profileItem.address = fileName;
                profileItem.remarks = "clash_custom";
            }
            //Is hysteria configuration
            else if (clipboardData.IndexOf("server") >= 0
                && clipboardData.IndexOf("up") >= 0
                && clipboardData.IndexOf("down") >= 0
                && clipboardData.IndexOf("listen") >= 0
                && clipboardData.IndexOf("<html>") < 0
                && clipboardData.IndexOf("<body>") < 0)
            {
                var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.json");
                File.WriteAllText(fileName, clipboardData);

                profileItem.coreType = ECoreType.hysteria;
                profileItem.address = fileName;
                profileItem.remarks = "hysteria_custom";
            }
            //Is naiveproxy configuration
            else if (clipboardData.IndexOf("listen") >= 0
                && clipboardData.IndexOf("proxy") >= 0
                && clipboardData.IndexOf("<html>") < 0
                && clipboardData.IndexOf("<body>") < 0)
            {
                var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.json");
                File.WriteAllText(fileName, clipboardData);

                profileItem.coreType = ECoreType.naiveproxy;
                profileItem.address = fileName;
                profileItem.remarks = "naiveproxy_custom";
            }
            //Is Other configuration
            else
            {
                return -1;
                //var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.txt");
                //File.WriteAllText(fileName, clipboardData);

                //profileItem.address = fileName;
                //profileItem.remarks = "other_custom";
            }

            if (isSub && !Utils.IsNullOrEmpty(subid))
            {
                RemoveServerViaSubid(ref config, subid, isSub);
            }
            if (isSub && lstOriSub != null && lstOriSub.Count == 1)
            {
                profileItem.indexId = lstOriSub[0].indexId;
            }
            profileItem.subid = subid;
            profileItem.isSub = isSub;

            if (Utils.IsNullOrEmpty(profileItem.address))
            {
                return -1;
            }

            if (AddCustomServer(ref config, profileItem, true) == 0)
            {
                return 1;

            }
            else
            {
                return -1;
            }
        }

        private static int AddBatchServers4SsSIP008(ref Config config, string clipboardData, string subid, bool isSub, List<ProfileItem> lstOriSub)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            if (isSub && !Utils.IsNullOrEmpty(subid))
            {
                RemoveServerViaSubid(ref config, subid, isSub);
            }

            //SsSIP008
            var lstSsServer = Utils.FromJson<List<SsServer>>(clipboardData);
            if (lstSsServer == null || lstSsServer.Count <= 0)
            {
                var ssSIP008 = Utils.FromJson<SsSIP008>(clipboardData);
                if (ssSIP008?.servers != null && ssSIP008.servers.Count > 0)
                {
                    lstSsServer = ssSIP008.servers;
                }
            }

            if (lstSsServer != null && lstSsServer.Count > 0)
            {
                int counter = 0;
                foreach (var it in lstSsServer)
                {
                    var ssItem = new ProfileItem()
                    {
                        subid = subid,
                        remarks = it.remarks,
                        security = it.method,
                        id = it.password,
                        address = it.server,
                        port = Utils.ToInt(it.server_port)
                    };
                    ssItem.subid = subid;
                    ssItem.isSub = isSub;
                    if (AddShadowsocksServer(ref config, ssItem) == 0)
                    {
                        counter++;
                    }
                }
                ToJsonFile(config);
                return counter;
            }

            return -1;
        }

        public static int AddBatchServers(ref Config config, string clipboardData, string subid, bool isSub)
        {
            List<ProfileItem> lstOriSub = null;
            if (isSub && !Utils.IsNullOrEmpty(subid))
            {
                lstOriSub = LazyConfig.Instance.ProfileItems(subid);
            }

            int counter = AddBatchServers(ref config, clipboardData, subid, isSub, lstOriSub);
            if (counter < 1)
            {
                counter = AddBatchServers(ref config, Utils.Base64Decode(clipboardData), subid, isSub, lstOriSub);
            }

            if (counter < 1)
            {
                counter = AddBatchServers4SsSIP008(ref config, clipboardData, subid, isSub, lstOriSub);
            }

            //maybe other sub 
            if (counter < 1)
            {
                counter = AddBatchServers4Custom(ref config, clipboardData, subid, isSub, lstOriSub);
            }

            return counter;
        }


        #endregion

        #region Sub & Group

        /// <summary>
        /// add sub
        /// </summary>
        /// <param name="config"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public static int AddSubItem(ref Config config, string url)
        {
            //already exists
            if (SqliteHelper.Instance.Table<SubItem>().Where(e => e.url == url).Count() > 0)
            {
                return 0;
            }

            SubItem subItem = new SubItem
            {
                id = string.Empty,
                remarks = "import_sub",
                url = url
            };

            return AddSubItem(ref config, subItem);
        }

        public static int AddSubItem(ref Config config, SubItem subItem)
        {
            if (Utils.IsNullOrEmpty(subItem.id))
            {
                subItem.id = Utils.GetGUID(false);

                if (subItem.sort <= 0)
                {
                    var maxSort = 0;
                    if (SqliteHelper.Instance.Table<SubItem>().Count() > 0)
                    {
                        maxSort = SqliteHelper.Instance.Table<SubItem>().Max(t => t == null ? 0 : t.sort);
                    }
                    subItem.sort = maxSort + 1;
                }
            }
            if (SqliteHelper.Instance.Replace(subItem) > 0)
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
        public static int RemoveServerViaSubid(ref Config config, string subid, bool isSub)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return -1;
            }
            if (isSub)
            {
                SqliteHelper.Instance.Execute($"delete from ProfileItem where isSub = 1 and subid = '{subid}'");
            }
            else
            {
                SqliteHelper.Instance.Execute($"delete from ProfileItem where subid = '{subid}'");
            }

            return 0;
        }

        public static int DeleteSubItem(ref Config config, string id)
        {
            var item = LazyConfig.Instance.GetSubItem(id);
            if (item is null)
            {
                return 0;
            }
            SqliteHelper.Instance.Delete(item);
            RemoveServerViaSubid(ref config, id, false);

            return 0;
        }

        public static int MoveToGroup(Config config, List<ProfileItem> lstProfile, string subid)
        {
            foreach (var it in lstProfile)
            {
                var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                if (item is null)
                {
                    continue;
                }

                item.subid = subid;
                SqliteHelper.Instance.Update(item);
            }

            return 0;
        }
        #endregion

        #region UI

        public static int AddformMainLvColWidth(ref Config config, string name, int width)
        {
            if (config.uiItem.mainLvColWidth == null)
            {
                config.uiItem.mainLvColWidth = new Dictionary<string, int>();
            }
            if (config.uiItem.mainLvColWidth.ContainsKey(name))
            {
                config.uiItem.mainLvColWidth[name] = width;
            }
            else
            {
                config.uiItem.mainLvColWidth.Add(name, width);
            }

            ToJsonFile(config);
            return 0;
        }
        public static int GetformMainLvColWidth(ref Config config, string name, int width)
        {
            if (config.uiItem.mainLvColWidth == null)
            {
                config.uiItem.mainLvColWidth = new Dictionary<string, int>();
            }
            if (config.uiItem.mainLvColWidth.ContainsKey(name))
            {
                return config.uiItem.mainLvColWidth[name];
            }
            else
            {
                return width;
            }
        }

        #endregion

        #region Routing        

        public static int SaveRoutingItem(ref Config config, RoutingItem item)
        {
            if (Utils.IsNullOrEmpty(item.id))
            {
                item.id = Utils.GetGUID(false);
            }

            if (SqliteHelper.Instance.Replace(item) > 0)
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
        /// <param name="clipboardData"></param>
        /// <returns></returns>
        public static int AddBatchRoutingRules(ref RoutingItem routingItem, string clipboardData)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            var lstRules = Utils.FromJson<List<RulesItem>>(clipboardData);
            if (lstRules == null)
            {
                return -1;
            }

            foreach (var item in lstRules)
            {
                item.id = Utils.GetGUID(false);
            }
            routingItem.ruleNum = lstRules.Count;
            routingItem.ruleSet = Utils.ToJson(lstRules, false);

            if (Utils.IsNullOrEmpty(routingItem.id))
            {
                routingItem.id = Utils.GetGUID(false);
            }

            if (SqliteHelper.Instance.Replace(routingItem) > 0)
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
                        var item = Utils.DeepCopy(rules[index]);
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
                        var item = Utils.DeepCopy(rules[index]);
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
                        var item = Utils.DeepCopy(rules[index]);
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
                        var item = Utils.DeepCopy(rules[index]);
                        rules.RemoveAt(index);
                        rules.Add(item);

                        break;
                    }
                case EMove.Position:
                    {
                        var removeItem = rules[index];
                        var item = Utils.DeepCopy(rules[index]);
                        rules.Insert(pos, item);
                        rules.Remove(removeItem);
                        break;
                    }

            }
            return 0;
        }

        public static int SetDefaultRouting(ref Config config, RoutingItem routingItem)
        {
            if (SqliteHelper.Instance.Table<RoutingItem>().Where(t => t.id == routingItem.id).Count() > 0)
            {
                config.routingIndexId = routingItem.id;
            }

            Global.reloadCore = true;

            ToJsonFile(config);

            return 0;
        }
        public static RoutingItem GetDefaultRouting(ref Config config)
        {
            var item = LazyConfig.Instance.GetRoutingItem(config.routingIndexId);
            if (item is null)
            {
                var item2 = SqliteHelper.Instance.Table<RoutingItem>().FirstOrDefault(t => t.locked == false);
                SetDefaultRouting(ref config, item2);
                return item2;
            }

            return item;
        }

        public static int InitBuiltinRouting(ref Config config, bool blImportAdvancedRules = false)
        {
            if (blImportAdvancedRules || LazyConfig.Instance.RoutingItems().Count <= 0)
            {
                //Bypass the mainland
                var item2 = new RoutingItem()
                {
                    remarks = "绕过大陆(Whitelist)",
                    url = string.Empty,
                };
                AddBatchRoutingRules(ref item2, Utils.GetEmbedText(Global.CustomRoutingFileName + "white"));

                //Blacklist
                var item3 = new RoutingItem()
                {
                    remarks = "黑名单(Blacklist)",
                    url = string.Empty,
                };
                AddBatchRoutingRules(ref item3, Utils.GetEmbedText(Global.CustomRoutingFileName + "black"));

                //Global
                var item1 = new RoutingItem()
                {
                    remarks = "全局(Global)",
                    url = string.Empty,
                };
                AddBatchRoutingRules(ref item1, Utils.GetEmbedText(Global.CustomRoutingFileName + "global"));

                if (!blImportAdvancedRules)
                {
                    SetDefaultRouting(ref config, item2);
                }
            }

            if (GetLockedRoutingItem(ref config) == null)
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

        public static RoutingItem GetLockedRoutingItem(ref Config config)
        {
            return SqliteHelper.Instance.Table<RoutingItem>().FirstOrDefault(it => it.locked == true);
        }

        public static void RemoveRoutingItem(RoutingItem routingItem)
        {
            SqliteHelper.Instance.Delete(routingItem);
        }
        #endregion
    }
}
