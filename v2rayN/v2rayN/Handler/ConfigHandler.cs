using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using v2rayN.Mode;
using v2rayN.Base;
using System.Linq;
using v2rayN.Tool;

namespace v2rayN.Handler
{
    /// <summary>
    /// 本软件配置文件处理类
    /// </summary>
    class ConfigHandler
    {
        private static string configRes = Global.ConfigFileName;

        #region ConfigHandler

        /// <summary>
        /// 载入配置文件
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int LoadConfig(ref Config config)
        {
            //载入配置文件 
            string result = Utils.LoadResource(Utils.GetPath(configRes));
            if (!Utils.IsNullOrEmpty(result))
            {
                //转成Json
                config = Utils.FromJson<Config>(result);
            }
            else
            {
                if (File.Exists(Utils.GetPath(configRes)))
                {
                    Utils.SaveLog("LoadConfig Exception");
                    return -1;
                }
            }

            if (config == null)
            {
                config = new Config
                {
                    logEnabled = false,
                    loglevel = "warning",
                    vmess = new List<VmessItem>(),

                    //Mux
                    muxEnabled = false,

                    // 默认不开启统计
                    enableStatistics = false,

                    // 默认中等刷新率
                    statisticsFreshRate = (int)Global.StatisticsFreshRate.medium,

                    enableRoutingAdvanced = true
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
                    sniffingEnabled = true
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
                config.domainStrategy = "IPIfNonMatch";
            }
            if (Utils.IsNullOrEmpty(config.domainMatcher))
            {
                config.domainMatcher = "linear";
            }

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


            if (config.constItem == null)
            {
                config.constItem = new ConstItem();
            }
            if (Utils.IsNullOrEmpty(config.constItem.speedTestUrl))
            {
                config.constItem.speedTestUrl = Global.SpeedTestUrl;
            }
            if (Utils.IsNullOrEmpty(config.constItem.speedPingTestUrl))
            {
                config.constItem.speedPingTestUrl = Global.SpeedPingTestUrl;
            }
            if (Utils.IsNullOrEmpty(config.constItem.defIEProxyExceptions))
            {
                config.constItem.defIEProxyExceptions = Global.IEProxyExceptions;
            }
            //if (Utils.IsNullOrEmpty(config.remoteDNS))
            //{
            //    config.remoteDNS = "1.1.1.1";
            //}

            if (config.subItem == null)
            {
                config.subItem = new List<SubItem>();
            }
            if (config.groupItem == null)
            {
                config.groupItem = new List<GroupItem>();
            }


            if (config == null
                || config.vmess.Count <= 0
                )
            {
                Global.reloadV2ray = false;
            }
            else
            {
                Global.reloadV2ray = true;

                //版本升级
                for (int i = 0; i < config.vmess.Count; i++)
                {
                    VmessItem vmessItem = config.vmess[i];
                    UpgradeServerVersion(ref vmessItem);

                    if (Utils.IsNullOrEmpty(vmessItem.indexId))
                    {
                        vmessItem.indexId = Utils.GetGUID(false);
                    }
                }
            }

            LazyConfig.Instance.SetConfig(ref config);
            return 0;
        }

        #endregion

        #region Server

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int AddServer(ref Config config, VmessItem vmessItem, bool toFile = true)
        {
            vmessItem.configType = (int)EConfigType.Vmess;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();
            vmessItem.network = vmessItem.network.TrimEx();
            vmessItem.headerType = vmessItem.headerType.TrimEx();
            vmessItem.requestHost = vmessItem.requestHost.TrimEx();
            vmessItem.path = vmessItem.path.TrimEx();
            vmessItem.streamSecurity = vmessItem.streamSecurity.TrimEx();

            if (!Global.vmessSecuritys.Contains(vmessItem.security))
            {
                return -1;
            }

            AddServerCommon(ref config, vmessItem);

            if (toFile)
            {
                ToJsonFile(config);
            }
            return 0;
        }

        /// <summary>
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="indexs"></param>
        /// <returns></returns>
        public static int RemoveServer(Config config, List<VmessItem> indexs)
        {
            foreach (var item in indexs)
            {
                var index = config.FindIndexId(item.indexId);
                if (index >= 0)
                {
                    config.vmess.RemoveAt(index);
                }
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 克隆服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int CopyServer(ref Config config, VmessItem item)
        {
            if (item == null)
            {
                return -1;
            }

            VmessItem vmessItem = new VmessItem
            {
                configVersion = item.configVersion,
                address = item.address,
                port = item.port,
                id = item.id,
                alterId = item.alterId,
                security = item.security,
                network = item.network,
                remarks = string.Format("{0}-clone", item.remarks),
                headerType = item.headerType,
                requestHost = item.requestHost,
                path = item.path,
                streamSecurity = item.streamSecurity,
                allowInsecure = item.allowInsecure,
                configType = item.configType,
                flow = item.flow,
                sni = item.sni
            };

            AddServerCommon(ref config, vmessItem);

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 设置活动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int SetDefaultServer(ref Config config, VmessItem item)
        {
            if (item == null)
            {
                return -1;
            }

            config.indexId = item.indexId;
            Global.reloadV2ray = true;

            ToJsonFile(config);

            return 0;
        }

        public static int SetDefaultServer(Config config, List<VmessItem> lstVmess)
        {
            if (lstVmess.Exists(t => t.indexId == config.indexId))
            {
                return 0;
            }
            if (config.vmess.Exists(t => t.indexId == config.indexId))
            {
                return 0;
            }
            if (lstVmess.Count > 0)
            {
                return SetDefaultServer(ref config, lstVmess[0]);
            }
            if (config.vmess.Count > 0)
            {
                return SetDefaultServer(ref config, config.vmess[0]);
            }
            return -1;
        }
        public static VmessItem GetDefaultServer(ref Config config)
        {
            if (config.vmess.Count <= 0)
            {
                return null;
            }
            var index = config.FindIndexId(config.indexId);
            if (index < 0)
            {
                SetDefaultServer(ref config, config.vmess[0]);
                return config.vmess[0];
            }

            return config.vmess[index];
        }

        /// <summary>
        /// 保参数
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int SaveConfig(ref Config config, bool reload = true)
        {
            Global.reloadV2ray = reload;

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="config"></param>
        private static void ToJsonFile(Config config)
        {
            Utils.ToJsonFile(config, Utils.GetPath(configRes));
        }

        /// <summary>
        /// 移动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="lstVmess"></param>
        /// <param name="index"></param>
        /// <param name="eMove"></param>
        /// <returns></returns>
        public static int MoveServer(ref Config config, ref List<VmessItem> lstVmess, int index, EMove eMove)
        {
            int count = lstVmess.Count;
            if (index < 0 || index > lstVmess.Count - 1)
            {
                return -1;
            }

            for (int i = 0; i < lstVmess.Count; i++)
            {
                lstVmess[i].sort = (i + 1) * 10;
            }

            switch (eMove)
            {
                case EMove.Top:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        lstVmess[index].sort = lstVmess[0].sort - 1;

                        break;
                    }
                case EMove.Up:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        lstVmess[index].sort = lstVmess[index - 1].sort - 1;

                        break;
                    }

                case EMove.Down:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        lstVmess[index].sort = lstVmess[index + 1].sort + 1;

                        break;
                    }
                case EMove.Bottom:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        lstVmess[index].sort = lstVmess[lstVmess.Count - 1].sort + 1;

                        break;
                    }
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加自定义服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static int AddCustomServer(ref Config config, string fileName, string groupId)
        {
            string newFileName = string.Format("{0}.json", Utils.GetGUID());
            //newFileName = Path.Combine(Utils.GetTempPath(), newFileName);

            try
            {
                File.Copy(fileName, Path.Combine(Utils.GetTempPath(), newFileName));
            }
            catch
            {
                return -1;
            }

            VmessItem vmessItem = new VmessItem
            {
                groupId = groupId,
                address = newFileName,
                configType = (int)EConfigType.Custom,
                remarks = string.Format("import custom@{0}", DateTime.Now.ToShortDateString())
            };

            AddServerCommon(ref config, vmessItem);

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int EditCustomServer(ref Config config, VmessItem vmessItem)
        {
            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int AddShadowsocksServer(ref Config config, VmessItem vmessItem, bool toFile = true)
        {
            vmessItem.configType = (int)EConfigType.Shadowsocks;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();

            if (!config.GetShadowsocksSecuritys().Contains(vmessItem.security))
            {
                return -1;
            }

            AddServerCommon(ref config, vmessItem);

            if (toFile)
            {
                ToJsonFile(config);
            }

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int AddSocksServer(ref Config config, VmessItem vmessItem, bool toFile = true)
        {
            vmessItem.configType = (int)EConfigType.Socks;

            vmessItem.address = vmessItem.address.TrimEx();

            AddServerCommon(ref config, vmessItem);

            if (toFile)
            {
                ToJsonFile(config);
            }

            return 0;
        }


        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int AddTrojanServer(ref Config config, VmessItem vmessItem, bool toFile = true)
        {
            vmessItem.configType = (int)EConfigType.Trojan;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            if (Utils.IsNullOrEmpty(vmessItem.streamSecurity))
            {
                vmessItem.streamSecurity = Global.StreamSecurity;
            }
            if (Utils.IsNullOrEmpty(vmessItem.allowInsecure))
            {
                vmessItem.allowInsecure = config.defAllowInsecure.ToString();
            }

            AddServerCommon(ref config, vmessItem);

            if (toFile)
            {
                ToJsonFile(config);
            }

            return 0;
        }

        /// <summary>
        /// 配置文件版本升级
        /// </summary>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int UpgradeServerVersion(ref VmessItem vmessItem)
        {
            try
            {
                if (vmessItem == null
                    || vmessItem.configVersion == 2)
                {
                    return 0;
                }
                if (vmessItem.configType == (int)EConfigType.Vmess)
                {
                    string path = "";
                    string host = "";
                    string[] arrParameter;
                    switch (vmessItem.network)
                    {
                        case "kcp":
                            break;
                        case "ws":
                            //*ws(path+host),它们中间分号(;)隔开
                            arrParameter = vmessItem.requestHost.Replace(" ", "").Split(';');
                            if (arrParameter.Length > 0)
                            {
                                path = arrParameter[0];
                            }
                            if (arrParameter.Length > 1)
                            {
                                path = arrParameter[0];
                                host = arrParameter[1];
                            }
                            vmessItem.path = path;
                            vmessItem.requestHost = host;
                            break;
                        case "h2":
                            //*h2 path
                            arrParameter = vmessItem.requestHost.Replace(" ", "").Split(';');
                            if (arrParameter.Length > 0)
                            {
                                path = arrParameter[0];
                            }
                            if (arrParameter.Length > 1)
                            {
                                path = arrParameter[0];
                                host = arrParameter[1];
                            }
                            vmessItem.path = path;
                            vmessItem.requestHost = host;
                            break;
                        default:
                            break;
                    }
                }
                vmessItem.configVersion = 2;
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// 批量添加服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="clipboardData"></param>
        /// <param name="subid"></param>
        /// <returns>成功导入的数量</returns>
        public static int AddBatchServers(ref Config config, string clipboardData, string subid, string groupId)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            //copy sub items
            List<VmessItem> lstOriSub = null;
            if (!Utils.IsNullOrEmpty(subid))
            {
                lstOriSub = config.vmess.Where(it => it.subid == subid).ToList();
                RemoveServerViaSubid(ref config, subid);
            }
            //if (clipboardData.IndexOf("vmess") >= 0 && clipboardData.IndexOf("vmess") == clipboardData.LastIndexOf("vmess"))
            //{
            //    clipboardData = clipboardData.Replace("\r\n", "").Replace("\n", "");
            //}
            int countServers = 0;

            //string[] arrData = clipboardData.Split(new string[] { "\r\n" }, StringSplitOptions.None);
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
                VmessItem vmessItem = ShareHandler.ImportFromClipboardConfig(str, out string msg);
                if (vmessItem == null)
                {
                    continue;
                }

                //exist sub items
                if (!Utils.IsNullOrEmpty(subid))
                {
                    var existItem = lstOriSub?.FirstOrDefault(t => CompareVmessItem(t, vmessItem, true));
                    if (existItem != null)
                    {
                        vmessItem = existItem;
                    }
                    vmessItem.subid = subid;
                }

                //groupId
                vmessItem.groupId = groupId;

                if (vmessItem.configType == (int)EConfigType.Vmess)
                {
                    if (AddServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.Shadowsocks)
                {
                    if (AddShadowsocksServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.Socks)
                {
                    if (AddSocksServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.Trojan)
                {
                    if (AddTrojanServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.VLESS)
                {
                    if (AddVlessServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
            }

            ToJsonFile(config);
            return countServers;
        }

        /// <summary>
        /// add sub
        /// </summary>
        /// <param name="config"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public static int AddSubItem(ref Config config, string url)
        {
            //already exists
            if (config.subItem.FindIndex(e => e.url == url) >= 0)
            {
                return 0;
            }

            SubItem subItem = new SubItem
            {
                id = string.Empty,
                remarks = "import sub",
                url = url
            };
            config.subItem.Add(subItem);

            return SaveSubItem(ref config);
        }

        /// <summary>
        /// save sub
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int SaveSubItem(ref Config config)
        {
            if (config.subItem == null)
            {
                return -1;
            }

            foreach (SubItem item in config.subItem)
            {
                if (Utils.IsNullOrEmpty(item.id))
                {
                    item.id = Utils.GetGUID(false);
                }
            }

            ToJsonFile(config);
            return 0;
        }

        /// <summary>
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="subid"></param>
        /// <returns></returns>
        public static int RemoveServerViaSubid(ref Config config, string subid)
        {
            if (Utils.IsNullOrEmpty(subid) || config.vmess.Count <= 0)
            {
                return -1;
            }
            for (int k = config.vmess.Count - 1; k >= 0; k--)
            {
                if (config.vmess[k].subid.Equals(subid))
                {
                    config.vmess.RemoveAt(k);
                }
            }

            ToJsonFile(config);
            return 0;
        }

        public static int SortServers(ref Config config, ref List<VmessItem> lstVmess, EServerColName name, bool asc)
        {
            if (lstVmess.Count <= 0)
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
                case EServerColName.testResult:
                    propertyName = name.ToString();
                    break;
                case EServerColName.subRemarks:
                    propertyName = "subid";
                    break;
                default:
                    return -1;
            }

            var items = lstVmess.AsQueryable();

            if (asc)
            {
                lstVmess = items.OrderBy(propertyName).ToList();
            }
            else
            {
                lstVmess = items.OrderByDescending(propertyName).ToList();
            }
            for (int i = 0; i < lstVmess.Count; i++)
            {
                lstVmess[i].sort = (i + 1) * 10;
            }

            ToJsonFile(config);
            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int AddVlessServer(ref Config config, VmessItem vmessItem, bool toFile = true)
        {
            vmessItem.configType = (int)EConfigType.VLESS;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();
            vmessItem.network = vmessItem.network.TrimEx();
            vmessItem.headerType = vmessItem.headerType.TrimEx();
            vmessItem.requestHost = vmessItem.requestHost.TrimEx();
            vmessItem.path = vmessItem.path.TrimEx();
            vmessItem.streamSecurity = vmessItem.streamSecurity.TrimEx();

            AddServerCommon(ref config, vmessItem);

            if (toFile)
            {
                ToJsonFile(config);
            }

            return 0;
        }

        public static int DedupServerList(ref Config config, ref List<VmessItem> lstVmess)
        {
            List<VmessItem> source = lstVmess;
            bool keepOlder = config.keepOlderDedupl;

            List<VmessItem> list = new List<VmessItem>();
            if (!keepOlder) source.Reverse(); // Remove the early items first

            foreach (VmessItem item in source)
            {
                if (!list.Exists(i => CompareVmessItem(i, item, false)))
                {
                    list.Add(item);
                }
                else
                {
                    var index = config.FindIndexId(item.indexId);
                    if (index >= 0)
                    {
                        config.vmess.RemoveAt(index);
                    }
                }
            }
            //if (!keepOlder) list.Reverse();
            //config.vmess = list;

            return 0;
        }

        public static int AddServerCommon(ref Config config, VmessItem vmessItem)
        {
            vmessItem.configVersion = 2;
            if (Utils.IsNullOrEmpty(vmessItem.allowInsecure))
            {
                vmessItem.allowInsecure = config.defAllowInsecure.ToString();
            }
            if (!Utils.IsNullOrEmpty(vmessItem.network) && !Global.networks.Contains(vmessItem.network))
            {
                vmessItem.network = Global.DefaultNetwork;
            }

            if (Utils.IsNullOrEmpty(vmessItem.indexId))
            {
                vmessItem.indexId = Utils.GetGUID(false);
            }
            if (!config.vmess.Exists(it => it.indexId == vmessItem.indexId))
            {
                var maxSort = config.vmess.Any() ? config.vmess.Max(t => t.sort) : 0;
                vmessItem.sort = maxSort++;

                config.vmess.Add(vmessItem);
            }

            //if (config.vmess.Count == 1)
            //{
            //    config.indexId = config.vmess[0].indexId;
            //    Global.reloadV2ray = true;
            //}
            return 0;
        }

        private static bool CompareVmessItem(VmessItem o, VmessItem n, bool remarks)
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
                && o.streamSecurity == n.streamSecurity
                && o.flow == n.flow
                && (remarks ? o.remarks == n.remarks : true);
        }

        /// <summary>
        /// save Group
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static int SaveGroupItem(ref Config config)
        {
            if (config.groupItem == null)
            {
                return -1;
            }

            foreach (GroupItem item in config.groupItem)
            {
                if (Utils.IsNullOrEmpty(item.id))
                {
                    item.id = Utils.GetGUID(false);
                }
            }

            ToJsonFile(config);
            return 0;
        }

        public static int RemoveGroupItem(ref Config config, string groupId)
        {
            if (Utils.IsNullOrEmpty(groupId))
            {
                return -1;
            }

            var items = config.vmess.Where(t => t.groupId == groupId).ToList();
            foreach (var item in items)
            {
                if (item.groupId.Equals(groupId))
                {
                    item.groupId = string.Empty;
                }
            }
            foreach (var item in config.subItem)
            {
                if (item.groupId.Equals(groupId))
                {
                    item.groupId = string.Empty;
                }
            }

            ToJsonFile(config);
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

        public static int SaveRouting(ref Config config)
        {
            if (config.routings == null)
            {
                return -1;
            }

            foreach (var item in config.routings)
            {

            }
            //move locked item
            int index = config.routings.FindIndex(it => it.locked == true);
            if (index != -1)
            {
                var item = Utils.DeepCopy(config.routings[index]);
                config.routings.RemoveAt(index);
                config.routings.Add(item);
            }
            if (config.routingIndex >= config.routings.Count)
            {
                config.routingIndex = 0;
            }

            Global.reloadV2ray = true;

            ToJsonFile(config);
            return 0;
        }

        public static int AddRoutingItem(ref Config config, RoutingItem item, int index)
        {
            if (index >= 0)
            {
                config.routings[index] = item;
            }
            else
            {
                config.routings.Add(item);
                int indexLocked = config.routings.FindIndex(it => it.locked == true);
                if (indexLocked != -1)
                {
                    var itemLocked = Utils.DeepCopy(config.routings[indexLocked]);
                    config.routings.RemoveAt(indexLocked);
                    config.routings.Add(itemLocked);
                }
            }
            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// AddBatchRoutingRules
        /// </summary>
        /// <param name="config"></param>
        /// <param name="clipboardData"></param>
        /// <returns></returns>
        public static int AddBatchRoutingRules(ref RoutingItem routingItem, string clipboardData, bool blReplace = true)
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
            if (routingItem.rules == null)
            {
                routingItem.rules = new List<RulesItem>();
            }
            if (blReplace)
            {
                routingItem.rules.Clear();
            }
            foreach (var item in lstRules)
            {
                routingItem.rules.Add(item);
            }
            return 0;
        }

        /// <summary>
        /// MoveRoutingRule
        /// </summary>
        /// <param name="routingItem"></param>
        /// <param name="index"></param>
        /// <param name="eMove"></param>
        /// <returns></returns>
        public static int MoveRoutingRule(ref RoutingItem routingItem, int index, EMove eMove)
        {
            int count = routingItem.rules.Count;
            if (index < 0 || index > routingItem.rules.Count - 1)
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
                        var item = Utils.DeepCopy(routingItem.rules[index]);
                        routingItem.rules.RemoveAt(index);
                        routingItem.rules.Insert(0, item);

                        break;
                    }
                case EMove.Up:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        var item = Utils.DeepCopy(routingItem.rules[index]);
                        routingItem.rules.RemoveAt(index);
                        routingItem.rules.Insert(index - 1, item);

                        break;
                    }

                case EMove.Down:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        var item = Utils.DeepCopy(routingItem.rules[index]);
                        routingItem.rules.RemoveAt(index);
                        routingItem.rules.Insert(index + 1, item);

                        break;
                    }
                case EMove.Bottom:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        var item = Utils.DeepCopy(routingItem.rules[index]);
                        routingItem.rules.RemoveAt(index);
                        routingItem.rules.Add(item);

                        break;
                    }

            }
            return 0;
        }

        public static int SetDefaultRouting(ref Config config, int index)
        {
            if (index < 0 || index > config.routings.Count - 1)
            {
                return -1;
            }

            ////和现在相同
            //if (config.index.Equals(index))
            //{
            //    return -1;
            //}
            config.routingIndex = index;
            Global.reloadV2ray = true;

            ToJsonFile(config);

            return 0;
        }

        public static int InitBuiltinRouting(ref Config config, bool blImportAdvancedRules = false)
        {
            if (config.routings == null)
            {
                config.routings = new List<RoutingItem>();
            }

            if (blImportAdvancedRules || config.routings.Count(it => it.locked != true) <= 0)
            {
                //Bypass the mainland
                var item2 = new RoutingItem()
                {
                    remarks = "绕过大陆(Whitelist)",
                    url = string.Empty,
                };
                AddBatchRoutingRules(ref item2, Utils.GetEmbedText(Global.CustomRoutingFileName + "white"));
                config.routings.Add(item2);

                //Blacklist
                var item3 = new RoutingItem()
                {
                    remarks = "黑名单(Blacklist)",
                    url = string.Empty,
                };
                AddBatchRoutingRules(ref item3, Utils.GetEmbedText(Global.CustomRoutingFileName + "black"));
                config.routings.Add(item3);

                //Global
                var item1 = new RoutingItem()
                {
                    remarks = "全局(Global)",
                    url = string.Empty,
                };
                AddBatchRoutingRules(ref item1, Utils.GetEmbedText(Global.CustomRoutingFileName + "global"));
                config.routings.Add(item1);

                if (!blImportAdvancedRules)
                {
                    config.routingIndex = 0;
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
                config.routings.Add(item1);
            }

            SaveRouting(ref config);
            return 0;
        }

        public static RoutingItem GetLockedRoutingItem(ref Config config)
        {
            if (config.routings == null)
            {
                return null;
            }
            return config.routings.Find(it => it.locked == true);
        }
        #endregion
    }
}
