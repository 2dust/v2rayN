using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    /// <summary>
    /// v2ray配置文件处理类
    /// </summary>
    class V2rayConfigHandler
    {
        private static string SampleClient = Global.v2raySampleClient;
        private static string SampleServer = Global.v2raySampleServer;

        #region 生成客户端配置

        /// <summary>
        /// 生成v2ray的客户端配置文件
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static int GenerateClientConfig(Config config, string fileName, bool blExport, out string msg)
        {
            try
            {
                //检查GUI设置
                if (config == null
                    || config.index < 0
                    || config.vmess.Count <= 0
                    || config.index > config.vmess.Count - 1
                    )
                {
                    msg = UIRes.I18N("CheckServerSettings");
                    return -1;
                }

                msg = UIRes.I18N("InitialConfiguration");
                if (config.configType() == (int)EConfigType.Custom)
                {
                    return GenerateClientCustomConfig(config, fileName, out msg);
                }

                //取得默认配置
                string result = Utils.GetEmbedText(SampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedGetDefaultConfiguration");
                    return -1;
                }

                //转成Json
                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = UIRes.I18N("FailedGenDefaultConfiguration");
                    return -1;
                }

                //开始修改配置
                log(config, ref v2rayConfig, blExport);

                //本地端口
                inbound(config, ref v2rayConfig);

                //路由
                routing(config, ref v2rayConfig);

                //outbound
                outbound(config, ref v2rayConfig);

                //dns
                dns(config, ref v2rayConfig);

                // TODO: 统计配置
                statistic(config, ref v2rayConfig);

                Utils.ToJsonFile(v2rayConfig, fileName, false);

                msg = string.Format(UIRes.I18N("SuccessfulConfiguration"), config.getSummary());
            }
            catch
            {
                msg = UIRes.I18N("FailedGenDefaultConfiguration");
                return -1;
            }
            return 0;
        }

        /// <summary>
        /// 日志
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int log(Config config, ref V2rayConfig v2rayConfig, bool blExport)
        {
            try
            {
                if (blExport)
                {
                    if (config.logEnabled)
                    {
                        v2rayConfig.log.loglevel = config.loglevel;
                    }
                    else
                    {
                        v2rayConfig.log.loglevel = config.loglevel;
                        v2rayConfig.log.access = "";
                        v2rayConfig.log.error = "";
                    }
                }
                else
                {
                    if (config.logEnabled)
                    {
                        v2rayConfig.log.loglevel = config.loglevel;
                        v2rayConfig.log.access = Utils.GetPath(v2rayConfig.log.access);
                        v2rayConfig.log.error = Utils.GetPath(v2rayConfig.log.error);
                    }
                    else
                    {
                        v2rayConfig.log.loglevel = config.loglevel;
                        v2rayConfig.log.access = "";
                        v2rayConfig.log.error = "";
                    }
                }
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// 本地端口
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int inbound(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                Inbounds inbound = v2rayConfig.inbounds[0];
                inbound.tag = Global.InboundSocks;
                inbound.port = config.inbound[0].localPort;
                inbound.protocol = config.inbound[0].protocol;
                if (config.allowLANConn)
                {
                    inbound.listen = "0.0.0.0";
                }
                else
                {
                    inbound.listen = Global.Loopback;
                }
                //udp
                inbound.settings.udp = config.inbound[0].udpEnabled;
                inbound.sniffing.enabled = config.inbound[0].sniffingEnabled;

                //http
                Inbounds inbound2 = v2rayConfig.inbounds[1];
                inbound2.tag = Global.InboundHttp;
                inbound2.port = config.GetLocalPort(Global.InboundHttp);
                inbound2.protocol = Global.InboundHttp;
                inbound2.listen = inbound.listen;
                inbound2.settings.allowTransparent = false;
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// 路由
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int routing(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.routing != null
                  && v2rayConfig.routing.rules != null)
                {
                    v2rayConfig.routing.domainStrategy = config.domainStrategy;
                    v2rayConfig.routing.domainMatcher = config.domainMatcher;

                    if (config.enableRoutingAdvanced)
                    {
                        if (config.routings != null && config.routingIndex < config.routings.Count)
                        {
                            foreach (var item in config.routings[config.routingIndex].rules)
                            {
                                if (item.enabled)
                                {
                                    routingUserRule(item, ref v2rayConfig);
                                }
                            }
                        }
                    }
                    else
                    {
                        var lockedItem = ConfigHandler.GetLockedRoutingItem(ref config);
                        if (lockedItem != null)
                        {
                            foreach (var item in lockedItem.rules)
                            {
                                routingUserRule(item, ref v2rayConfig);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return 0;
        }
        private static int routingUserRule(RulesItem rules, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (rules == null)
                {
                    return 0;
                }
                if (Utils.IsNullOrEmpty(rules.port))
                {
                    rules.port = null;
                }
                if (rules.domain != null && rules.domain.Count == 0)
                {
                    rules.domain = null;
                }
                if (rules.ip != null && rules.ip.Count == 0)
                {
                    rules.ip = null;
                }
                if (rules.protocol != null && rules.protocol.Count == 0)
                {
                    rules.protocol = null;
                }

                var hasDomainIp = false;
                if (rules.domain != null && rules.domain.Count > 0)
                {
                    var it = Utils.DeepCopy(rules);
                    it.ip = null;
                    it.type = "field";
                    for (int k = it.domain.Count - 1; k >= 0; k--)
                    {
                        if (it.domain[k].StartsWith("#"))
                        {
                            it.domain.RemoveAt(k);
                        }
                        it.domain[k] = it.domain[k].Replace(Global.RoutingRuleComma, ",");
                    }
                    //if (Utils.IsNullOrEmpty(it.port))
                    //{
                    //    it.port = null;
                    //}
                    //if (it.protocol != null && it.protocol.Count == 0)
                    //{
                    //    it.protocol = null;
                    //}
                    v2rayConfig.routing.rules.Add(it);
                    hasDomainIp = true;
                }
                if (rules.ip != null && rules.ip.Count > 0)
                {
                    var it = Utils.DeepCopy(rules);
                    it.domain = null;
                    it.type = "field";
                    //if (Utils.IsNullOrEmpty(it.port))
                    //{
                    //    it.port = null;
                    //}
                    //if (it.protocol != null && it.protocol.Count == 0)
                    //{
                    //    it.protocol = null;
                    //}
                    v2rayConfig.routing.rules.Add(it);
                    hasDomainIp = true;
                }
                if (!hasDomainIp)
                {
                    if (!Utils.IsNullOrEmpty(rules.port))
                    {
                        var it = Utils.DeepCopy(rules);
                        //it.domain = null;
                        //it.ip = null;
                        //if (it.protocol != null && it.protocol.Count == 0)
                        //{
                        //    it.protocol = null;
                        //}
                        it.type = "field";
                        v2rayConfig.routing.rules.Add(it);
                    }
                    else if (rules.protocol != null && rules.protocol.Count > 0)
                    {
                        var it = Utils.DeepCopy(rules);
                        //it.domain = null;
                        //it.ip = null;
                        //if (Utils.IsNullOrEmpty(it.port))
                        //{
                        //    it.port = null;
                        //}
                        it.type = "field";
                        v2rayConfig.routing.rules.Add(it);
                    }
                }
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// vmess协议服务器配置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int outbound(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                Outbounds outbound = v2rayConfig.outbounds[0];
                if (config.configType() == (int)EConfigType.Vmess)
                {
                    VnextItem vnextItem;
                    if (outbound.settings.vnext.Count <= 0)
                    {
                        vnextItem = new VnextItem();
                        outbound.settings.vnext.Add(vnextItem);
                    }
                    else
                    {
                        vnextItem = outbound.settings.vnext[0];
                    }
                    //远程服务器地址和端口
                    vnextItem.address = config.address();
                    vnextItem.port = config.port();

                    UsersItem usersItem;
                    if (vnextItem.users.Count <= 0)
                    {
                        usersItem = new UsersItem();
                        vnextItem.users.Add(usersItem);
                    }
                    else
                    {
                        usersItem = vnextItem.users[0];
                    }
                    //远程服务器用户ID
                    usersItem.id = config.id();
                    usersItem.alterId = config.alterId();
                    usersItem.email = Global.userEMail;
                    if (Global.vmessSecuritys.Contains(config.security()))
                    {
                        usersItem.security = config.security();
                    }
                    else
                    {
                        usersItem.security = Global.DefaultSecurity;
                    }

                    //Mux
                    outbound.mux.enabled = config.muxEnabled;
                    outbound.mux.concurrency = config.muxEnabled ? 8 : -1;

                    //远程服务器底层传输配置
                    StreamSettings streamSettings = outbound.streamSettings;
                    boundStreamSettings(config, "out", ref streamSettings);

                    outbound.protocol = Global.vmessProtocolLite;
                    outbound.settings.servers = null;
                }
                else if (config.configType() == (int)EConfigType.Shadowsocks)
                {
                    ServersItem serversItem;
                    if (outbound.settings.servers.Count <= 0)
                    {
                        serversItem = new ServersItem();
                        outbound.settings.servers.Add(serversItem);
                    }
                    else
                    {
                        serversItem = outbound.settings.servers[0];
                    }
                    //远程服务器地址和端口
                    serversItem.address = config.address();
                    serversItem.port = config.port();
                    serversItem.password = config.id();
                    if (Global.ssSecuritys.Contains(config.security()))
                    {
                        serversItem.method = config.security();
                    }
                    else
                    {
                        serversItem.method = "none";
                    }


                    serversItem.ota = false;
                    serversItem.level = 1;

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;


                    outbound.protocol = Global.ssProtocolLite;
                    outbound.settings.vnext = null;
                }
                else if (config.configType() == (int)EConfigType.Socks)
                {
                    ServersItem serversItem;
                    if (outbound.settings.servers.Count <= 0)
                    {
                        serversItem = new ServersItem();
                        outbound.settings.servers.Add(serversItem);
                    }
                    else
                    {
                        serversItem = outbound.settings.servers[0];
                    }
                    //远程服务器地址和端口
                    serversItem.address = config.address();
                    serversItem.port = config.port();
                    serversItem.method = null;
                    serversItem.password = null;

                    if (!Utils.IsNullOrEmpty(config.security())
                        && !Utils.IsNullOrEmpty(config.id()))
                    {
                        SocksUsersItem socksUsersItem = new SocksUsersItem
                        {
                            user = config.security(),
                            pass = config.id(),
                            level = 1
                        };

                        serversItem.users = new List<SocksUsersItem>() { socksUsersItem };
                    }

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;

                    outbound.protocol = Global.socksProtocolLite;
                    outbound.settings.vnext = null;
                }
                else if (config.configType() == (int)EConfigType.VLESS)
                {
                    VnextItem vnextItem;
                    if (outbound.settings.vnext.Count <= 0)
                    {
                        vnextItem = new VnextItem();
                        outbound.settings.vnext.Add(vnextItem);
                    }
                    else
                    {
                        vnextItem = outbound.settings.vnext[0];
                    }
                    //远程服务器地址和端口
                    vnextItem.address = config.address();
                    vnextItem.port = config.port();

                    UsersItem usersItem;
                    if (vnextItem.users.Count <= 0)
                    {
                        usersItem = new UsersItem();
                        vnextItem.users.Add(usersItem);
                    }
                    else
                    {
                        usersItem = vnextItem.users[0];
                    }
                    //远程服务器用户ID
                    usersItem.id = config.id();
                    usersItem.flow = string.Empty;
                    usersItem.email = Global.userEMail;
                    usersItem.encryption = config.security();

                    //Mux
                    outbound.mux.enabled = config.muxEnabled;
                    outbound.mux.concurrency = config.muxEnabled ? 8 : -1;

                    //远程服务器底层传输配置
                    StreamSettings streamSettings = outbound.streamSettings;
                    boundStreamSettings(config, "out", ref streamSettings);

                    //if xtls
                    if (config.streamSecurity() == Global.StreamSecurityX)
                    {
                        if (Utils.IsNullOrEmpty(config.flow()))
                        {
                            usersItem.flow = Global.xtlsFlows[1];
                        }
                        else
                        {
                            usersItem.flow = config.flow().Replace("splice", "direct");
                        }

                        outbound.mux.enabled = false;
                        outbound.mux.concurrency = -1;
                    }

                    outbound.protocol = Global.vlessProtocolLite;
                    outbound.settings.servers = null;
                }
                else if (config.configType() == (int)EConfigType.Trojan)
                {
                    ServersItem serversItem;
                    if (outbound.settings.servers.Count <= 0)
                    {
                        serversItem = new ServersItem();
                        outbound.settings.servers.Add(serversItem);
                    }
                    else
                    {
                        serversItem = outbound.settings.servers[0];
                    }
                    //远程服务器地址和端口
                    serversItem.address = config.address();
                    serversItem.port = config.port();
                    serversItem.password = config.id();
                    serversItem.flow = string.Empty;

                    serversItem.ota = false;
                    serversItem.level = 1;

                    //if xtls
                    if (config.streamSecurity() == Global.StreamSecurityX)
                    {
                        if (Utils.IsNullOrEmpty(config.flow()))
                        {
                            serversItem.flow = Global.xtlsFlows[1];
                        }
                        else
                        {
                            serversItem.flow = config.flow().Replace("splice", "direct");
                        }

                        outbound.mux.enabled = false;
                        outbound.mux.concurrency = -1;
                    }

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;


                    //远程服务器底层传输配置
                    StreamSettings streamSettings = outbound.streamSettings;
                    boundStreamSettings(config, "out", ref streamSettings);

                    outbound.protocol = Global.trojanProtocolLite;
                    outbound.settings.vnext = null;
                }
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// vmess协议远程服务器底层传输配置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="iobound"></param>
        /// <param name="streamSettings"></param>
        /// <returns></returns>
        private static int boundStreamSettings(Config config, string iobound, ref StreamSettings streamSettings)
        {
            try
            {
                //远程服务器底层传输配置
                streamSettings.network = config.network();
                string host = config.requestHost();
                string sni = config.sni();

                //if tls
                if (config.streamSecurity() == Global.StreamSecurity)
                {
                    streamSettings.security = config.streamSecurity();

                    TlsSettings tlsSettings = new TlsSettings
                    {
                        allowInsecure = config.allowInsecure(),
                        alpn = config.alpn()
                    };
                    if (!string.IsNullOrWhiteSpace(sni))
                    {
                        tlsSettings.serverName = sni;
                    }
                    else if (!string.IsNullOrWhiteSpace(host))
                    {
                        tlsSettings.serverName = Utils.String2List(host)[0];
                    }
                    streamSettings.tlsSettings = tlsSettings;
                }

                //if xtls
                if (config.streamSecurity() == Global.StreamSecurityX)
                {
                    streamSettings.security = config.streamSecurity();

                    TlsSettings xtlsSettings = new TlsSettings
                    {
                        allowInsecure = config.allowInsecure(),
                        alpn = config.alpn()
                    };
                    if (!string.IsNullOrWhiteSpace(sni))
                    {
                        xtlsSettings.serverName = sni;
                    }
                    else if (!string.IsNullOrWhiteSpace(host))
                    {
                        xtlsSettings.serverName = Utils.String2List(host)[0];
                    }
                    streamSettings.xtlsSettings = xtlsSettings;
                }

                //streamSettings
                switch (config.network())
                {
                    //kcp基本配置暂时是默认值，用户能自己设置伪装类型
                    case "kcp":
                        KcpSettings kcpSettings = new KcpSettings
                        {
                            mtu = config.kcpItem.mtu,
                            tti = config.kcpItem.tti
                        };
                        if (iobound.Equals("out"))
                        {
                            kcpSettings.uplinkCapacity = config.kcpItem.uplinkCapacity;
                            kcpSettings.downlinkCapacity = config.kcpItem.downlinkCapacity;
                        }
                        else if (iobound.Equals("in"))
                        {
                            kcpSettings.uplinkCapacity = config.kcpItem.downlinkCapacity; ;
                            kcpSettings.downlinkCapacity = config.kcpItem.downlinkCapacity;
                        }
                        else
                        {
                            kcpSettings.uplinkCapacity = config.kcpItem.uplinkCapacity;
                            kcpSettings.downlinkCapacity = config.kcpItem.downlinkCapacity;
                        }

                        kcpSettings.congestion = config.kcpItem.congestion;
                        kcpSettings.readBufferSize = config.kcpItem.readBufferSize;
                        kcpSettings.writeBufferSize = config.kcpItem.writeBufferSize;
                        kcpSettings.header = new Header
                        {
                            type = config.headerType()
                        };
                        if (!Utils.IsNullOrEmpty(config.path()))
                        {
                            kcpSettings.seed = config.path();
                        }
                        streamSettings.kcpSettings = kcpSettings;
                        break;
                    //ws
                    case "ws":
                        WsSettings wsSettings = new WsSettings
                        {
                        };

                        string path = config.path();
                        if (!string.IsNullOrWhiteSpace(host))
                        {
                            wsSettings.headers = new Headers
                            {
                                Host = host
                            };
                        }
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            wsSettings.path = path;
                        }
                        streamSettings.wsSettings = wsSettings;

                        //TlsSettings tlsSettings = new TlsSettings();
                        //tlsSettings.allowInsecure = config.allowInsecure();
                        //if (!string.IsNullOrWhiteSpace(host))
                        //{
                        //    tlsSettings.serverName = host;
                        //}
                        //streamSettings.tlsSettings = tlsSettings;
                        break;
                    //h2
                    case "h2":
                        HttpSettings httpSettings = new HttpSettings();

                        if (!string.IsNullOrWhiteSpace(host))
                        {
                            httpSettings.host = Utils.String2List(host);
                        }
                        httpSettings.path = config.path();

                        streamSettings.httpSettings = httpSettings;

                        //TlsSettings tlsSettings2 = new TlsSettings();
                        //tlsSettings2.allowInsecure = config.allowInsecure();
                        //streamSettings.tlsSettings = tlsSettings2;
                        break;
                    //quic
                    case "quic":
                        QuicSettings quicsettings = new QuicSettings
                        {
                            security = host,
                            key = config.path(),
                            header = new Header
                            {
                                type = config.headerType()
                            }
                        };
                        streamSettings.quicSettings = quicsettings;
                        if (config.streamSecurity() == Global.StreamSecurity)
                        {
                            if (!string.IsNullOrWhiteSpace(sni))
                            {
                                streamSettings.tlsSettings.serverName = sni;
                            }
                            else
                            {
                                streamSettings.tlsSettings.serverName = config.address();
                            }
                        }
                        break;
                    case "grpc":
                        var grpcSettings = new GrpcSettings();

                        grpcSettings.serviceName = config.path();
                        grpcSettings.multiMode = (config.headerType() == Global.GrpcmultiMode ? true : false);
                        streamSettings.grpcSettings = grpcSettings;
                        break;
                    default:
                        //tcp带http伪装
                        if (config.headerType().Equals(Global.TcpHeaderHttp))
                        {
                            TcpSettings tcpSettings = new TcpSettings
                            {
                                header = new Header
                                {
                                    type = config.headerType()
                                }
                            };

                            if (iobound.Equals("out"))
                            {
                                //request填入自定义Host
                                string request = Utils.GetEmbedText(Global.v2raySampleHttprequestFileName);
                                string[] arrHost = host.Split(',');
                                string host2 = string.Join("\",\"", arrHost);
                                request = request.Replace("$requestHost$", string.Format("\"{0}\"", host2));
                                //request = request.Replace("$requestHost$", string.Format("\"{0}\"", config.requestHost()));

                                //填入自定义Path
                                string pathHttp = @"/";
                                if (!Utils.IsNullOrEmpty(config.path()))
                                {
                                    string[] arrPath = config.path().Split(',');
                                    pathHttp = string.Join("\",\"", arrPath);
                                }
                                request = request.Replace("$requestPath$", string.Format("\"{0}\"", pathHttp));
                                tcpSettings.header.request = Utils.FromJson<object>(request);
                            }
                            else if (iobound.Equals("in"))
                            {
                                //string response = Utils.GetEmbedText(Global.v2raySampleHttpresponseFileName);
                                //tcpSettings.header.response = Utils.FromJson<object>(response);
                            }

                            streamSettings.tcpSettings = tcpSettings;
                        }
                        break;
                }
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// remoteDNS
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int dns(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.remoteDNS))
                {
                    return 0;
                }

                var obj = Utils.ParseJson(config.remoteDNS);
                if (obj != null && obj.ContainsKey("servers"))
                {
                    v2rayConfig.dns = obj;
                }
                else
                {
                    List<string> servers = new List<string>();

                    string[] arrDNS = config.remoteDNS.Split(',');
                    foreach (string str in arrDNS)
                    {
                        //if (Utils.IsIP(str))
                        //{
                        servers.Add(str);
                        //}
                    }
                    //servers.Add("localhost");
                    v2rayConfig.dns = new Mode.Dns
                    {
                        servers = servers
                    };
                }
            }
            catch
            {
            }
            return 0;
        }

        public static int statistic(Config config, ref V2rayConfig v2rayConfig)
        {
            if (config.enableStatistics)
            {
                string tag = Global.InboundAPITagName;
                API apiObj = new API();
                Policy policyObj = new Policy();
                SystemPolicy policySystemSetting = new SystemPolicy();

                string[] services = { "StatsService" };

                v2rayConfig.stats = new Stats();

                apiObj.tag = tag;
                apiObj.services = services.ToList();
                v2rayConfig.api = apiObj;

                policySystemSetting.statsOutboundDownlink = true;
                policySystemSetting.statsOutboundUplink = true;
                policyObj.system = policySystemSetting;
                v2rayConfig.policy = policyObj;

                if (!v2rayConfig.inbounds.Exists(item => { return item.tag == tag; }))
                {
                    Inbounds apiInbound = new Inbounds();
                    Inboundsettings apiInboundSettings = new Inboundsettings();
                    apiInbound.tag = tag;
                    apiInbound.listen = Global.Loopback;
                    apiInbound.port = Global.statePort;
                    apiInbound.protocol = Global.InboundAPIProtocal;
                    apiInboundSettings.address = Global.Loopback;
                    apiInbound.settings = apiInboundSettings;
                    v2rayConfig.inbounds.Add(apiInbound);
                }

                if (!v2rayConfig.routing.rules.Exists(item => { return item.outboundTag == tag; }))
                {
                    RulesItem apiRoutingRule = new RulesItem
                    {
                        inboundTag = new List<string> { tag },
                        outboundTag = tag,
                        type = "field"
                    };
                    v2rayConfig.routing.rules.Add(apiRoutingRule);
                }
            }
            return 0;
        }

        /// <summary>
        /// 生成v2ray的客户端配置文件(自定义配置)
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static int GenerateClientCustomConfig(Config config, string fileName, out string msg)
        {
            try
            {
                //检查GUI设置
                if (config == null
                    || config.index < 0
                    || config.vmess.Count <= 0
                    || config.index > config.vmess.Count - 1
                    )
                {
                    msg = UIRes.I18N("CheckServerSettings");
                    return -1;
                }

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                string addressFileName = config.address();
                if (!File.Exists(addressFileName))
                {
                    addressFileName = Path.Combine(Utils.GetTempPath(), addressFileName);
                }
                if (!File.Exists(addressFileName))
                {
                    msg = UIRes.I18N("FailedGenDefaultConfiguration");
                    return -1;
                }
                File.Copy(addressFileName, fileName);

                msg = string.Format(UIRes.I18N("SuccessfulConfiguration"), config.getSummary());
            }
            catch
            {
                msg = UIRes.I18N("FailedGenDefaultConfiguration");
                return -1;
            }
            return 0;
        }

        #endregion

        #region 生成服务端端配置

        /// <summary>
        /// 生成v2ray的客户端配置文件
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static int GenerateServerConfig(Config config, string fileName, out string msg)
        {
            try
            {
                //检查GUI设置
                if (config == null
                    || config.index < 0
                    || config.vmess.Count <= 0
                    || config.index > config.vmess.Count - 1
                    )
                {
                    msg = UIRes.I18N("CheckServerSettings");
                    return -1;
                }

                msg = UIRes.I18N("InitialConfiguration");

                //取得默认配置
                string result = Utils.GetEmbedText(SampleServer);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedGetDefaultConfiguration");
                    return -1;
                }

                //转成Json
                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = UIRes.I18N("FailedGenDefaultConfiguration");
                    return -1;
                }

                ////开始修改配置
                log(config, ref v2rayConfig, true);

                //vmess协议服务器配置
                ServerInbound(config, ref v2rayConfig);

                //传出设置
                ServerOutbound(config, ref v2rayConfig);

                Utils.ToJsonFile(v2rayConfig, fileName, false);

                msg = string.Format(UIRes.I18N("SuccessfulConfiguration"), config.getSummary());
            }
            catch
            {
                msg = UIRes.I18N("FailedGenDefaultConfiguration");
                return -1;
            }
            return 0;
        }

        /// <summary>
        /// vmess协议服务器配置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int ServerInbound(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                Inbounds inbound = v2rayConfig.inbounds[0];
                UsersItem usersItem;
                if (inbound.settings.clients.Count <= 0)
                {
                    usersItem = new UsersItem();
                    inbound.settings.clients.Add(usersItem);
                }
                else
                {
                    usersItem = inbound.settings.clients[0];
                }
                //远程服务器端口
                inbound.port = config.port();

                //远程服务器用户ID
                usersItem.id = config.id();
                usersItem.email = Global.userEMail;

                if (config.configType() == (int)EConfigType.Vmess)
                {
                    inbound.protocol = Global.vmessProtocolLite;
                    usersItem.alterId = config.alterId();

                }
                else if (config.configType() == (int)EConfigType.VLESS)
                {
                    inbound.protocol = Global.vlessProtocolLite;
                    usersItem.flow = config.flow();
                    inbound.settings.decryption = config.security();
                }

                //远程服务器底层传输配置
                StreamSettings streamSettings = inbound.streamSettings;
                boundStreamSettings(config, "in", ref streamSettings);
            }
            catch
            {
            }
            return 0;
        }

        /// <summary>
        /// 传出设置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="v2rayConfig"></param>
        /// <returns></returns>
        private static int ServerOutbound(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.outbounds[0] != null)
                {
                    v2rayConfig.outbounds[0].settings = null;
                }
            }
            catch
            {
            }
            return 0;
        }
        #endregion

        #region 导入(导出)客户端/服务端配置

        /// <summary>
        /// 导入v2ray客户端配置
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static VmessItem ImportFromClientConfig(string fileName, out string msg)
        {
            msg = string.Empty;
            VmessItem vmessItem = new VmessItem();

            try
            {
                //载入配置文件 
                string result = Utils.LoadResource(fileName);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedReadConfiguration");
                    return null;
                }

                //转成Json
                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = UIRes.I18N("FailedConversionConfiguration");
                    return null;
                }

                if (v2rayConfig.outbounds == null
                 || v2rayConfig.outbounds.Count <= 0)
                {
                    msg = UIRes.I18N("IncorrectClientConfiguration");
                    return null;
                }

                Outbounds outbound = v2rayConfig.outbounds[0];
                if (outbound == null
                    || Utils.IsNullOrEmpty(outbound.protocol)
                    || outbound.protocol != Global.vmessProtocolLite
                    || outbound.settings == null
                    || outbound.settings.vnext == null
                    || outbound.settings.vnext.Count <= 0
                    || outbound.settings.vnext[0].users == null
                    || outbound.settings.vnext[0].users.Count <= 0)
                {
                    msg = UIRes.I18N("IncorrectClientConfiguration");
                    return null;
                }

                vmessItem.security = Global.DefaultSecurity;
                vmessItem.network = Global.DefaultNetwork;
                vmessItem.headerType = Global.None;
                vmessItem.address = outbound.settings.vnext[0].address;
                vmessItem.port = outbound.settings.vnext[0].port;
                vmessItem.id = outbound.settings.vnext[0].users[0].id;
                vmessItem.alterId = outbound.settings.vnext[0].users[0].alterId;
                vmessItem.remarks = string.Format("import@{0}", DateTime.Now.ToShortDateString());

                //tcp or kcp
                if (outbound.streamSettings != null
                    && outbound.streamSettings.network != null
                    && !Utils.IsNullOrEmpty(outbound.streamSettings.network))
                {
                    vmessItem.network = outbound.streamSettings.network;
                }

                //tcp伪装http
                if (outbound.streamSettings != null
                    && outbound.streamSettings.tcpSettings != null
                    && outbound.streamSettings.tcpSettings.header != null
                    && !Utils.IsNullOrEmpty(outbound.streamSettings.tcpSettings.header.type))
                {
                    if (outbound.streamSettings.tcpSettings.header.type.Equals(Global.TcpHeaderHttp))
                    {
                        vmessItem.headerType = outbound.streamSettings.tcpSettings.header.type;
                        string request = Convert.ToString(outbound.streamSettings.tcpSettings.header.request);
                        if (!Utils.IsNullOrEmpty(request))
                        {
                            V2rayTcpRequest v2rayTcpRequest = Utils.FromJson<V2rayTcpRequest>(request);
                            if (v2rayTcpRequest != null
                                && v2rayTcpRequest.headers != null
                                && v2rayTcpRequest.headers.Host != null
                                && v2rayTcpRequest.headers.Host.Count > 0)
                            {
                                vmessItem.requestHost = v2rayTcpRequest.headers.Host[0];
                            }
                        }
                    }
                }
                //kcp伪装
                if (outbound.streamSettings != null
                    && outbound.streamSettings.kcpSettings != null
                    && outbound.streamSettings.kcpSettings.header != null
                    && !Utils.IsNullOrEmpty(outbound.streamSettings.kcpSettings.header.type))
                {
                    vmessItem.headerType = outbound.streamSettings.kcpSettings.header.type;
                }

                //ws
                if (outbound.streamSettings != null
                    && outbound.streamSettings.wsSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(outbound.streamSettings.wsSettings.path))
                    {
                        vmessItem.path = outbound.streamSettings.wsSettings.path;
                    }
                    if (outbound.streamSettings.wsSettings.headers != null
                      && !Utils.IsNullOrEmpty(outbound.streamSettings.wsSettings.headers.Host))
                    {
                        vmessItem.requestHost = outbound.streamSettings.wsSettings.headers.Host;
                    }
                }

                //h2
                if (outbound.streamSettings != null
                    && outbound.streamSettings.httpSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(outbound.streamSettings.httpSettings.path))
                    {
                        vmessItem.path = outbound.streamSettings.httpSettings.path;
                    }
                    if (outbound.streamSettings.httpSettings.host != null
                        && outbound.streamSettings.httpSettings.host.Count > 0)
                    {
                        vmessItem.requestHost = Utils.List2String(outbound.streamSettings.httpSettings.host);
                    }
                }

                //tls
                if (outbound.streamSettings != null
                    && outbound.streamSettings.security != null
                    && outbound.streamSettings.security == Global.StreamSecurity)
                {
                    vmessItem.streamSecurity = Global.StreamSecurity;
                }
            }
            catch
            {
                msg = UIRes.I18N("IncorrectClientConfiguration");
                return null;
            }

            return vmessItem;
        }

        /// <summary>
        /// 导入v2ray服务端配置
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static VmessItem ImportFromServerConfig(string fileName, out string msg)
        {
            msg = string.Empty;
            VmessItem vmessItem = new VmessItem();

            try
            {
                //载入配置文件 
                string result = Utils.LoadResource(fileName);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedReadConfiguration");
                    return null;
                }

                //转成Json
                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = UIRes.I18N("FailedConversionConfiguration");
                    return null;
                }

                if (v2rayConfig.inbounds == null
                 || v2rayConfig.inbounds.Count <= 0)
                {
                    msg = UIRes.I18N("IncorrectServerConfiguration");
                    return null;
                }

                Inbounds inbound = v2rayConfig.inbounds[0];
                if (inbound == null
                    || Utils.IsNullOrEmpty(inbound.protocol)
                    || inbound.protocol != Global.vmessProtocolLite
                    || inbound.settings == null
                    || inbound.settings.clients == null
                    || inbound.settings.clients.Count <= 0)
                {
                    msg = UIRes.I18N("IncorrectServerConfiguration");
                    return null;
                }

                vmessItem.security = Global.DefaultSecurity;
                vmessItem.network = Global.DefaultNetwork;
                vmessItem.headerType = Global.None;
                vmessItem.address = string.Empty;
                vmessItem.port = inbound.port;
                vmessItem.id = inbound.settings.clients[0].id;
                vmessItem.alterId = inbound.settings.clients[0].alterId;

                vmessItem.remarks = string.Format("import@{0}", DateTime.Now.ToShortDateString());

                //tcp or kcp
                if (inbound.streamSettings != null
                    && inbound.streamSettings.network != null
                    && !Utils.IsNullOrEmpty(inbound.streamSettings.network))
                {
                    vmessItem.network = inbound.streamSettings.network;
                }

                //tcp伪装http
                if (inbound.streamSettings != null
                    && inbound.streamSettings.tcpSettings != null
                    && inbound.streamSettings.tcpSettings.header != null
                    && !Utils.IsNullOrEmpty(inbound.streamSettings.tcpSettings.header.type))
                {
                    if (inbound.streamSettings.tcpSettings.header.type.Equals(Global.TcpHeaderHttp))
                    {
                        vmessItem.headerType = inbound.streamSettings.tcpSettings.header.type;
                        string request = Convert.ToString(inbound.streamSettings.tcpSettings.header.request);
                        if (!Utils.IsNullOrEmpty(request))
                        {
                            V2rayTcpRequest v2rayTcpRequest = Utils.FromJson<V2rayTcpRequest>(request);
                            if (v2rayTcpRequest != null
                                && v2rayTcpRequest.headers != null
                                && v2rayTcpRequest.headers.Host != null
                                && v2rayTcpRequest.headers.Host.Count > 0)
                            {
                                vmessItem.requestHost = v2rayTcpRequest.headers.Host[0];
                            }
                        }
                    }
                }
                //kcp伪装
                //if (v2rayConfig.outbound.streamSettings != null
                //    && v2rayConfig.outbound.streamSettings.kcpSettings != null
                //    && v2rayConfig.outbound.streamSettings.kcpSettings.header != null
                //    && !Utils.IsNullOrEmpty(v2rayConfig.outbound.streamSettings.kcpSettings.header.type))
                //{
                //    cmbHeaderType.Text = v2rayConfig.outbound.streamSettings.kcpSettings.header.type;
                //}

                //ws
                if (inbound.streamSettings != null
                    && inbound.streamSettings.wsSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(inbound.streamSettings.wsSettings.path))
                    {
                        vmessItem.path = inbound.streamSettings.wsSettings.path;
                    }
                    if (inbound.streamSettings.wsSettings.headers != null
                      && !Utils.IsNullOrEmpty(inbound.streamSettings.wsSettings.headers.Host))
                    {
                        vmessItem.requestHost = inbound.streamSettings.wsSettings.headers.Host;
                    }
                }

                //h2
                if (inbound.streamSettings != null
                    && inbound.streamSettings.httpSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(inbound.streamSettings.httpSettings.path))
                    {
                        vmessItem.path = inbound.streamSettings.httpSettings.path;
                    }
                    if (inbound.streamSettings.httpSettings.host != null
                        && inbound.streamSettings.httpSettings.host.Count > 0)
                    {
                        vmessItem.requestHost = Utils.List2String(inbound.streamSettings.httpSettings.host);
                    }
                }

                //tls
                if (inbound.streamSettings != null
                    && inbound.streamSettings.security != null
                    && inbound.streamSettings.security == Global.StreamSecurity)
                {
                    vmessItem.streamSecurity = Global.StreamSecurity;
                }
            }
            catch
            {
                msg = UIRes.I18N("IncorrectClientConfiguration");
                return null;
            }
            return vmessItem;
        }

        /// <summary>
        /// 导出为客户端配置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static int Export2ClientConfig(Config config, string fileName, out string msg)
        {
            return GenerateClientConfig(config, fileName, true, out msg);
        }

        /// <summary>
        /// 导出为服务端配置
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static int Export2ServerConfig(Config config, string fileName, out string msg)
        {
            return GenerateServerConfig(config, fileName, out msg);
        }

        #endregion

        #region Gen speedtest config


        public static string GenerateClientSpeedtestConfigString(Config config, List<ServerTestItem> selecteds, out string msg)
        {
            try
            {
                if (config == null
                    || config.index < 0
                    || config.vmess.Count <= 0
                    || config.index > config.vmess.Count - 1
                    )
                {
                    msg = UIRes.I18N("CheckServerSettings");
                    return "";
                }

                msg = UIRes.I18N("InitialConfiguration");

                Config configCopy = Utils.DeepCopy(config);

                string result = Utils.GetEmbedText(SampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedGetDefaultConfiguration");
                    return "";
                }

                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = UIRes.I18N("FailedGenDefaultConfiguration");
                    return "";
                }
                List<IPEndPoint> lstIpEndPoints = null;
                try
                {
                    lstIpEndPoints = new List<IPEndPoint>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());
                }
                catch { }

                log(configCopy, ref v2rayConfig, false);
                //routing(config, ref v2rayConfig);
                dns(configCopy, ref v2rayConfig);

                v2rayConfig.inbounds.Clear(); // Remove "proxy" service for speedtest, avoiding port conflicts.

                int httpPort = configCopy.GetLocalPort("speedtest");

                foreach (var it in selecteds)
                {
                    if (it.configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    if (it.port <= 0)
                    {
                        continue;
                    }

                    //find unuse port
                    var port = httpPort;
                    for (int k = httpPort; k < 65536; k++)
                    {
                        if (lstIpEndPoints != null && lstIpEndPoints.FindIndex(_it => _it.Port == k) >= 0)
                        {
                            continue;
                        }
                        //found
                        port = k;
                        httpPort = port + 1;
                        break;
                    }

                    //Port In Used
                    if (lstIpEndPoints != null && lstIpEndPoints.FindIndex(_it => _it.Port == port) >= 0)
                    {
                        continue;
                    }
                    configCopy.index = it.selected;
                    it.port = port;

                    Inbounds inbound = new Inbounds
                    {
                        listen = Global.Loopback,
                        port = port,
                        protocol = Global.InboundHttp
                    };
                    inbound.tag = Global.InboundHttp + inbound.port.ToString();
                    v2rayConfig.inbounds.Add(inbound);


                    V2rayConfig v2rayConfigCopy = Utils.FromJson<V2rayConfig>(result);
                    outbound(configCopy, ref v2rayConfigCopy);
                    v2rayConfigCopy.outbounds[0].tag = Global.agentTag + inbound.port.ToString();
                    v2rayConfig.outbounds.Add(v2rayConfigCopy.outbounds[0]);

                    RulesItem rule = new RulesItem
                    {
                        inboundTag = new List<string> { inbound.tag },
                        outboundTag = v2rayConfigCopy.outbounds[0].tag,
                        type = "field"
                    };
                    v2rayConfig.routing.rules.Add(rule);
                }

                msg = string.Format(UIRes.I18N("SuccessfulConfiguration"), configCopy.getSummary());
                return Utils.ToJson(v2rayConfig);
            }
            catch
            {
                msg = UIRes.I18N("FailedGenDefaultConfiguration");
                return "";
            }
        }

        #endregion

    }
}
