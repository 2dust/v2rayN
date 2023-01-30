using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    /// <summary>
    /// Core configuration file processing class
    /// </summary>
    class CoreConfigHandler
    {
        private static string SampleClient = Global.v2raySampleClient;
        private static string SampleServer = Global.v2raySampleServer;

        #region Generate client configuration

        /// <summary>
        /// Generate client configuration
        /// </summary>
        /// <param name="node"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int GenerateClientConfig(ProfileItem node, string fileName, out string msg, out string content)
        {
            content = string.Empty;
            try
            {
                if (node == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;
                if (node.configType == EConfigType.Custom)
                {
                    return GenerateClientCustomConfig(node, fileName, out msg);
                }
                else
                {
                    V2rayConfig v2rayConfig = null;
                    if (GenerateClientConfigContent(node, false, ref v2rayConfig, out msg) != 0)
                    {
                        return -1;
                    }
                    if (Utils.IsNullOrEmpty(fileName))
                    {
                        content = Utils.ToJson(v2rayConfig);
                    }
                    else
                    {
                        Utils.ToJsonFile(v2rayConfig, fileName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog("GenerateClientConfig", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

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
                        v2rayConfig.log.access = Utils.GetLogPath(v2rayConfig.log.access);
                        v2rayConfig.log.error = Utils.GetLogPath(v2rayConfig.log.error);
                    }
                    else
                    {
                        v2rayConfig.log.loglevel = config.loglevel;
                        v2rayConfig.log.access = "";
                        v2rayConfig.log.error = "";
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static int inbound(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                v2rayConfig.inbounds = new List<Inbounds>();

                Inbounds inbound = GetInbound(config.inbound[0], Global.InboundSocks, 0, true);
                v2rayConfig.inbounds.Add(inbound);

                //http
                Inbounds inbound2 = GetInbound(config.inbound[0], Global.InboundHttp, 1, false);
                v2rayConfig.inbounds.Add(inbound2);

                if (config.inbound[0].allowLANConn)
                {
                    if (config.inbound[0].newPort4LAN)
                    {
                        Inbounds inbound3 = GetInbound(config.inbound[0], Global.InboundSocks2, 2, true);
                        inbound3.listen = "0.0.0.0";
                        v2rayConfig.inbounds.Add(inbound3);

                        Inbounds inbound4 = GetInbound(config.inbound[0], Global.InboundHttp2, 3, false);
                        inbound4.listen = "0.0.0.0";
                        v2rayConfig.inbounds.Add(inbound4);

                        //auth
                        if (!Utils.IsNullOrEmpty(config.inbound[0].user) && !Utils.IsNullOrEmpty(config.inbound[0].pass))
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts = new List<AccountsItem> { new AccountsItem() { user = config.inbound[0].user, pass = config.inbound[0].pass } };

                            inbound4.settings.auth = "password";
                            inbound4.settings.accounts = new List<AccountsItem> { new AccountsItem() { user = config.inbound[0].user, pass = config.inbound[0].pass } };
                        }
                    }
                    else
                    {
                        inbound.listen = "0.0.0.0";
                        inbound2.listen = "0.0.0.0";
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static Inbounds GetInbound(InItem inItem, string tag, int offset, bool bSocks)
        {
            string result = Utils.GetEmbedText(Global.v2raySampleInbound);
            if (Utils.IsNullOrEmpty(result))
            {
                return null;
            }

            var inbound = Utils.FromJson<Inbounds>(result);
            if (inbound == null)
            {
                return null;
            }
            inbound.tag = tag;
            inbound.port = inItem.localPort + offset;
            inbound.protocol = bSocks ? Global.InboundSocks : Global.InboundHttp;
            inbound.settings.udp = inItem.udpEnabled;
            inbound.sniffing.enabled = inItem.sniffingEnabled;
            inbound.sniffing.routeOnly = inItem.routeOnly;

            return inbound;
        }

        private static int routing(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.routing != null
                  && v2rayConfig.routing.rules != null)
                {
                    v2rayConfig.routing.domainStrategy = config.domainStrategy;
                    v2rayConfig.routing.domainMatcher = Utils.IsNullOrEmpty(config.domainMatcher) ? null : config.domainMatcher;

                    if (config.enableRoutingAdvanced)
                    {
                        var routing = ConfigHandler.GetDefaultRouting(ref config);
                        if (routing != null)
                        {
                            if (!Utils.IsNullOrEmpty(routing.domainStrategy))
                            {
                                v2rayConfig.routing.domainStrategy = routing.domainStrategy;
                            }
                            var rules = Utils.FromJson<List<RulesItem>>(routing.ruleSet);
                            foreach (var item in rules)
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
                            var rules = Utils.FromJson<List<RulesItem>>(lockedItem.ruleSet);
                            foreach (var item in rules)
                            {
                                routingUserRule(item, ref v2rayConfig);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
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
                if (rules.inboundTag != null && rules.inboundTag.Count == 0)
                {
                    rules.inboundTag = null;
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
                    v2rayConfig.routing.rules.Add(it);
                    hasDomainIp = true;
                }
                if (rules.ip != null && rules.ip.Count > 0)
                {
                    var it = Utils.DeepCopy(rules);
                    it.domain = null;
                    it.type = "field";
                    v2rayConfig.routing.rules.Add(it);
                    hasDomainIp = true;
                }
                if (!hasDomainIp)
                {
                    if (!Utils.IsNullOrEmpty(rules.port)
                        || (rules.protocol != null && rules.protocol.Count > 0)
                        || (rules.inboundTag != null && rules.inboundTag.Count > 0)
                        )
                    {
                        var it = Utils.DeepCopy(rules);
                        it.type = "field";
                        v2rayConfig.routing.rules.Add(it);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static int outbound(ProfileItem node, ref V2rayConfig v2rayConfig)
        {
            try
            {
                var config = LazyConfig.Instance.GetConfig();
                Outbounds outbound = v2rayConfig.outbounds[0];
                if (node.configType == EConfigType.VMess)
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
                    vnextItem.address = node.address;
                    vnextItem.port = node.port;

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
                    usersItem.id = node.id;
                    usersItem.alterId = node.alterId;
                    usersItem.email = Global.userEMail;
                    if (Global.vmessSecuritys.Contains(node.security))
                    {
                        usersItem.security = node.security;
                    }
                    else
                    {
                        usersItem.security = Global.DefaultSecurity;
                    }

                    //Mux
                    outbound.mux.enabled = config.muxEnabled;
                    outbound.mux.concurrency = config.muxEnabled ? 8 : -1;

                    boundStreamSettings(node, "out", outbound.streamSettings);

                    outbound.protocol = Global.vmessProtocolLite;
                    outbound.settings.servers = null;
                }
                else if (node.configType == EConfigType.Shadowsocks)
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
                    serversItem.address = node.address;
                    serversItem.port = node.port;
                    serversItem.password = node.id;
                    serversItem.method = LazyConfig.Instance.GetShadowsocksSecuritys(node).Contains(node.security) ? node.security : "none";


                    serversItem.ota = false;
                    serversItem.level = 1;

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;

                    boundStreamSettings(node, "out", outbound.streamSettings);

                    outbound.protocol = Global.ssProtocolLite;
                    outbound.settings.vnext = null;
                }
                else if (node.configType == EConfigType.Socks)
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
                    serversItem.address = node.address;
                    serversItem.port = node.port;
                    serversItem.method = null;
                    serversItem.password = null;

                    if (!Utils.IsNullOrEmpty(node.security)
                        && !Utils.IsNullOrEmpty(node.id))
                    {
                        SocksUsersItem socksUsersItem = new SocksUsersItem
                        {
                            user = node.security,
                            pass = node.id,
                            level = 1
                        };

                        serversItem.users = new List<SocksUsersItem>() { socksUsersItem };
                    }

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;

                    outbound.protocol = Global.socksProtocolLite;
                    outbound.settings.vnext = null;
                }
                else if (node.configType == EConfigType.VLESS)
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
                    vnextItem.address = node.address;
                    vnextItem.port = node.port;

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
                    usersItem.id = node.id;
                    usersItem.flow = string.Empty;
                    usersItem.email = Global.userEMail;
                    usersItem.encryption = node.security;

                    //Mux
                    outbound.mux.enabled = config.muxEnabled;
                    outbound.mux.concurrency = config.muxEnabled ? 8 : -1;

                    boundStreamSettings(node, "out", outbound.streamSettings);

                    //if xtls
                    if (node.streamSecurity == Global.StreamSecurityX)
                    {
                        if (Utils.IsNullOrEmpty(node.flow))
                        {
                            usersItem.flow = Global.xtlsFlows[1];
                        }
                        else
                        {
                            usersItem.flow = node.flow.Replace("splice", "direct");
                        }

                        outbound.mux.enabled = false;
                        outbound.mux.concurrency = -1;
                    }
                    else if (node.streamSecurity == Global.StreamSecurity)
                    {
                        if (!Utils.IsNullOrEmpty(node.flow))
                        {
                            usersItem.flow = node.flow;

                            outbound.mux.enabled = false;
                            outbound.mux.concurrency = -1;
                        }
                    }

                    outbound.protocol = Global.vlessProtocolLite;
                    outbound.settings.servers = null;
                }
                else if (node.configType == EConfigType.Trojan)
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
                    serversItem.address = node.address;
                    serversItem.port = node.port;
                    serversItem.password = node.id;
                    serversItem.flow = string.Empty;

                    serversItem.ota = false;
                    serversItem.level = 1;

                    //if xtls
                    if (node.streamSecurity == Global.StreamSecurityX)
                    {
                        if (Utils.IsNullOrEmpty(node.flow))
                        {
                            serversItem.flow = Global.xtlsFlows[1];
                        }
                        else
                        {
                            serversItem.flow = node.flow.Replace("splice", "direct");
                        }

                        outbound.mux.enabled = false;
                        outbound.mux.concurrency = -1;
                    }

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;

                    boundStreamSettings(node, "out", outbound.streamSettings);

                    outbound.protocol = Global.trojanProtocolLite;
                    outbound.settings.vnext = null;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static int boundStreamSettings(ProfileItem node, string iobound, StreamSettings streamSettings)
        {
            try
            {
                var config = LazyConfig.Instance.GetConfig();

                streamSettings.network = node.GetNetwork();
                string host = node.requestHost.TrimEx();
                string sni = node.sni;

                //if tls
                if (node.streamSecurity == Global.StreamSecurity)
                {
                    streamSettings.security = node.streamSecurity;

                    TlsSettings tlsSettings = new TlsSettings
                    {
                        allowInsecure = Utils.ToBool(node.allowInsecure.IsNullOrEmpty() ? config.defAllowInsecure.ToString().ToLower() : node.allowInsecure),
                        alpn = node.GetAlpn(),
                        fingerprint = node.fingerprint
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
                if (node.streamSecurity == Global.StreamSecurityX)
                {
                    streamSettings.security = node.streamSecurity;

                    TlsSettings xtlsSettings = new TlsSettings
                    {
                        allowInsecure = Utils.ToBool(node.allowInsecure.IsNullOrEmpty() ? config.defAllowInsecure.ToString().ToLower() : node.allowInsecure),
                        alpn = node.GetAlpn(),
                        fingerprint = node.fingerprint
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
                switch (node.GetNetwork())
                {
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
                            type = node.headerType
                        };
                        if (!Utils.IsNullOrEmpty(node.path))
                        {
                            kcpSettings.seed = node.path;
                        }
                        streamSettings.kcpSettings = kcpSettings;
                        break;
                    //ws
                    case "ws":
                        WsSettings wsSettings = new WsSettings
                        {
                        };

                        string path = node.path;
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
                        httpSettings.path = node.path;

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
                            key = node.path,
                            header = new Header
                            {
                                type = node.headerType
                            }
                        };
                        streamSettings.quicSettings = quicsettings;
                        if (node.streamSecurity == Global.StreamSecurity)
                        {
                            if (!string.IsNullOrWhiteSpace(sni))
                            {
                                streamSettings.tlsSettings.serverName = sni;
                            }
                            else
                            {
                                streamSettings.tlsSettings.serverName = node.address;
                            }
                        }
                        break;
                    case "grpc":
                        var grpcSettings = new GrpcSettings
                        {
                            serviceName = node.path,
                            multiMode = (node.headerType == Global.GrpcmultiMode),
                            idle_timeout = config.grpcItem.idle_timeout,
                            health_check_timeout = config.grpcItem.health_check_timeout,
                            permit_without_stream = config.grpcItem.permit_without_stream,
                            initial_windows_size = config.grpcItem.initial_windows_size,
                        };

                        streamSettings.grpcSettings = grpcSettings;
                        break;
                    default:
                        //tcp
                        if (node.headerType.Equals(Global.TcpHeaderHttp))
                        {
                            TcpSettings tcpSettings = new TcpSettings
                            {
                                header = new Header
                                {
                                    type = node.headerType
                                }
                            };

                            if (iobound.Equals("out"))
                            {
                                //request Host
                                string request = Utils.GetEmbedText(Global.v2raySampleHttprequestFileName);
                                string[] arrHost = host.Split(',');
                                string host2 = string.Join("\",\"", arrHost);
                                request = request.Replace("$requestHost$", $"\"{host2}\"");
                                //request = request.Replace("$requestHost$", string.Format("\"{0}\"", config.requestHost()));

                                //Path
                                string pathHttp = @"/";
                                if (!Utils.IsNullOrEmpty(node.path))
                                {
                                    string[] arrPath = node.path.Split(',');
                                    pathHttp = string.Join("\",\"", arrPath);
                                }
                                request = request.Replace("$requestPath$", $"\"{pathHttp}\"");
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
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static int dns(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.remoteDNS))
                {
                    return 0;
                }

                //Outbound Freedom domainStrategy
                if (!string.IsNullOrWhiteSpace(config.domainStrategy4Freedom))
                {
                    var outbound = v2rayConfig.outbounds[1];
                    outbound.settings.domainStrategy = config.domainStrategy4Freedom;
                    outbound.settings.userLevel = 0;
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
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static int statistic(Config config, ref V2rayConfig v2rayConfig)
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

                if (!v2rayConfig.inbounds.Exists(item => item.tag == tag))
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

                if (!v2rayConfig.routing.rules.Exists(item => item.outboundTag == tag))
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
        /// Generate custom configuration
        /// </summary>
        /// <param name="node"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        private static int GenerateClientCustomConfig(ProfileItem node, string fileName, out string msg)
        {
            try
            {
                if (node == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                string addressFileName = node.address;
                if (!File.Exists(addressFileName))
                {
                    addressFileName = Utils.GetConfigPath(addressFileName);
                }
                if (!File.Exists(addressFileName))
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }
                File.Copy(addressFileName, fileName);

                //check again
                if (!File.Exists(fileName))
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                //overwrite port
                if (node.preSocksPort <= 0)
                {
                    var fileContent = File.ReadAllLines(fileName).ToList();
                    var coreType = LazyConfig.Instance.GetCoreType(node, node.configType);
                    switch (coreType)
                    {
                        case ECoreType.v2fly:
                        case ECoreType.SagerNet:
                        case ECoreType.Xray:
                        case ECoreType.v2fly_v5:
                            break;
                        case ECoreType.clash:
                        case ECoreType.clash_meta:
                            //remove the original 
                            var indexPort = fileContent.FindIndex(t => t.Contains("port:"));
                            if (indexPort >= 0)
                            {
                                fileContent.RemoveAt(indexPort);
                            }
                            indexPort = fileContent.FindIndex(t => t.Contains("socks-port:"));
                            if (indexPort >= 0)
                            {
                                fileContent.RemoveAt(indexPort);
                            }

                            fileContent.Add($"port: {LazyConfig.Instance.GetLocalPort(Global.InboundHttp)}");
                            fileContent.Add($"socks-port: {LazyConfig.Instance.GetLocalPort(Global.InboundSocks)}");
                            break;
                    }
                    File.WriteAllLines(fileName, fileContent);
                }

                msg = string.Format(ResUI.SuccessfulConfiguration, "");
            }
            catch (Exception ex)
            {
                Utils.SaveLog("GenerateClientCustomConfig", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        public static int GenerateClientConfigContent(ProfileItem node, bool blExport, ref V2rayConfig v2rayConfig, out string msg)
        {
            try
            {
                if (node == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(SampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                var config = LazyConfig.Instance.GetConfig();

                log(config, ref v2rayConfig, blExport);

                inbound(config, ref v2rayConfig);

                routing(config, ref v2rayConfig);

                //outbound
                outbound(node, ref v2rayConfig);

                //dns
                dns(config, ref v2rayConfig);

                //stat
                statistic(config, ref v2rayConfig);

                msg = string.Format(ResUI.SuccessfulConfiguration, "");
            }
            catch (Exception ex)
            {
                Utils.SaveLog("GenerateClientConfig", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        #endregion

        #region Generate server-side configuration

        public static int GenerateServerConfig(ProfileItem node, string fileName, out string msg)
        {
            try
            {
                if (node == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(SampleServer);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                var config = LazyConfig.Instance.GetConfig();

                log(config, ref v2rayConfig, true);

                ServerInbound(node, ref v2rayConfig);

                ServerOutbound(config, ref v2rayConfig);

                Utils.ToJsonFile(v2rayConfig, fileName, false);

                msg = string.Format(ResUI.SuccessfulConfiguration, node.GetSummary());
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        private static int ServerInbound(ProfileItem node, ref V2rayConfig v2rayConfig)
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
                inbound.port = node.port;

                usersItem.id = node.id;
                usersItem.email = Global.userEMail;

                if (node.configType == EConfigType.VMess)
                {
                    inbound.protocol = Global.vmessProtocolLite;
                    usersItem.alterId = node.alterId;

                }
                else if (node.configType == EConfigType.VLESS)
                {
                    inbound.protocol = Global.vlessProtocolLite;
                    usersItem.flow = node.flow;
                    inbound.settings.decryption = node.security;
                }

                boundStreamSettings(node, "in", inbound.streamSettings);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private static int ServerOutbound(Config config, ref V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.outbounds[0] != null)
                {
                    v2rayConfig.outbounds[0].settings = null;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }
        #endregion

        #region Import (export) client/server configuration

        public static ProfileItem ImportFromClientConfig(string fileName, out string msg)
        {
            msg = string.Empty;
            ProfileItem profileItem = new ProfileItem();

            try
            {
                string result = Utils.LoadResource(fileName);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedReadConfiguration;
                    return null;
                }

                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedConversionConfiguration;
                    return null;
                }

                if (v2rayConfig.outbounds == null
                 || v2rayConfig.outbounds.Count <= 0)
                {
                    msg = ResUI.IncorrectClientConfiguration;
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
                    msg = ResUI.IncorrectClientConfiguration;
                    return null;
                }

                profileItem.security = Global.DefaultSecurity;
                profileItem.network = Global.DefaultNetwork;
                profileItem.headerType = Global.None;
                profileItem.address = outbound.settings.vnext[0].address;
                profileItem.port = outbound.settings.vnext[0].port;
                profileItem.id = outbound.settings.vnext[0].users[0].id;
                profileItem.alterId = outbound.settings.vnext[0].users[0].alterId;
                profileItem.remarks = $"import@{DateTime.Now.ToShortDateString()}";

                //tcp or kcp
                if (outbound.streamSettings != null
                    && outbound.streamSettings.network != null
                    && !Utils.IsNullOrEmpty(outbound.streamSettings.network))
                {
                    profileItem.network = outbound.streamSettings.network;
                }

                //tcp http
                if (outbound.streamSettings != null
                    && outbound.streamSettings.tcpSettings != null
                    && outbound.streamSettings.tcpSettings.header != null
                    && !Utils.IsNullOrEmpty(outbound.streamSettings.tcpSettings.header.type))
                {
                    if (outbound.streamSettings.tcpSettings.header.type.Equals(Global.TcpHeaderHttp))
                    {
                        profileItem.headerType = outbound.streamSettings.tcpSettings.header.type;
                        string request = Convert.ToString(outbound.streamSettings.tcpSettings.header.request);
                        if (!Utils.IsNullOrEmpty(request))
                        {
                            V2rayTcpRequest v2rayTcpRequest = Utils.FromJson<V2rayTcpRequest>(request);
                            if (v2rayTcpRequest != null
                                && v2rayTcpRequest.headers != null
                                && v2rayTcpRequest.headers.Host != null
                                && v2rayTcpRequest.headers.Host.Count > 0)
                            {
                                profileItem.requestHost = v2rayTcpRequest.headers.Host[0];
                            }
                        }
                    }
                }
                //kcp
                if (outbound.streamSettings != null
                    && outbound.streamSettings.kcpSettings != null
                    && outbound.streamSettings.kcpSettings.header != null
                    && !Utils.IsNullOrEmpty(outbound.streamSettings.kcpSettings.header.type))
                {
                    profileItem.headerType = outbound.streamSettings.kcpSettings.header.type;
                }

                //ws
                if (outbound.streamSettings != null
                    && outbound.streamSettings.wsSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(outbound.streamSettings.wsSettings.path))
                    {
                        profileItem.path = outbound.streamSettings.wsSettings.path;
                    }
                    if (outbound.streamSettings.wsSettings.headers != null
                      && !Utils.IsNullOrEmpty(outbound.streamSettings.wsSettings.headers.Host))
                    {
                        profileItem.requestHost = outbound.streamSettings.wsSettings.headers.Host;
                    }
                }

                //h2
                if (outbound.streamSettings != null
                    && outbound.streamSettings.httpSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(outbound.streamSettings.httpSettings.path))
                    {
                        profileItem.path = outbound.streamSettings.httpSettings.path;
                    }
                    if (outbound.streamSettings.httpSettings.host != null
                        && outbound.streamSettings.httpSettings.host.Count > 0)
                    {
                        profileItem.requestHost = Utils.List2String(outbound.streamSettings.httpSettings.host);
                    }
                }

                //tls
                if (outbound.streamSettings != null
                    && outbound.streamSettings.security != null
                    && outbound.streamSettings.security == Global.StreamSecurity)
                {
                    profileItem.streamSecurity = Global.StreamSecurity;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ResUI.IncorrectClientConfiguration;
                return null;
            }

            return profileItem;
        }

        public static ProfileItem ImportFromServerConfig(string fileName, out string msg)
        {
            msg = string.Empty;
            ProfileItem profileItem = new ProfileItem();

            try
            {
                string result = Utils.LoadResource(fileName);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedReadConfiguration;
                    return null;
                }

                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedConversionConfiguration;
                    return null;
                }

                if (v2rayConfig.inbounds == null
                 || v2rayConfig.inbounds.Count <= 0)
                {
                    msg = ResUI.IncorrectServerConfiguration;
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
                    msg = ResUI.IncorrectServerConfiguration;
                    return null;
                }

                profileItem.security = Global.DefaultSecurity;
                profileItem.network = Global.DefaultNetwork;
                profileItem.headerType = Global.None;
                profileItem.address = string.Empty;
                profileItem.port = inbound.port;
                profileItem.id = inbound.settings.clients[0].id;
                profileItem.alterId = inbound.settings.clients[0].alterId;

                profileItem.remarks = $"import@{DateTime.Now.ToShortDateString()}";

                //tcp or kcp
                if (inbound.streamSettings != null
                    && inbound.streamSettings.network != null
                    && !Utils.IsNullOrEmpty(inbound.streamSettings.network))
                {
                    profileItem.network = inbound.streamSettings.network;
                }

                //tcp http
                if (inbound.streamSettings != null
                    && inbound.streamSettings.tcpSettings != null
                    && inbound.streamSettings.tcpSettings.header != null
                    && !Utils.IsNullOrEmpty(inbound.streamSettings.tcpSettings.header.type))
                {
                    if (inbound.streamSettings.tcpSettings.header.type.Equals(Global.TcpHeaderHttp))
                    {
                        profileItem.headerType = inbound.streamSettings.tcpSettings.header.type;
                        string request = Convert.ToString(inbound.streamSettings.tcpSettings.header.request);
                        if (!Utils.IsNullOrEmpty(request))
                        {
                            V2rayTcpRequest v2rayTcpRequest = Utils.FromJson<V2rayTcpRequest>(request);
                            if (v2rayTcpRequest != null
                                && v2rayTcpRequest.headers != null
                                && v2rayTcpRequest.headers.Host != null
                                && v2rayTcpRequest.headers.Host.Count > 0)
                            {
                                profileItem.requestHost = v2rayTcpRequest.headers.Host[0];
                            }
                        }
                    }
                }
                //kcp
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
                        profileItem.path = inbound.streamSettings.wsSettings.path;
                    }
                    if (inbound.streamSettings.wsSettings.headers != null
                      && !Utils.IsNullOrEmpty(inbound.streamSettings.wsSettings.headers.Host))
                    {
                        profileItem.requestHost = inbound.streamSettings.wsSettings.headers.Host;
                    }
                }

                //h2
                if (inbound.streamSettings != null
                    && inbound.streamSettings.httpSettings != null)
                {
                    if (!Utils.IsNullOrEmpty(inbound.streamSettings.httpSettings.path))
                    {
                        profileItem.path = inbound.streamSettings.httpSettings.path;
                    }
                    if (inbound.streamSettings.httpSettings.host != null
                        && inbound.streamSettings.httpSettings.host.Count > 0)
                    {
                        profileItem.requestHost = Utils.List2String(inbound.streamSettings.httpSettings.host);
                    }
                }

                //tls
                if (inbound.streamSettings != null
                    && inbound.streamSettings.security != null
                    && inbound.streamSettings.security == Global.StreamSecurity)
                {
                    profileItem.streamSecurity = Global.StreamSecurity;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ResUI.IncorrectClientConfiguration;
                return null;
            }
            return profileItem;
        }

        public static int Export2ClientConfig(ProfileItem node, string fileName, out string msg)
        {
            V2rayConfig v2rayConfig = null;
            if (GenerateClientConfigContent(node, true, ref v2rayConfig, out msg) != 0)
            {
                return -1;
            }
            return Utils.ToJsonFile(v2rayConfig, fileName, false);
        }

        public static int Export2ServerConfig(ProfileItem node, string fileName, out string msg)
        {
            return GenerateServerConfig(node, fileName, out msg);
        }

        #endregion

        #region Gen speedtest config


        public static string GenerateClientSpeedtestConfigString(Config config, List<ServerTestItem> selecteds, out string msg)
        {
            try
            {
                if (config == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return "";
                }

                msg = ResUI.InitialConfiguration;

                Config configCopy = Utils.DeepCopy(config);

                string result = Utils.GetEmbedText(SampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return "";
                }

                V2rayConfig v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return "";
                }
                List<IPEndPoint> lstIpEndPoints = new List<IPEndPoint>();
                List<TcpConnectionInformation> lstTcpConns = new List<TcpConnectionInformation>();
                try
                {
                    lstIpEndPoints.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());
                    lstIpEndPoints.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners());
                    lstTcpConns.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections());
                }
                catch (Exception ex)
                {
                    Utils.SaveLog(ex.Message, ex);
                }

                log(configCopy, ref v2rayConfig, false);
                //routing(config, ref v2rayConfig);
                //dns(configCopy, ref v2rayConfig);

                v2rayConfig.inbounds.Clear(); // Remove "proxy" service for speedtest, avoiding port conflicts.

                int httpPort = LazyConfig.Instance.GetLocalPort("speedtest");

                foreach (var it in selecteds)
                {
                    if (it.configType == EConfigType.Custom)
                    {
                        continue;
                    }
                    if (it.port <= 0)
                    {
                        continue;
                    }
                    if (it.configType == EConfigType.VMess || it.configType == EConfigType.VLESS)
                    {
                        var item2 = LazyConfig.Instance.GetProfileItem(it.indexId);
                        if (item2 is null || Utils.IsNullOrEmpty(item2.id) || !Utils.IsGuidByParse(item2.id))
                        {
                            continue;
                        }
                    }

                    //find unuse port
                    var port = httpPort;
                    for (int k = httpPort; k < Global.MaxPort; k++)
                    {
                        if (lstIpEndPoints != null && lstIpEndPoints.FindIndex(_it => _it.Port == k) >= 0)
                        {
                            continue;
                        }
                        if (lstTcpConns != null && lstTcpConns.FindIndex(_it => _it.LocalEndPoint.Port == k) >= 0)
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
                    it.port = port;
                    it.allowTest = true;

                    //inbound
                    Inbounds inbound = new Inbounds
                    {
                        listen = Global.Loopback,
                        port = port,
                        protocol = Global.InboundHttp
                    };
                    inbound.tag = Global.InboundHttp + inbound.port.ToString();
                    v2rayConfig.inbounds.Add(inbound);

                    //outbound
                    V2rayConfig v2rayConfigCopy = Utils.FromJson<V2rayConfig>(result);
                    var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                    if (item is null)
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.Shadowsocks
                        && !Global.ssSecuritysInXray.Contains(item.security))
                    {
                        continue;
                    }

                    outbound(item, ref v2rayConfigCopy);
                    v2rayConfigCopy.outbounds[0].tag = Global.agentTag + inbound.port.ToString();
                    v2rayConfig.outbounds.Add(v2rayConfigCopy.outbounds[0]);

                    //rule
                    RulesItem rule = new RulesItem
                    {
                        inboundTag = new List<string> { inbound.tag },
                        outboundTag = v2rayConfigCopy.outbounds[0].tag,
                        type = "field"
                    };
                    v2rayConfig.routing.rules.Add(rule);
                }

                //msg = string.Format(ResUI.SuccessfulConfiguration"), node.getSummary());
                return Utils.ToJson(v2rayConfig);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return "";
            }
        }

        #endregion

    }
}
