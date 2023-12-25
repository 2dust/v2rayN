using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    internal class CoreConfigV2ray
    {
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

                string result = Utils.GetEmbedText(Global.V2raySampleClient);
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

                GenLog(v2rayConfig);

                GenInbounds(v2rayConfig);

                GenRouting(v2rayConfig);

                GenOutbound(node, v2rayConfig.outbounds[0]);

                GenMoreOutbounds(node, v2rayConfig);

                GenDns(v2rayConfig);

                GenStatistic(v2rayConfig);

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

        private int GenLog(V2rayConfig v2rayConfig)
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

        private int GenInbounds(V2rayConfig v2rayConfig)
        {
            try
            {
                v2rayConfig.inbounds = new List<Inbounds4Ray>();

                Inbounds4Ray? inbound = GetInbound(_config.inbound[0], Global.InboundSocks, 0, true);
                v2rayConfig.inbounds.Add(inbound);

                //http
                Inbounds4Ray? inbound2 = GetInbound(_config.inbound[0], Global.InboundHttp, 1, false);
                v2rayConfig.inbounds.Add(inbound2);

                if (_config.inbound[0].allowLANConn)
                {
                    if (_config.inbound[0].newPort4LAN)
                    {
                        Inbounds4Ray inbound3 = GetInbound(_config.inbound[0], Global.InboundSocks2, 2, true);
                        inbound3.listen = "0.0.0.0";
                        v2rayConfig.inbounds.Add(inbound3);

                        Inbounds4Ray inbound4 = GetInbound(_config.inbound[0], Global.InboundHttp2, 3, false);
                        inbound4.listen = "0.0.0.0";
                        v2rayConfig.inbounds.Add(inbound4);

                        //auth
                        if (!Utils.IsNullOrEmpty(_config.inbound[0].user) && !Utils.IsNullOrEmpty(_config.inbound[0].pass))
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts = new List<AccountsItem4Ray> { new AccountsItem4Ray() { user = _config.inbound[0].user, pass = _config.inbound[0].pass } };

                            inbound4.settings.auth = "password";
                            inbound4.settings.accounts = new List<AccountsItem4Ray> { new AccountsItem4Ray() { user = _config.inbound[0].user, pass = _config.inbound[0].pass } };
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

        private Inbounds4Ray? GetInbound(InItem inItem, string tag, int offset, bool bSocks)
        {
            string result = Utils.GetEmbedText(Global.V2raySampleInbound);
            if (Utils.IsNullOrEmpty(result))
            {
                return null;
            }

            var inbound = Utils.FromJson<Inbounds4Ray>(result);
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

        private int GenRouting(V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.routing?.rules != null)
                {
                    v2rayConfig.routing.domainStrategy = _config.routingBasicItem.domainStrategy;
                    v2rayConfig.routing.domainMatcher = Utils.IsNullOrEmpty(_config.routingBasicItem.domainMatcher) ? null : _config.routingBasicItem.domainMatcher;

                    if (_config.routingBasicItem.enableRoutingAdvanced)
                    {
                        var routing = ConfigHandler.GetDefaultRouting(_config);
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
                                    var item2 = Utils.FromJson<RulesItem4Ray>(Utils.ToJson(item));
                                    GenRoutingUserRule(item2, v2rayConfig);
                                }
                            }
                        }
                    }
                    else
                    {
                        var lockedItem = ConfigHandler.GetLockedRoutingItem(_config);
                        if (lockedItem != null)
                        {
                            var rules = Utils.FromJson<List<RulesItem>>(lockedItem.ruleSet);
                            foreach (var item in rules)
                            {
                                var item2 = Utils.FromJson<RulesItem4Ray>(Utils.ToJson(item));
                                GenRoutingUserRule(item2, v2rayConfig);
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

        private int GenRoutingUserRule(RulesItem4Ray rules, V2rayConfig v2rayConfig)
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

        private int GenOutbound(ProfileItem node, Outbounds4Ray outbound)
        {
            try
            {
                if (node.configType == EConfigType.VMess)
                {
                    VnextItem4Ray vnextItem;
                    if (outbound.settings.vnext.Count <= 0)
                    {
                        vnextItem = new VnextItem4Ray();
                        outbound.settings.vnext.Add(vnextItem);
                    }
                    else
                    {
                        vnextItem = outbound.settings.vnext[0];
                    }
                    vnextItem.address = node.address;
                    vnextItem.port = node.port;

                    UsersItem4Ray usersItem;
                    if (vnextItem.users.Count <= 0)
                    {
                        usersItem = new UsersItem4Ray();
                        vnextItem.users.Add(usersItem);
                    }
                    else
                    {
                        usersItem = vnextItem.users[0];
                    }
                    //远程服务器用户ID
                    usersItem.id = node.id;
                    usersItem.alterId = node.alterId;
                    usersItem.email = Global.UserEMail;
                    if (Global.VmessSecuritys.Contains(node.security))
                    {
                        usersItem.security = node.security;
                    }
                    else
                    {
                        usersItem.security = Global.DefaultSecurity;
                    }

                    GenOutboundMux(node, outbound, _config.coreBasicItem.muxEnabled);

                    outbound.protocol = Global.ProtocolTypes[EConfigType.VMess];
                    outbound.settings.servers = null;
                }
                else if (node.configType == EConfigType.Shadowsocks)
                {
                    ServersItem4Ray serversItem;
                    if (outbound.settings.servers.Count <= 0)
                    {
                        serversItem = new ServersItem4Ray();
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

                    GenOutboundMux(node, outbound, false);

                    outbound.protocol = Global.ProtocolTypes[EConfigType.Shadowsocks];
                    outbound.settings.vnext = null;
                }
                else if (node.configType == EConfigType.Socks)
                {
                    ServersItem4Ray serversItem;
                    if (outbound.settings.servers.Count <= 0)
                    {
                        serversItem = new ServersItem4Ray();
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
                        SocksUsersItem4Ray socksUsersItem = new()
                        {
                            user = node.security,
                            pass = node.id,
                            level = 1
                        };

                        serversItem.users = new List<SocksUsersItem4Ray>() { socksUsersItem };
                    }

                    GenOutboundMux(node, outbound, false);

                    outbound.protocol = Global.ProtocolTypes[EConfigType.Socks];
                    outbound.settings.vnext = null;
                }
                else if (node.configType == EConfigType.VLESS)
                {
                    VnextItem4Ray vnextItem;
                    if (outbound.settings.vnext.Count <= 0)
                    {
                        vnextItem = new VnextItem4Ray();
                        outbound.settings.vnext.Add(vnextItem);
                    }
                    else
                    {
                        vnextItem = outbound.settings.vnext[0];
                    }
                    vnextItem.address = node.address;
                    vnextItem.port = node.port;

                    UsersItem4Ray usersItem;
                    if (vnextItem.users.Count <= 0)
                    {
                        usersItem = new UsersItem4Ray();
                        vnextItem.users.Add(usersItem);
                    }
                    else
                    {
                        usersItem = vnextItem.users[0];
                    }
                    usersItem.id = node.id;
                    usersItem.email = Global.UserEMail;
                    usersItem.encryption = node.security;

                    GenOutboundMux(node, outbound, _config.coreBasicItem.muxEnabled);

                    if (node.streamSecurity == Global.StreamSecurityReality
                        || node.streamSecurity == Global.StreamSecurity)
                    {
                        if (!Utils.IsNullOrEmpty(node.flow))
                        {
                            usersItem.flow = node.flow;

                            GenOutboundMux(node, outbound, false);
                        }
                    }
                    if (node.streamSecurity == Global.StreamSecurityReality && Utils.IsNullOrEmpty(node.flow))
                    {
                        GenOutboundMux(node, outbound, _config.coreBasicItem.muxEnabled);
                    }

                    outbound.protocol = Global.ProtocolTypes[EConfigType.VLESS];
                    outbound.settings.servers = null;
                }
                else if (node.configType == EConfigType.Trojan)
                {
                    ServersItem4Ray serversItem;
                    if (outbound.settings.servers.Count <= 0)
                    {
                        serversItem = new ServersItem4Ray();
                        outbound.settings.servers.Add(serversItem);
                    }
                    else
                    {
                        serversItem = outbound.settings.servers[0];
                    }
                    serversItem.address = node.address;
                    serversItem.port = node.port;
                    serversItem.password = node.id;

                    serversItem.ota = false;
                    serversItem.level = 1;

                    GenOutboundMux(node, outbound, false);

                    outbound.protocol = Global.ProtocolTypes[EConfigType.Trojan];
                    outbound.settings.vnext = null;
                }
                GenBoundStreamSettings(node, outbound.streamSettings);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenOutboundMux(ProfileItem node, Outbounds4Ray outbound, bool enabled)
        {
            try
            {
                if (enabled)
                {
                    outbound.mux.enabled = true;
                    outbound.mux.concurrency = 8;
                }
                else
                {
                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenBoundStreamSettings(ProfileItem node, StreamSettings4Ray streamSettings)
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
                        useragent = Global.UserAgentTxts[_config.coreBasicItem.defUserAgent];
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

                    TlsSettings4Ray tlsSettings = new()
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

                    TlsSettings4Ray realitySettings = new()
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
                        KcpSettings4Ray kcpSettings = new()
                        {
                            mtu = _config.kcpItem.mtu,
                            tti = _config.kcpItem.tti
                        };

                        kcpSettings.uplinkCapacity = _config.kcpItem.uplinkCapacity;
                        kcpSettings.downlinkCapacity = _config.kcpItem.downlinkCapacity;

                        kcpSettings.congestion = _config.kcpItem.congestion;
                        kcpSettings.readBufferSize = _config.kcpItem.readBufferSize;
                        kcpSettings.writeBufferSize = _config.kcpItem.writeBufferSize;
                        kcpSettings.header = new Header4Ray
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
                        WsSettings4Ray wsSettings = new();
                        wsSettings.headers = new Headers4Ray();
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
                        HttpSettings4Ray httpSettings = new();

                        if (!string.IsNullOrWhiteSpace(host))
                        {
                            httpSettings.host = Utils.String2List(host);
                        }
                        httpSettings.path = node.path;

                        streamSettings.httpSettings = httpSettings;

                        break;
                    //quic
                    case "quic":
                        QuicSettings4Ray quicsettings = new()
                        {
                            security = host,
                            key = node.path,
                            header = new Header4Ray
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
                        GrpcSettings4Ray grpcSettings = new()
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
                            TcpSettings4Ray tcpSettings = new()
                            {
                                header = new Header4Ray
                                {
                                    type = node.headerType
                                }
                            };

                            //request Host
                            string request = Utils.GetEmbedText(Global.V2raySampleHttprequestFileName);
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

        private int GenDns(V2rayConfig v2rayConfig)
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

                var obj = Utils.ParseJson(normalDNS) ?? new JObject();

                if (!obj.ContainsKey("servers"))
                {
                    List<string> servers = new();
                    string[] arrDNS = normalDNS.Split(',');
                    foreach (string str in arrDNS)
                    {
                        servers.Add(str);
                    }
                    obj["servers"] = JArray.FromObject(servers);
                }

                if (item.useSystemHosts)
                {
                    var hostfile = @"C:\Windows\System32\drivers\etc\hosts";

                    if (File.Exists(hostfile))
                    {
                        var hosts = File.ReadAllText(hostfile).Replace("\r", "");
                        var hostsList = hosts.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        // 获取系统hosts
                        var systemHosts = new Dictionary<string, string>();
                        foreach (var host in hostsList)
                        {
                            if (host.StartsWith("#")) continue;
                            var hostItem = host.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (hostItem.Length < 2) continue;
                            systemHosts.Add(hostItem[1], hostItem[0]);
                        }

                        // 追加至 dns 设置
                        var normalHost = obj["hosts"] ?? new JObject();
                        foreach (var host in systemHosts)
                        {
                            if (normalHost[host.Key] != null) continue;
                            normalHost[host.Key] = host.Value;
                        }
                        obj["hosts"] = normalHost;
                    }
                }

                v2rayConfig.dns = obj;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenStatistic(V2rayConfig v2rayConfig)
        {
            if (_config.guiItem.enableStatistics)
            {
                string tag = Global.InboundAPITagName;
                API4Ray apiObj = new();
                Policy4Ray policyObj = new();
                SystemPolicy4Ray policySystemSetting = new();

                string[] services = { "StatsService" };

                v2rayConfig.stats = new Stats4Ray();

                apiObj.tag = tag;
                apiObj.services = services.ToList();
                v2rayConfig.api = apiObj;

                policySystemSetting.statsOutboundDownlink = true;
                policySystemSetting.statsOutboundUplink = true;
                policyObj.system = policySystemSetting;
                v2rayConfig.policy = policyObj;

                if (!v2rayConfig.inbounds.Exists(item => item.tag == tag))
                {
                    Inbounds4Ray apiInbound = new();
                    Inboundsettings4Ray apiInboundSettings = new();
                    apiInbound.tag = tag;
                    apiInbound.listen = Global.Loopback;
                    apiInbound.port = Global.StatePort;
                    apiInbound.protocol = Global.InboundAPIProtocal;
                    apiInboundSettings.address = Global.Loopback;
                    apiInbound.settings = apiInboundSettings;
                    v2rayConfig.inbounds.Add(apiInbound);
                }

                if (!v2rayConfig.routing.rules.Exists(item => item.outboundTag == tag))
                {
                    RulesItem4Ray apiRoutingRule = new()
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

        private int GenMoreOutbounds(ProfileItem node, V2rayConfig v2rayConfig)
        {
            if (node.subid.IsNullOrEmpty())
            {
                return 0;
            }
            try
            {
                var subItem = LazyConfig.Instance.GetSubItem(node.subid);
                if (subItem is null)
                {
                    return 0;
                }

                //current proxy
                var outbound = v2rayConfig.outbounds[0];
                var txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);

                //Previous proxy
                var prevNode = LazyConfig.Instance.GetProfileItemViaRemarks(subItem.prevProfile!);
                if (prevNode is not null
                    && prevNode.configType != EConfigType.Custom
                    && prevNode.configType != EConfigType.Hysteria2
                    && prevNode.configType != EConfigType.Tuic)
                {
                    var prevOutbound = Utils.FromJson<Outbounds4Ray>(txtOutbound);
                    GenOutbound(prevNode, prevOutbound);
                    prevOutbound.tag = $"{Global.ProxyTag}2";
                    v2rayConfig.outbounds.Add(prevOutbound);

                    outbound.streamSettings.sockopt = new()
                    {
                        dialerProxy = prevOutbound.tag
                    };
                }

                //Next proxy
                var nextNode = LazyConfig.Instance.GetProfileItemViaRemarks(subItem.nextProfile!);
                if (nextNode is not null
                    && nextNode.configType != EConfigType.Custom
                    && nextNode.configType != EConfigType.Hysteria2
                    && nextNode.configType != EConfigType.Tuic)
                {
                    var nextOutbound = Utils.FromJson<Outbounds4Ray>(txtOutbound);
                    GenOutbound(nextNode, nextOutbound);
                    nextOutbound.tag = Global.ProxyTag;
                    v2rayConfig.outbounds.Insert(0, nextOutbound);

                    outbound.tag = $"{Global.ProxyTag}1";
                    nextOutbound.streamSettings.sockopt = new()
                    {
                        dialerProxy = outbound.tag
                    };
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
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

                string result = Utils.GetEmbedText(Global.V2raySampleClient);
                string txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);
                if (Utils.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return "";
                }

                var v2rayConfig = Utils.FromJson<V2rayConfig>(result);
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

                GenLog(v2rayConfig);
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
                    Inbounds4Ray inbound = new()
                    {
                        listen = Global.Loopback,
                        port = port,
                        protocol = Global.InboundHttp
                    };
                    inbound.tag = Global.InboundHttp + inbound.port.ToString();
                    v2rayConfig.inbounds.Add(inbound);

                    //outbound
                    var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                    if (item is null)
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.Shadowsocks
                        && !Global.SsSecuritysInXray.Contains(item.security))
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.VLESS
                     && !Global.Flows.Contains(item.flow))
                    {
                        continue;
                    }

                    var outbound = Utils.FromJson<Outbounds4Ray>(txtOutbound);
                    GenOutbound(item, outbound);
                    outbound.tag = Global.ProxyTag + inbound.port.ToString();
                    v2rayConfig.outbounds.Add(outbound);

                    //rule
                    RulesItem4Ray rule = new()
                    {
                        inboundTag = new List<string> { inbound.tag },
                        outboundTag = outbound.tag,
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