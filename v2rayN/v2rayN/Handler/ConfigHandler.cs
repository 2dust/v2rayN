﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using v2rayN.Mode;
using v2rayN.Base;
using System.Linq;
using v2rayN.Tool;
using System.Threading.Tasks;

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

                    enableStatistics = false,

                    statisticsFreshRate = 1,

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
            if (config.statisticsFreshRate > 100)
            {
                config.statisticsFreshRate = 1;
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
            lock (objLock)
            {
                try
                {

                    //save temp file
                    var resPath = Utils.GetPath(configRes);
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
            vmessItem.configType = EConfigType.VMess;

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
                    RemoveVmessItem(config, index);
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
        public static int CopyServer(ref Config config, List<VmessItem> indexs)
        {
            foreach (var item in indexs)
            {
                VmessItem vmessItem = Utils.DeepCopy(item);
                vmessItem.indexId = string.Empty;
                vmessItem.remarks = $"{item.remarks}-clone";

                if (vmessItem.configType == EConfigType.Custom)
                {
                    vmessItem.address = Utils.GetConfigPath(vmessItem.address);
                    if (AddCustomServer(ref config, vmessItem, false) == 0)
                    {
                    }
                }
                else
                {
                    AddServerCommon(ref config, vmessItem);
                }
            }

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
        /// 移动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="lstVmess"></param>
        /// <param name="index"></param>
        /// <param name="eMove"></param>
        /// <returns></returns>
        public static int MoveServer(ref Config config, ref List<VmessItem> lstVmess, int index, EMove eMove, int pos = -1)
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
                case EMove.Position:
                    lstVmess[index].sort = pos * 10 + 1;
                    break;
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加自定义服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <returns></returns>
        public static int AddCustomServer(ref Config config, VmessItem vmessItem, bool blDelete)
        {
            var fileName = vmessItem.address;
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

            vmessItem.address = newFileName;
            vmessItem.configType = EConfigType.Custom;
            if (Utils.IsNullOrEmpty(vmessItem.remarks))
            {
                vmessItem.remarks = $"import custom@{DateTime.Now.ToShortDateString()}";
            }


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
            vmessItem.configType = EConfigType.Shadowsocks;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();

            if (!LazyConfig.Instance.GetShadowsocksSecuritys().Contains(vmessItem.security))
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
            vmessItem.configType = EConfigType.Socks;

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
            vmessItem.configType = EConfigType.Trojan;

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
                if (vmessItem.configType == EConfigType.VMess)
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
            vmessItem.configType = EConfigType.VLESS;

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
                        RemoveVmessItem(config, index);
                    }
                }
            }
            //if (!keepOlder) list.Reverse();
            //config.vmess = list;

            return list.Count;
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
            else if (vmessItem.indexId == config.indexId)
            {
                Global.reloadV2ray = true;
            }
            if (!config.vmess.Exists(it => it.indexId == vmessItem.indexId))
            {
                var maxSort = config.vmess.Any() ? config.vmess.Max(t => t.sort) : 0;
                vmessItem.sort = maxSort++;

                config.vmess.Add(vmessItem);
            }

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
                && (!remarks || o.remarks == n.remarks);
        }

        private static int RemoveVmessItem(Config config, int index)
        {
            try
            {
                if (config.vmess[index].configType == EConfigType.Custom)
                {
                    File.Delete(Utils.GetConfigPath(config.vmess[index].address));
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog("RemoveVmessItem", ex);
            }
            config.vmess.RemoveAt(index);

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
        private static int AddBatchServers(ref Config config, string clipboardData, string subid, List<VmessItem> lstOriSub, string groupId)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            //copy sub items
            if (!Utils.IsNullOrEmpty(subid))
            {
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

                if (vmessItem.configType == EConfigType.VMess)
                {
                    if (AddServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == EConfigType.Shadowsocks)
                {
                    if (AddShadowsocksServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == EConfigType.Socks)
                {
                    if (AddSocksServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == EConfigType.Trojan)
                {
                    if (AddTrojanServer(ref config, vmessItem, false) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == EConfigType.VLESS)
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

        private static int AddBatchServers4Custom(ref Config config, string clipboardData, string subid, List<VmessItem> lstOriSub, string groupId)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            VmessItem vmessItem = new VmessItem();
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

                vmessItem.coreType = ECoreType.Xray;
                vmessItem.address = fileName;
                vmessItem.remarks = "v2ray_custom";
            }
            //Is Clash configuration
            else if (clipboardData.IndexOf("port") >= 0
                && clipboardData.IndexOf("socks-port") >= 0
                && clipboardData.IndexOf("proxies") >= 0)
            {
                var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.yaml");
                File.WriteAllText(fileName, clipboardData);

                vmessItem.coreType = ECoreType.clash;
                vmessItem.address = fileName;
                vmessItem.remarks = "clash_custom";
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

                vmessItem.coreType = ECoreType.hysteria;
                vmessItem.address = fileName;
                vmessItem.remarks = "hysteria_custom";
            }
            //Is naiveproxy configuration
            else if (clipboardData.IndexOf("listen") >= 0
                && clipboardData.IndexOf("proxy") >= 0
                && clipboardData.IndexOf("<html>") < 0
                && clipboardData.IndexOf("<body>") < 0)
            {
                var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.json");
                File.WriteAllText(fileName, clipboardData);

                vmessItem.coreType = ECoreType.naiveproxy;
                vmessItem.address = fileName;
                vmessItem.remarks = "naiveproxy_custom";
            }
            //Is Other configuration
            else
            {
                return -1;
                //var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.txt");
                //File.WriteAllText(fileName, clipboardData);

                //vmessItem.address = fileName;
                //vmessItem.remarks = "other_custom";
            }

            if (!Utils.IsNullOrEmpty(subid))
            {
                RemoveServerViaSubid(ref config, subid);
            }
            if (lstOriSub != null && lstOriSub.Count == 1)
            {
                vmessItem.indexId = lstOriSub[0].indexId;
            }
            vmessItem.subid = subid;
            vmessItem.groupId = groupId;

            if (Utils.IsNullOrEmpty(vmessItem.address))
            {
                return -1;
            }

            if (AddCustomServer(ref config, vmessItem, true) == 0)
            {
                return 1;

            }
            else
            {
                return -1;
            }
        }

        private static int AddBatchServers4SsSIP008(ref Config config, string clipboardData, string subid, List<VmessItem> lstOriSub, string groupId)
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }

            if (!Utils.IsNullOrEmpty(subid))
            {
                RemoveServerViaSubid(ref config, subid);
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
                    var ssItem = new VmessItem()
                    {
                        subid = subid,
                        groupId = groupId,
                        remarks = it.remarks,
                        security = it.method,
                        id = it.password,
                        address = it.server,
                        port = Utils.ToInt(it.server_port)
                    };
                    if (AddShadowsocksServer(ref config, ssItem, false) == 0)
                    {
                        counter++;
                    }
                }
                ToJsonFile(config);
                return counter;
            }

            return -1;
        }

        public static int AddBatchServers(ref Config config, string clipboardData, string subid, string groupId)
        {
            List<VmessItem> lstOriSub = null;
            if (!Utils.IsNullOrEmpty(subid))
            {
                lstOriSub = config.vmess.Where(it => it.subid == subid).ToList();
            }

            int counter = AddBatchServers(ref config, clipboardData, subid, lstOriSub, groupId);
            if (counter < 1)
            {
                counter = AddBatchServers(ref config, Utils.Base64Decode(clipboardData), subid, lstOriSub, groupId);
            }

            if (counter < 1)
            {
                counter = AddBatchServers4SsSIP008(ref config, clipboardData, subid, lstOriSub, groupId);
            }

            //maybe other sub 
            if (counter < 1)
            {
                counter = AddBatchServers4Custom(ref config, clipboardData, subid, lstOriSub, groupId);
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

            foreach (var item in config.subItem.Where(item => Utils.IsNullOrEmpty(item.id)))
            {
                item.id = Utils.GetGUID(false);
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
                    RemoveVmessItem(config, k);
                }
            }

            ToJsonFile(config);
            return 0;
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

            foreach (var item in config.groupItem.Where(item => Utils.IsNullOrEmpty(item.id)))
            {
                item.id = Utils.GetGUID(false);
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

        public static int MoveServerToGroup(Config config, List<VmessItem> indexs, string groupId)
        {
            foreach (var item in indexs)
            {
                item.groupId = groupId;
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
            if (config.trayMenuServersLimit <= 0)
            {
                config.trayMenuServersLimit = 30;
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
        public static int MoveRoutingRule(ref RoutingItem routingItem, int index, EMove eMove, int pos = -1)
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
                case EMove.Position:
                    {
                        var removeItem = routingItem.rules[index];
                        var item = Utils.DeepCopy(routingItem.rules[index]);
                        routingItem.rules.Insert(pos, item);
                        routingItem.rules.Remove(removeItem);
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
