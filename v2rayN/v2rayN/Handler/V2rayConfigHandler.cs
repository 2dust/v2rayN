using System;
using System.Collections.Generic;
using System.IO;
using v2rayN.Mode;
using System.Net;
using System.Text;
using System.Linq;

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
            msg = string.Empty;

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

                Utils.ToJsonFile(v2rayConfig, fileName);

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
                var inbound = v2rayConfig.inbounds[0];
                //端口
                inbound.port = config.inbound[0].localPort;
                inbound.protocol = config.inbound[0].protocol;
                if (config.allowLANConn)
                {
                    inbound.listen = "0.0.0.0";
                }
                else
                {
                    inbound.listen = "127.0.0.1";
                }
                //开启udp
                inbound.settings.udp = config.inbound[0].udpEnabled;
                inbound.sniffing.enabled = config.inbound[0].sniffingEnabled;
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

                    //自定义
                    //需代理
                    routingUserRule(config.useragent, Global.agentTag, ref v2rayConfig);
                    //直连
                    routingUserRule(config.userdirect, Global.directTag, ref v2rayConfig);
                    //阻止
                    routingUserRule(config.userblock, Global.blockTag, ref v2rayConfig);


                    switch (config.routingMode)
                    {
                        case "0":
                            break;
                        case "1":
                            routingGeo("ip", "private", Global.directTag, ref v2rayConfig);
                            break;
                        case "2":
                            routingGeo("", "cn", Global.directTag, ref v2rayConfig);
                            break;
                        case "3":
                            routingGeo("ip", "private", Global.directTag, ref v2rayConfig);
                            routingGeo("", "cn", Global.directTag, ref v2rayConfig);
                            break;
                    }

                }
            }
            catch
            {
            }
            return 0;
        }
        private static int routingUserRule(List<string> userRule, string tag, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (userRule != null
                    && userRule.Count > 0)
                {
                    //Domain
                    RulesItem rulesDomain = new RulesItem();
                    rulesDomain.type = "field";
                    rulesDomain.outboundTag = tag;
                    rulesDomain.domain = new List<string>();

                    //IP
                    RulesItem rulesIP = new RulesItem();
                    rulesIP.type = "field";
                    rulesIP.outboundTag = tag;
                    rulesIP.ip = new List<string>();

                    for (int k = 0; k < userRule.Count; k++)
                    {
                        string url = userRule[k].TrimEx();
                        if (Utils.IsNullOrEmpty(url))
                        {
                            continue;
                        }
                        if (Utils.IsIP(url) || url.StartsWith("geoip:"))
                        {
                            rulesIP.ip.Add(url);
                        }
                        else if (Utils.IsDomain(url)
                            || url.StartsWith("geosite:")
                            || url.StartsWith("regexp:")
                            || url.StartsWith("domain:")
                            || url.StartsWith("full:"))
                        {
                            rulesDomain.domain.Add(url);
                        }
                    }
                    if (rulesDomain.domain.Count > 0)
                    {
                        v2rayConfig.routing.rules.Add(rulesDomain);
                    }
                    if (rulesIP.ip.Count > 0)
                    {
                        v2rayConfig.routing.rules.Add(rulesIP);
                    }
                }
            }
            catch
            {
            }
            return 0;
        }


        private static int routingGeo(string ipOrDomain, string code, string tag, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (!Utils.IsNullOrEmpty(code))
                {
                    //IP
                    if (ipOrDomain == "ip" || ipOrDomain == "")
                    {
                        RulesItem rulesItem = new RulesItem();
                        rulesItem.type = "field";
                        rulesItem.outboundTag = Global.directTag;
                        rulesItem.ip = new List<string>();
                        rulesItem.ip.Add($"geoip:{code}");

                        v2rayConfig.routing.rules.Add(rulesItem);
                    }

                    if (ipOrDomain == "domain" || ipOrDomain == "")
                    {
                        RulesItem rulesItem = new RulesItem();
                        rulesItem.type = "field";
                        rulesItem.outboundTag = Global.directTag;
                        rulesItem.domain = new List<string>();
                        rulesItem.domain.Add($"geosite:{code}");
                        v2rayConfig.routing.rules.Add(rulesItem);
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
                var outbound = v2rayConfig.outbounds[0];
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
                    usersItem.security = config.security();

                    //Mux
                    outbound.mux.enabled = config.muxEnabled;

                    //远程服务器底层传输配置
                    StreamSettings streamSettings = outbound.streamSettings;
                    boundStreamSettings(config, "out", ref streamSettings);

                    outbound.protocol = "vmess";
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
                    serversItem.method = config.security();

                    serversItem.ota = false;
                    serversItem.level = 1;

                    outbound.mux.enabled = false;

                    outbound.protocol = "shadowsocks";
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

                    if (!Utils.IsNullOrEmpty(config.security())
                        && !Utils.IsNullOrEmpty(config.id()))
                    {
                        var socksUsersItem = new SocksUsersItem();
                        socksUsersItem.user = config.security();
                        socksUsersItem.pass = config.id();
                        socksUsersItem.level = 1;

                        serversItem.users = new List<SocksUsersItem>() { socksUsersItem };
                    }

                    outbound.mux.enabled = false;

                    outbound.protocol = "socks";
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
                var host = config.requestHost();

                //if tls
                if (config.streamSecurity() == "tls")
                {
                    streamSettings.security = config.streamSecurity();

                    TlsSettings tlsSettings = new TlsSettings();
                    tlsSettings.allowInsecure = config.allowInsecure();
                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        tlsSettings.serverName = host;
                    }
                    streamSettings.tlsSettings = tlsSettings;
                }

                //streamSettings
                switch (config.network())
                {
                    //kcp基本配置暂时是默认值，用户能自己设置伪装类型
                    case "kcp":
                        KcpSettings kcpSettings = new KcpSettings();
                        kcpSettings.mtu = config.kcpItem.mtu;
                        kcpSettings.tti = config.kcpItem.tti;
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
                        kcpSettings.header = new Header();
                        kcpSettings.header.type = config.headerType();
                        streamSettings.kcpSettings = kcpSettings;
                        break;
                    //ws
                    case "ws":
                        WsSettings wsSettings = new WsSettings();
                        wsSettings.connectionReuse = true;

                        string path = config.path();
                        if (!string.IsNullOrWhiteSpace(host))
                        {
                            wsSettings.headers = new Headers();
                            wsSettings.headers.Host = host;
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
                        QuicSettings quicsettings = new QuicSettings();
                        quicsettings.security = host;
                        quicsettings.key = config.path();
                        quicsettings.header = new Header();
                        quicsettings.header.type = config.headerType();

                        streamSettings.quicSettings = quicsettings;
                        break;
                    default:
                        //tcp带http伪装
                        if (config.headerType().Equals(Global.TcpHeaderHttp))
                        {
                            TcpSettings tcpSettings = new TcpSettings();
                            tcpSettings.connectionReuse = true;
                            tcpSettings.header = new Header();
                            tcpSettings.header.type = config.headerType();

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
                v2rayConfig.dns = new Mode.Dns();
                v2rayConfig.dns.servers = servers;
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
                var tag = Global.InboundAPITagName;
                var apiObj = new Mode.API();
                var policyObj = new Mode.Policy();
                var policySystemSetting = new Mode.SystemPolicy();

                string[] services = { "StatsService" };

                v2rayConfig.stats = new Stats();

                apiObj.tag = tag;
                apiObj.services =  services.ToList();
                v2rayConfig.api = apiObj;

                policySystemSetting.statsInboundDownlink = true;
                policySystemSetting.statsInboundUplink = true;
                policyObj.system = policySystemSetting;
                v2rayConfig.policy = policyObj;
                if(!v2rayConfig.inbounds.Exists(item => { return item.tag == tag; }))
                {
                    var apiInbound = new Mode.Inbounds();
                    var apiInboundSettings = new Mode.Inboundsettings();
                    apiInbound.tag = tag;
                    apiInbound.listen = Global.Loopback;
                    apiInbound.port = config.port();
                    apiInbound.protocol = Global.InboundAPIProtocal;
                    apiInboundSettings.address = Global.Loopback;
                    apiInbound.settings = apiInboundSettings;
                    v2rayConfig.inbounds.Add(apiInbound);
                }
                if(!v2rayConfig.routing.rules.Exists(item => { return item.outboundTag == tag; }))
                {
                    var apiRoutingRule = new Mode.RulesItem();
                    apiRoutingRule.inboundTag = tag;
                    apiRoutingRule.outboundTag = tag;
                    apiRoutingRule.type = "field";
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
            msg = string.Empty;

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

                string addressFileName = config.address();
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
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
            msg = string.Empty;

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

                Utils.ToJsonFile(v2rayConfig, fileName);

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
                var inbound = v2rayConfig.inbounds[0];
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
                usersItem.alterId = config.alterId();
                usersItem.email = Global.userEMail;

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

                var outbound = v2rayConfig.outbounds[0];
                if (outbound == null
                    || Utils.IsNullOrEmpty(outbound.protocol)
                    || outbound.protocol != "vmess"
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

                var inbound = v2rayConfig.inbounds[0];
                if (inbound == null
                    || Utils.IsNullOrEmpty(inbound.protocol)
                    || inbound.protocol != "vmess"
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
            }
            catch
            {
                msg = UIRes.I18N("IncorrectClientConfiguration");
                return null;
            }
            return vmessItem;
        }

        /// <summary>
        /// 从剪贴板导入URL
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static VmessItem ImportFromClipboardConfig(string clipboardData, out string msg)
        {
            msg = string.Empty;
            VmessItem vmessItem = new VmessItem();

            try
            {
                //载入配置文件 
                string result = clipboardData.TrimEx();// Utils.GetClipboardData();
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedReadConfiguration");
                    return null;
                }

                if (result.StartsWith(Global.vmessProtocol))
                {
                    int indexSplit = result.IndexOf("?");
                    if (indexSplit > 0)
                    {
                        vmessItem = ResolveVmess4Kitsunebi(result);
                    }
                    else
                    {
                        vmessItem.configType = (int)EConfigType.Vmess;
                        result = result.Substring(Global.vmessProtocol.Length);
                        result = Utils.Base64Decode(result);

                        //转成Json
                        VmessQRCode vmessQRCode = Utils.FromJson<VmessQRCode>(result);
                        if (vmessQRCode == null)
                        {
                            msg = UIRes.I18N("FailedConversionConfiguration");
                            return null;
                        }
                        vmessItem.security = Global.DefaultSecurity;
                        vmessItem.network = Global.DefaultNetwork;
                        vmessItem.headerType = Global.None;

                      
                        vmessItem.configVersion = Utils.ToInt(vmessQRCode.v);
                        vmessItem.remarks = Utils.ToString(vmessQRCode.ps);
                        vmessItem.address = Utils.ToString(vmessQRCode.add);
                        vmessItem.port = Utils.ToInt(vmessQRCode.port);
                        vmessItem.id = Utils.ToString(vmessQRCode.id);
                        vmessItem.alterId = Utils.ToInt(vmessQRCode.aid);
                    
                        if (!Utils.IsNullOrEmpty(vmessQRCode.net))
                        {
                            vmessItem.network = vmessQRCode.net;
                        }
                        if (!Utils.IsNullOrEmpty(vmessQRCode.type))
                        {
                            vmessItem.headerType = vmessQRCode.type;
                        }
                         
                        vmessItem.requestHost = Utils.ToString(vmessQRCode.host);
                        vmessItem.path = Utils.ToString(vmessQRCode.path);
                        vmessItem.streamSecurity = Utils.ToString(vmessQRCode.tls);
                    }

                    ConfigHandler.UpgradeServerVersion(ref vmessItem);
                }
                else if (result.StartsWith(Global.ssProtocol))
                {
                    msg = UIRes.I18N("ConfigurationFormatIncorrect");

                    vmessItem.configType = (int)EConfigType.Shadowsocks;
                    result = result.Substring(Global.ssProtocol.Length);
                    //remark
                    int indexRemark = result.IndexOf("#");
                    if (indexRemark > 0)
                    {
                        try
                        {
                            vmessItem.remarks = WebUtility.UrlDecode(result.Substring(indexRemark + 1, result.Length - indexRemark - 1));
                        }
                        catch { }
                        result = result.Substring(0, indexRemark);
                    }
                    //part decode
                    int indexS = result.IndexOf("@");
                    if (indexS > 0)
                    {
                        result = Utils.Base64Decode(result.Substring(0, indexS)) + result.Substring(indexS, result.Length - indexS);
                    }
                    else
                    {
                        result = Utils.Base64Decode(result);
                    }

                    string[] arr1 = result.Split('@');
                    if (arr1.Length != 2)
                    {
                        return null;
                    }
                    string[] arr21 = arr1[0].Split(':');
                    //string[] arr22 = arr1[1].Split(':');
                    int indexPort = arr1[1].LastIndexOf(":");
                    if (arr21.Length != 2 || indexPort < 0)
                    {
                        return null;
                    }
                    vmessItem.address = arr1[1].Substring(0, indexPort);
                    vmessItem.port = Utils.ToInt(arr1[1].Substring(indexPort + 1, arr1[1].Length - (indexPort + 1)));
                    vmessItem.security = arr21[0];
                    vmessItem.id = arr21[1];
                }
                else if (result.StartsWith(Global.socksProtocol))
                {
                    msg = UIRes.I18N("ConfigurationFormatIncorrect");

                    vmessItem.configType = (int)EConfigType.Socks;
                    result = result.Substring(Global.socksProtocol.Length);
                    //remark
                    int indexRemark = result.IndexOf("#");
                    if (indexRemark > 0)
                    {
                        try
                        {
                            vmessItem.remarks = WebUtility.UrlDecode(result.Substring(indexRemark + 1, result.Length - indexRemark - 1));
                        }
                        catch { }
                        result = result.Substring(0, indexRemark);
                    }
                    //part decode
                    int indexS = result.IndexOf("@");
                    if (indexS > 0)
                    {
                    }
                    else
                    {
                        result = Utils.Base64Decode(result);
                    }

                    string[] arr1 = result.Split('@');
                    if (arr1.Length != 2)
                    {
                        return null;
                    }
                    string[] arr21 = arr1[0].Split(':');
                    //string[] arr22 = arr1[1].Split(':');
                    int indexPort = arr1[1].LastIndexOf(":");
                    if (arr21.Length != 2 || indexPort < 0)
                    {
                        return null;
                    }
                    vmessItem.address = arr1[1].Substring(0, indexPort);
                    vmessItem.port = Utils.ToInt(arr1[1].Substring(indexPort + 1, arr1[1].Length - (indexPort + 1)));
                    vmessItem.security = arr21[0];
                    vmessItem.id = arr21[1];
                }
                else
                {
                    msg = UIRes.I18N("NonvmessOrssProtocol");
                    return null;
                }
            }
            catch
            {
                msg = UIRes.I18N("Incorrectconfiguration");
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
            msg = string.Empty;
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
            msg = string.Empty;
            return GenerateServerConfig(config, fileName, out msg);
        }

        private static VmessItem ResolveVmess4Kitsunebi(string result)
        {
            VmessItem vmessItem = new VmessItem();

            vmessItem.configType = (int)EConfigType.Vmess;
            result = result.Substring(Global.vmessProtocol.Length);
            int indexSplit = result.IndexOf("?");
            if (indexSplit > 0)
            {
                result = result.Substring(0, indexSplit);
            }
            result = Utils.Base64Decode(result);

            string[] arr1 = result.Split('@');
            if (arr1.Length != 2)
            {
                return null;
            }
            string[] arr21 = arr1[0].Split(':');
            string[] arr22 = arr1[1].Split(':');
            if (arr21.Length != 2 || arr21.Length != 2)
            {
                return null;
            }

            vmessItem.address = arr22[0];
            vmessItem.port = Utils.ToInt(arr22[1]);
            vmessItem.security = arr21[0];
            vmessItem.id = arr21[1];

            vmessItem.network = Global.DefaultNetwork;
            vmessItem.headerType = Global.None;
            vmessItem.remarks = "Alien";
            vmessItem.alterId = 0;

            return vmessItem;
        }

        #endregion

    }
}
