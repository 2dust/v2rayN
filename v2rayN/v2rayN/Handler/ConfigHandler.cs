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
            if (config == null)
            {
                config = new Config
                {
                    index = -1,
                    logEnabled = false,
                    loglevel = "warning",
                    vmess = new List<VmessItem>(),

                    //Mux
                    muxEnabled = false,

                    ////默认监听端口
                    //config.pacPort = 8888;

                    // 默认不开启统计
                    enableStatistics = false,

                    // 默认中等刷新率
                    statisticsFreshRate = (int)Global.StatisticsFreshRate.medium
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
                //http协议不由core提供,只保留socks
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
            if (Utils.IsNullOrEmpty(config.routingMode))
            {
                config.routingMode = "0";
            }
            if (config.useragent == null)
            {
                config.useragent = new List<string>();
            }
            if (config.userdirect == null)
            {
                config.userdirect = new List<string>();
            }
            if (config.userblock == null)
            {
                config.userblock = new List<string>();
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
                config.uiItem = new UIItem();
            }
            if (config.uiItem.mainLvColWidth == null)
            {
                config.uiItem.mainLvColWidth = new Dictionary<string, int>();
            }

            //// 如果是用户升级，首次会有端口号为0的情况，不可用，这里处理
            //if (config.pacPort == 0)
            //{
            //    config.pacPort = 8888;
            //}
            if (Utils.IsNullOrEmpty(config.speedTestUrl))
            {
                config.speedTestUrl = Global.SpeedTestUrl;
            }
            if (Utils.IsNullOrEmpty(config.speedPingTestUrl))
            {
                config.speedPingTestUrl = Global.SpeedPingTestUrl;
            }
            if (Utils.IsNullOrEmpty(config.urlGFWList))
            {
                config.urlGFWList = Global.GFWLIST_URL;
            }
            //if (Utils.IsNullOrEmpty(config.remoteDNS))
            //{
            //    config.remoteDNS = "1.1.1.1";
            //}

            if (config.subItem == null)
            {
                config.subItem = new List<SubItem>();
            }
            if (config.userPacRule == null)
            {
                config.userPacRule = new List<string>();
            }

            if (config == null
                || config.index < 0
                || config.vmess.Count <= 0
                || config.index > config.vmess.Count - 1
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
                }
            }

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int AddServer(ref Config config, VmessItem vmessItem, int index)
        {
            vmessItem.configVersion = 2;
            vmessItem.configType = (int)EConfigType.Vmess;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();
            vmessItem.network = vmessItem.network.TrimEx();
            vmessItem.headerType = vmessItem.headerType.TrimEx();
            vmessItem.requestHost = vmessItem.requestHost.TrimEx();
            vmessItem.path = vmessItem.path.TrimEx();
            vmessItem.streamSecurity = vmessItem.streamSecurity.TrimEx();

            if (index >= 0)
            {
                //修改
                config.vmess[index] = vmessItem;
                if (config.index.Equals(index))
                {
                    Global.reloadV2ray = true;
                }
            }
            else
            {
                //添加
                if (Utils.IsNullOrEmpty(vmessItem.allowInsecure))
                {
                    vmessItem.allowInsecure = config.defAllowInsecure.ToString();
                }
                config.vmess.Add(vmessItem);
                if (config.vmess.Count == 1)
                {
                    config.index = 0;
                    Global.reloadV2ray = true;
                }
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 移除服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int RemoveServer(ref Config config, int index)
        {
            if (index < 0 || index > config.vmess.Count - 1)
            {
                return -1;
            }

            //删除
            config.vmess.RemoveAt(index);


            //移除的是活动的
            if (config.index.Equals(index))
            {
                if (config.vmess.Count > 0)
                {
                    config.index = 0;
                }
                else
                {
                    config.index = -1;
                }
                Global.reloadV2ray = true;
            }
            else if (index < config.index)//移除活动之前的
            {
                config.index--;
                Global.reloadV2ray = true;
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
        public static int CopyServer(ref Config config, int index)
        {
            if (index < 0 || index > config.vmess.Count - 1)
            {
                return -1;
            }

            VmessItem vmessItem = new VmessItem
            {
                configVersion = config.vmess[index].configVersion,
                address = config.vmess[index].address,
                port = config.vmess[index].port,
                id = config.vmess[index].id,
                alterId = config.vmess[index].alterId,
                security = config.vmess[index].security,
                network = config.vmess[index].network,
                remarks = string.Format("{0}-clone", config.vmess[index].remarks),
                headerType = config.vmess[index].headerType,
                requestHost = config.vmess[index].requestHost,
                path = config.vmess[index].path,
                streamSecurity = config.vmess[index].streamSecurity,
                allowInsecure = config.vmess[index].allowInsecure,
                configType = config.vmess[index].configType
            };

            config.vmess.Insert(index + 1, vmessItem); // 插入到下一项

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 设置活动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int SetDefaultServer(ref Config config, int index)
        {
            if (index < 0 || index > config.vmess.Count - 1)
            {
                return -1;
            }

            ////和现在相同
            //if (config.index.Equals(index))
            //{
            //    return -1;
            //}
            config.index = index;
            Global.reloadV2ray = true;

            ToJsonFile(config);

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
            Utils.ToJsonFile(config, Utils.GetPath(configRes));
        }

        /// <summary>
        /// 取得服务器QRCode配置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static string GetVmessQRCode(Config config, int index)
        {
            try
            {
                string url = string.Empty;

                VmessItem vmessItem = config.vmess[index];
                if (vmessItem.configType == (int)EConfigType.Vmess)
                {
                    VmessQRCode vmessQRCode = new VmessQRCode
                    {
                        v = vmessItem.configVersion.ToString(),
                        ps = vmessItem.remarks.TrimEx(), //备注也许很长 ;
                        add = vmessItem.address,
                        port = vmessItem.port.ToString(),
                        id = vmessItem.id,
                        aid = vmessItem.alterId.ToString(),
                        net = vmessItem.network,
                        type = vmessItem.headerType,
                        host = vmessItem.requestHost,
                        path = vmessItem.path,
                        tls = vmessItem.streamSecurity
                    };

                    url = Utils.ToJson(vmessQRCode);
                    url = Utils.Base64Encode(url);
                    url = string.Format("{0}{1}", Global.vmessProtocol, url);

                }
                else if (vmessItem.configType == (int)EConfigType.Shadowsocks)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(vmessItem.remarks))
                    {
                        remark = "#" + WebUtility.UrlEncode(vmessItem.remarks);
                    }
                    url = string.Format("{0}:{1}@{2}:{3}",
                        vmessItem.security,
                        vmessItem.id,
                        vmessItem.address,
                        vmessItem.port);
                    url = Utils.Base64Encode(url);
                    url = string.Format("{0}{1}{2}", Global.ssProtocol, url, remark);
                }
                else if (vmessItem.configType == (int)EConfigType.Socks)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(vmessItem.remarks))
                    {
                        remark = "#" + WebUtility.UrlEncode(vmessItem.remarks);
                    }
                    url = string.Format("{0}:{1}@{2}:{3}",
                        vmessItem.security,
                        vmessItem.id,
                        vmessItem.address,
                        vmessItem.port);
                    url = Utils.Base64Encode(url);
                    url = string.Format("{0}{1}{2}", Global.socksProtocol, url, remark);
                }
                else if (vmessItem.configType == (int)EConfigType.Trojan)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(vmessItem.remarks))
                    {
                        remark = "#" + WebUtility.UrlEncode(vmessItem.remarks);
                    }
                    url = string.Format("{0}@{1}:{2}",
                        vmessItem.id,
                        vmessItem.address,
                        vmessItem.port);
                    url = string.Format("{0}{1}{2}", Global.trojanProtocol, url, remark);
                }
                else
                {
                }
                return url;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 移动服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <param name="eMove"></param>
        /// <returns></returns>
        public static int MoveServer(ref Config config, int index, EMove eMove)
        {
            int count = config.vmess.Count;
            if (index < 0 || index > config.vmess.Count - 1)
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
                        VmessItem vmess = Utils.DeepCopy(config.vmess[index]);
                        config.vmess.RemoveAt(index);
                        config.vmess.Insert(0, vmess);
                        if (index < config.index)
                        {
                            //
                        }
                        else if (config.index == index)
                        {
                            config.index = 0;
                        }
                        else
                        {
                            config.index++;
                        }
                        break;
                    }
                case EMove.Up:
                    {
                        if (index == 0)
                        {
                            return 0;
                        }
                        VmessItem vmess = Utils.DeepCopy(config.vmess[index]);
                        config.vmess.RemoveAt(index);
                        config.vmess.Insert(index - 1, vmess);
                        if (index == config.index + 1)
                        {
                            config.index++;
                        }
                        else if (config.index == index)
                        {
                            config.index--;
                        }
                        break;
                    }

                case EMove.Down:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        VmessItem vmess = Utils.DeepCopy(config.vmess[index]);
                        config.vmess.RemoveAt(index);
                        config.vmess.Insert(index + 1, vmess);
                        if (index == config.index - 1)
                        {
                            config.index--;
                        }
                        else if (config.index == index)
                        {
                            config.index++;
                        }
                        break;
                    }
                case EMove.Bottom:
                    {
                        if (index == count - 1)
                        {
                            return 0;
                        }
                        VmessItem vmess = Utils.DeepCopy(config.vmess[index]);
                        config.vmess.RemoveAt(index);
                        config.vmess.Add(vmess);
                        if (index < config.index)
                        {
                            config.index--;
                        }
                        else if (config.index == index)
                        {
                            config.index = count - 1;
                        }
                        else
                        {
                            //
                        }
                        break;
                    }

            }
            Global.reloadV2ray = true;

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加自定义服务器
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static int AddCustomServer(ref Config config, string fileName)
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
                address = newFileName,
                configType = (int)EConfigType.Custom,
                remarks = string.Format("import custom@{0}", DateTime.Now.ToShortDateString())
            };

            config.vmess.Add(vmessItem);
            if (config.vmess.Count == 1)
            {
                config.index = 0;
                Global.reloadV2ray = true;
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int EditCustomServer(ref Config config, VmessItem vmessItem, int index)
        {
            //修改
            config.vmess[index] = vmessItem;
            if (config.index.Equals(index))
            {
                Global.reloadV2ray = true;
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int AddShadowsocksServer(ref Config config, VmessItem vmessItem, int index)
        {
            vmessItem.configVersion = 2;
            vmessItem.configType = (int)EConfigType.Shadowsocks;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();

            if (index >= 0)
            {
                //修改
                config.vmess[index] = vmessItem;
                if (config.index.Equals(index))
                {
                    Global.reloadV2ray = true;
                }
            }
            else
            {
                //添加
                config.vmess.Add(vmessItem);
                if (config.vmess.Count == 1)
                {
                    config.index = 0;
                    Global.reloadV2ray = true;
                }
            }

            ToJsonFile(config);

            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int AddSocksServer(ref Config config, VmessItem vmessItem, int index)
        {
            vmessItem.configVersion = 2;
            vmessItem.configType = (int)EConfigType.Socks;

            vmessItem.address = vmessItem.address.TrimEx();

            if (index >= 0)
            {
                //修改
                config.vmess[index] = vmessItem;
                if (config.index.Equals(index))
                {
                    Global.reloadV2ray = true;
                }
            }
            else
            {
                //添加
                config.vmess.Add(vmessItem);
                if (config.vmess.Count == 1)
                {
                    config.index = 0;
                    Global.reloadV2ray = true;
                }
            }

            ToJsonFile(config);

            return 0;
        }


        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int AddTrojanServer(ref Config config, VmessItem vmessItem, int index)
        {
            vmessItem.configVersion = 2;
            vmessItem.configType = (int)EConfigType.Trojan;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();

            vmessItem.streamSecurity = Global.StreamSecurity;
            vmessItem.allowInsecure = "false";

            if (index >= 0)
            {
                //修改
                config.vmess[index] = vmessItem;
                if (config.index.Equals(index))
                {
                    Global.reloadV2ray = true;
                }
            }
            else
            {
                //添加
                config.vmess.Add(vmessItem);
                if (config.vmess.Count == 1)
                {
                    config.index = 0;
                    Global.reloadV2ray = true;
                }
            }

            ToJsonFile(config);

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
        public static int AddBatchServers(ref Config config, string clipboardData, string subid = "")
        {
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
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
                if (str.StartsWith(Global.httpsProtocol) || str.StartsWith(Global.httpProtocol))
                {
                    if (AddSubItem(ref config, str) == 0)
                    {
                        countServers++;
                    }
                    continue;
                }
                VmessItem vmessItem = V2rayConfigHandler.ImportFromClipboardConfig(str, out string msg);
                if (vmessItem == null)
                {
                    continue;
                }
                vmessItem.subid = subid;
                if (vmessItem.configType == (int)EConfigType.Vmess)
                {
                    if (AddServer(ref config, vmessItem, -1) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.Shadowsocks)
                {
                    if (AddShadowsocksServer(ref config, vmessItem, -1) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.Socks)
                {
                    if (AddSocksServer(ref config, vmessItem, -1) == 0)
                    {
                        countServers++;
                    }
                }
                else if (vmessItem.configType == (int)EConfigType.Trojan)
                {
                    if (AddTrojanServer(ref config, vmessItem, -1) == 0)
                    {
                        countServers++;
                    }
                }
            }
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
            foreach (SubItem sub in config.subItem)
            {
                if (url == sub.url)
                {
                    return 0;
                }
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
            if (config.subItem == null || config.subItem.Count <= 0)
            {
                return -1;
            }

            foreach (SubItem sub in config.subItem)
            {
                if (Utils.IsNullOrEmpty(sub.id))
                {
                    sub.id = Utils.GetGUID();
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

        public static int SortServers(ref Config config, EServerColName name, bool asc)
        {
            if (config.vmess.Count <= 0)
            {
                return -1;
            }
            switch (name)
            {
                case EServerColName.configType:
                case EServerColName.remarks:
                case EServerColName.address:
                case EServerColName.port:
                case EServerColName.security:
                case EServerColName.network:
                case EServerColName.testResult:
                    break;
                default:
                    return -1;
            }
            string itemId = config.getItemId();
            var items = config.vmess.AsQueryable();

            if (asc)
            {
                config.vmess = items.OrderBy(name.ToString()).ToList();
            }
            else
            {
                config.vmess = items.OrderByDescending(name.ToString()).ToList();
            }

            var index_ = config.vmess.FindIndex(it => it.getItemId() == itemId);
            if (index_ >= 0)
            {
                config.index = index_;
            }

            ToJsonFile(config);
            return 0;
        }

        /// <summary>
        /// 添加服务器或编辑
        /// </summary>
        /// <param name="config"></param>
        /// <param name="vmessItem"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int AddVlessServer(ref Config config, VmessItem vmessItem, int index)
        {
            vmessItem.configVersion = 2;
            vmessItem.configType = (int)EConfigType.VLESS;

            vmessItem.address = vmessItem.address.TrimEx();
            vmessItem.id = vmessItem.id.TrimEx();
            vmessItem.security = vmessItem.security.TrimEx();
            vmessItem.network = vmessItem.network.TrimEx();
            vmessItem.headerType = vmessItem.headerType.TrimEx();
            vmessItem.requestHost = vmessItem.requestHost.TrimEx();
            vmessItem.path = vmessItem.path.TrimEx();
            vmessItem.streamSecurity = vmessItem.streamSecurity.TrimEx();

            if (index >= 0)
            {
                //修改
                config.vmess[index] = vmessItem;
                if (config.index.Equals(index))
                {
                    Global.reloadV2ray = true;
                }
            }
            else
            {
                //添加
                if (Utils.IsNullOrEmpty(vmessItem.allowInsecure))
                {
                    vmessItem.allowInsecure = config.defAllowInsecure.ToString();
                }
                config.vmess.Add(vmessItem);
                if (config.vmess.Count == 1)
                {
                    config.index = 0;
                    Global.reloadV2ray = true;
                }
            }

            ToJsonFile(config);

            return 0;
        }

    }
}
