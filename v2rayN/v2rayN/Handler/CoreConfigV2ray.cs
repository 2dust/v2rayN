using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    internal class CoreConfigV2ray
    {
        private string SampleClient = Global.v2raySampleClient;
        private Config _config;

        public CoreConfigV2ray(Config config)
        {
            _config = config;
        }

        public int GenerateClientConfigContent(ProfileItem node, out V2rayConfig? v2rayConfig, out string msg)
        {
            v2rayConfig = null;
            try
            {
                if (node == null
                    || node.port <= 0)
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

                log(v2rayConfig);

                inbound(v2rayConfig);

                routing(v2rayConfig);

                outbound(node, v2rayConfig);

                dns(v2rayConfig);

                statistic(v2rayConfig);

                msg = string.Format(ResUI.SuccessfulConfiguration, "");
            }
            catch (Exception ex)
            {
                Utils.SaveLog("GenerateClientConfig4V2ray", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        private int log(V2rayConfig v2rayConfig)
        {
            try
            {
                if (_config.coreBasicItem.logEnabled)
                {
                    var dtNow = DateTime.Now;
                    v2rayConfig.log.loglevel = _config.coreBasicItem.loglevel;
                    v2rayConfig.log.access = Utils.GetLogPath($"Vaccess_{dtNow:yyyy-MM-dd}.txt");
                    v2rayConfig.log.error = Utils.GetLogPath($"Verror_{dtNow:yyyy-MM-dd}.txt");
                }
                else
                {
                    v2rayConfig.log.loglevel = _config.coreBasicItem.loglevel;
                    v2rayConfig.log.access = "";
                    v2rayConfig.log.error = "";
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int inbound(V2rayConfig v2rayConfig)
        {
            try
            {
                v2rayConfig.inbounds = new List<Inbounds>();

                Inbounds? inbound = GetInbound(_config.inbound[0], Global.InboundSocks, 0, true);
                v2rayConfig.inbounds.Add(inbound);

                //http
                Inbounds? inbound2 = GetInbound(_config.inbound[0], Global.InboundHttp, 1, false);
                v2rayConfig.inbounds.Add(inbound2);

                if (_config.inbound[0].allowLANConn)
                {
                    if (_config.inbound[0].newPort4LAN)
                    {
                        Inbounds inbound3 = GetInbound(_config.inbound[0], Global.InboundSocks2, 2, true);
                        inbound3.listen = "0.0.0.0";
                        v2rayConfig.inbounds.Add(inbound3);

                        Inbounds inbound4 = GetInbound(_config.inbound[0], Global.InboundHttp2, 3, false);
                        inbound4.listen = "0.0.0.0";
                        v2rayConfig.inbounds.Add(inbound4);

                        //auth
                        if (!Utils.IsNullOrEmpty(_config.inbound[0].user) && !Utils.IsNullOrEmpty(_config.inbound[0].pass))
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts = new List<AccountsItem> { new AccountsItem() { user = _config.inbound[0].user, pass = _config.inbound[0].pass } };

                            inbound4.settings.auth = "password";
                            inbound4.settings.accounts = new List<AccountsItem> { new AccountsItem() { user = _config.inbound[0].user, pass = _config.inbound[0].pass } };
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

        private Inbounds? GetInbound(InItem inItem, string tag, int offset, bool bSocks)
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

        private int routing(V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.routing?.rules != null)
                {
                    v2rayConfig.routing.domainStrategy = _config.routingBasicItem.domainStrategy;
                    v2rayConfig.routing.domainMatcher = Utils.IsNullOrEmpty(_config.routingBasicItem.domainMatcher) ? null : _config.routingBasicItem.domainMatcher;

                    if (_config.routingBasicItem.enableRoutingAdvanced)
                    {
                        var routing = ConfigHandler.GetDefaultRouting(ref _config);
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
                                    routingUserRule(item, v2rayConfig);
                                }
                            }
                        }
                    }
                    else
                    {
                        var lockedItem = ConfigHandler.GetLockedRoutingItem(ref _config);
                        if (lockedItem != null)
                        {
                            var rules = Utils.FromJson<List<RulesItem>>(lockedItem.ruleSet);
                            foreach (var item in rules)
                            {
                                routingUserRule(item, v2rayConfig);
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

        private int routingUserRule(RulesItem rules, V2rayConfig v2rayConfig)
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
                if (rules.domain?.Count == 0)
                {
                    rules.domain = null;
                }
                if (rules.ip?.Count == 0)
                {
                    rules.ip = null;
                }
                if (rules.protocol?.Count == 0)
                {
                    rules.protocol = null;
                }
                if (rules.inboundTag?.Count == 0)
                {
                    rules.inboundTag = null;
                }

                var hasDomainIp = false;
                if (rules.domain?.Count > 0)
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
                if (rules.ip?.Count > 0)
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
                        || (rules.protocol?.Count > 0)
                        || (rules.inboundTag?.Count > 0)
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

        private int outbound(ProfileItem node, V2rayConfig v2rayConfig)
        {
            try
            {
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
                    outbound.mux.enabled = _config.coreBasicItem.muxEnabled;
                    outbound.mux.concurrency = _config.coreBasicItem.muxEnabled ? 8 : -1;

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
                        SocksUsersItem socksUsersItem = new()
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
                    outbound.mux.enabled = _config.coreBasicItem.muxEnabled;
                    outbound.mux.concurrency = _config.coreBasicItem.muxEnabled ? 8 : -1;

                    if (node.streamSecurity == Global.StreamSecurityReality
                        || node.streamSecurity == Global.StreamSecurity)
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

                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;

                    outbound.protocol = Global.trojanProtocolLite;
                    outbound.settings.vnext = null;
                }
                boundStreamSettings(node, outbound.streamSettings);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int boundStreamSettings(ProfileItem node, StreamSettings streamSettings)
        {
            try
            {
                streamSettings.network = node.GetNetwork();
                string host = node.requestHost.TrimEx();
                string sni = node.sni;
                string useragent = "";
                if (!_config.coreBasicItem.defUserAgent.IsNullOrEmpty())
                {
                    try
                    {
                        useragent = Global.userAgentTxt[_config.coreBasicItem.defUserAgent];
                    }
                    catch (KeyNotFoundException)
                    {
                        useragent = _config.coreBasicItem.defUserAgent;
                    }
                }

                //if tls
                if (node.streamSecurity == Global.StreamSecurity)
                {
                    streamSettings.security = node.streamSecurity;

                    TlsSettings tlsSettings = new()
                    {
                        allowInsecure = Utils.ToBool(node.allowInsecure.IsNullOrEmpty() ? _config.coreBasicItem.defAllowInsecure.ToString().ToLower() : node.allowInsecure),
                        alpn = node.GetAlpn(),
                        fingerprint = node.fingerprint.IsNullOrEmpty() ? _config.coreBasicItem.defFingerprint : node.fingerprint
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

                //if Reality
                if (node.streamSecurity == Global.StreamSecurityReality)
                {
                    streamSettings.security = node.streamSecurity;

                    TlsSettings realitySettings = new()
                    {
                        fingerprint = node.fingerprint.IsNullOrEmpty() ? _config.coreBasicItem.defFingerprint : node.fingerprint,
                        serverName = sni,
                        publicKey = node.publicKey,
                        shortId = node.shortId,
                        spiderX = node.spiderX,
                    };

                    streamSettings.realitySettings = realitySettings;
                }

                //streamSettings
                switch (node.GetNetwork())
                {
                    case "kcp":
                        KcpSettings kcpSettings = new()
                        {
                            mtu = _config.kcpItem.mtu,
                            tti = _config.kcpItem.tti
                        };

                        kcpSettings.uplinkCapacity = _config.kcpItem.uplinkCapacity;
                        kcpSettings.downlinkCapacity = _config.kcpItem.downlinkCapacity;

                        kcpSettings.congestion = _config.kcpItem.congestion;
                        kcpSettings.readBufferSize = _config.kcpItem.readBufferSize;
                        kcpSettings.writeBufferSize = _config.kcpItem.writeBufferSize;
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
                        WsSettings wsSettings = new();
                        wsSettings.headers = new Headers();
                        string path = node.path;
                        if (!string.IsNullOrWhiteSpace(host))
                        {
                            wsSettings.headers.Host = host;
                        }
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            wsSettings.path = path;
                        }
                        if (!string.IsNullOrWhiteSpace(useragent))
                        {
                            wsSettings.headers.UserAgent = useragent;
                        }
                        streamSettings.wsSettings = wsSettings;

                        break;
                    //h2
                    case "h2":
                        HttpSettings httpSettings = new();

                        if (!string.IsNullOrWhiteSpace(host))
                        {
                            httpSettings.host = Utils.String2List(host);
                        }
                        httpSettings.path = node.path;

                        streamSettings.httpSettings = httpSettings;

                        break;
                    //quic
                    case "quic":
                        QuicSettings quicsettings = new()
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
                        GrpcSettings grpcSettings = new()
                        {
                            serviceName = node.path,
                            multiMode = (node.headerType == Global.GrpcmultiMode),
                            idle_timeout = _config.grpcItem.idle_timeout,
                            health_check_timeout = _config.grpcItem.health_check_timeout,
                            permit_without_stream = _config.grpcItem.permit_without_stream,
                            initial_windows_size = _config.grpcItem.initial_windows_size,
                        };
                        streamSettings.grpcSettings = grpcSettings;
                        break;

                    default:
                        //tcp
                        if (node.headerType == Global.TcpHeaderHttp)
                        {
                            TcpSettings tcpSettings = new()
                            {
                                header = new Header
                                {
                                    type = node.headerType
                                }
                            };

                            //request Host
                            string request = Utils.GetEmbedText(Global.v2raySampleHttprequestFileName);
                            string[] arrHost = host.Split(',');
                            string host2 = string.Join("\",\"", arrHost);
                            request = request.Replace("$requestHost$", $"\"{host2}\"");
                            //request = request.Replace("$requestHost$", string.Format("\"{0}\"", config.requestHost()));
                            request = request.Replace("$requestUserAgent$", $"\"{useragent}\"");
                            //Path
                            string pathHttp = @"/";
                            if (!Utils.IsNullOrEmpty(node.path))
                            {
                                string[] arrPath = node.path.Split(',');
                                pathHttp = string.Join("\",\"", arrPath);
                            }
                            request = request.Replace("$requestPath$", $"\"{pathHttp}\"");
                            tcpSettings.header.request = Utils.FromJson<object>(request);

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

        private int dns(V2rayConfig v2rayConfig)
        {
            try
            {
                var item = LazyConfig.Instance.GetDNSItem(ECoreType.Xray);
                var normalDNS = item?.normalDNS;
                var domainStrategy4Freedom = item?.domainStrategy4Freedom;
                if (string.IsNullOrWhiteSpace(normalDNS))
                {
                    normalDNS = "1.1.1.1,8.8.8.8";
                }

                //Outbound Freedom domainStrategy
                if (!string.IsNullOrWhiteSpace(domainStrategy4Freedom))
                {
                    var outbound = v2rayConfig.outbounds[1];
                    outbound.settings.domainStrategy = domainStrategy4Freedom;
                    outbound.settings.userLevel = 0;
                }

                var obj = Utils.ParseJson(normalDNS);
                if (obj?.ContainsKey("servers") == true)
                {
                    v2rayConfig.dns = obj;
                }
                else
                {
                    List<string> servers = new();

                    string[] arrDNS = normalDNS.Split(',');
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

        private int statistic(V2rayConfig v2rayConfig)
        {
            if (_config.guiItem.enableStatistics)
            {
                string tag = Global.InboundAPITagName;
                API apiObj = new();
                Policy policyObj = new();
                SystemPolicy policySystemSetting = new();

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
                    Inbounds apiInbound = new();
                    Inboundsettings apiInboundSettings = new();
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
                    RulesItem apiRoutingRule = new()
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

        #region Gen speedtest config

        public string GenerateClientSpeedtestConfigString(List<ServerTestItem> selecteds, out string msg)
        {
            try
            {
                if (_config == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return "";
                }

                msg = ResUI.InitialConfiguration;

                Config configCopy = Utils.DeepCopy(_config);

                string result = Utils.GetEmbedText(SampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return "";
                }

                V2rayConfig? v2rayConfig = Utils.FromJson<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return "";
                }
                List<IPEndPoint> lstIpEndPoints = new();
                List<TcpConnectionInformation> lstTcpConns = new();
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

                log(v2rayConfig);
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
                    if (it.configType is EConfigType.VMess or EConfigType.VLESS)
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
                        if (lstIpEndPoints?.FindIndex(_it => _it.Port == k) >= 0)
                        {
                            continue;
                        }
                        if (lstTcpConns?.FindIndex(_it => _it.LocalEndPoint.Port == k) >= 0)
                        {
                            continue;
                        }
                        //found
                        port = k;
                        httpPort = port + 1;
                        break;
                    }

                    //Port In Used
                    if (lstIpEndPoints?.FindIndex(_it => _it.Port == port) >= 0)
                    {
                        continue;
                    }
                    it.port = port;
                    it.allowTest = true;

                    //inbound
                    Inbounds inbound = new()
                    {
                        listen = Global.Loopback,
                        port = port,
                        protocol = Global.InboundHttp
                    };
                    inbound.tag = Global.InboundHttp + inbound.port.ToString();
                    v2rayConfig.inbounds.Add(inbound);

                    //outbound
                    V2rayConfig? v2rayConfigCopy = Utils.FromJson<V2rayConfig>(result);
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
                    if (item.configType == EConfigType.VLESS
                     && !Global.flows.Contains(item.flow))
                    {
                        continue;
                    }

                    outbound(item, v2rayConfigCopy);
                    v2rayConfigCopy.outbounds[0].tag = Global.agentTag + inbound.port.ToString();
                    v2rayConfig.outbounds.Add(v2rayConfigCopy.outbounds[0]);

                    //rule
                    RulesItem rule = new()
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

        #endregion Gen speedtest config
    }
}