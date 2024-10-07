using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig
{
    public class CoreConfigV2rayService
    {
        private Config _config;

        public CoreConfigV2rayService(Config config)
        {
            _config = config;
        }

        #region public gen function

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

                v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
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

                GenDns(node, v2rayConfig);

                GenStatistic(v2rayConfig);

                msg = string.Format(ResUI.SuccessfulConfiguration, "");
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GenerateClientConfig4V2ray", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        public int GenerateClientMultipleLoadConfig(List<ProfileItem> selecteds, out V2rayConfig? v2rayConfig, out string msg)
        {
            v2rayConfig = null;
            try
            {
                if (_config == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(Global.V2raySampleClient);
                string txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);
                if (Utils.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                GenLog(v2rayConfig);
                GenInbounds(v2rayConfig);
                GenRouting(v2rayConfig);
                GenDns(null, v2rayConfig);
                GenStatistic(v2rayConfig);
                v2rayConfig.outbounds.RemoveAt(0);

                var tagProxy = new List<string>();
                foreach (var it in selecteds)
                {
                    if (it.configType == EConfigType.Custom)
                    {
                        continue;
                    }
                    if (it.configType is EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.WireGuard)
                    {
                        continue;
                    }
                    if (it.port <= 0)
                    {
                        continue;
                    }
                    var item = AppHandler.Instance.GetProfileItem(it.indexId);
                    if (item is null)
                    {
                        continue;
                    }
                    if (it.configType is EConfigType.VMess or EConfigType.VLESS)
                    {
                        if (Utils.IsNullOrEmpty(item.id) || !Utils.IsGuidByParse(item.id))
                        {
                            continue;
                        }
                    }
                    if (item.configType == EConfigType.Shadowsocks
                      && !Global.SsSecuritiesInSingbox.Contains(item.security))
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.VLESS && !Global.Flows.Contains(item.flow))
                    {
                        continue;
                    }

                    //outbound
                    var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    GenOutbound(item, outbound);
                    outbound.tag = $"{Global.ProxyTag}-{tagProxy.Count + 1}";
                    v2rayConfig.outbounds.Add(outbound);
                    tagProxy.Add(outbound.tag);
                }
                if (tagProxy.Count <= 0)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                //add balancers
                var balancer = new BalancersItem4Ray
                {
                    selector = [Global.ProxyTag],
                    strategy = new() { type = "roundRobin" },
                    tag = $"{Global.ProxyTag}-round",
                };
                v2rayConfig.routing.balancers = [balancer];

                //add rule
                var rules = v2rayConfig.routing.rules.Where(t => t.outboundTag == Global.ProxyTag).ToList();
                if (rules?.Count > 0)
                {
                    foreach (var rule in rules)
                    {
                        rule.outboundTag = null;
                        rule.balancerTag = balancer.tag;
                    }
                }
                else
                {
                    v2rayConfig.routing.rules.Add(new()
                    {
                        network = "tcp,udp",
                        balancerTag = balancer.tag,
                        type = "field"
                    });
                }

                return 0;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
        }

        public int GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds, out V2rayConfig? v2rayConfig, out string msg)
        {
            v2rayConfig = null;
            try
            {
                if (_config == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(Global.V2raySampleClient);
                string txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);
                if (Utils.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
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
                    Logging.SaveLog(ex.Message, ex);
                }

                GenLog(v2rayConfig);
                v2rayConfig.inbounds.Clear(); // Remove "proxy" service for speedtest, avoiding port conflicts.
                v2rayConfig.outbounds.RemoveAt(0);

                int httpPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.speedtest);

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
                    var item = AppHandler.Instance.GetProfileItem(it.indexId);
                    if (it.configType is EConfigType.VMess or EConfigType.VLESS)
                    {
                        if (item is null || Utils.IsNullOrEmpty(item.id) || !Utils.IsGuidByParse(item.id))
                        {
                            continue;
                        }
                    }

                    //find unused port
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
                        protocol = EInboundProtocol.http.ToString(),
                    };
                    inbound.tag = inbound.protocol + inbound.port.ToString();
                    v2rayConfig.inbounds.Add(inbound);

                    //outbound
                    if (item is null)
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.Shadowsocks
                        && !Global.SsSecuritiesInXray.Contains(item.security))
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.VLESS
                     && !Global.Flows.Contains(item.flow))
                    {
                        continue;
                    }
                    if (it.configType is EConfigType.VLESS or EConfigType.Trojan
                        && item.streamSecurity == Global.StreamSecurityReality
                        && item.publicKey.IsNullOrEmpty())
                    {
                        continue;
                    }

                    var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
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
                return 0;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
        }

        #endregion public gen function

        #region private gen function

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
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenInbounds(V2rayConfig v2rayConfig)
        {
            try
            {
                var listen = "0.0.0.0";
                v2rayConfig.inbounds = [];

                Inbounds4Ray? inbound = GetInbound(_config.inbound[0], EInboundProtocol.socks, true);
                v2rayConfig.inbounds.Add(inbound);

                //http
                Inbounds4Ray? inbound2 = GetInbound(_config.inbound[0], EInboundProtocol.http, false);
                v2rayConfig.inbounds.Add(inbound2);

                if (_config.inbound[0].allowLANConn)
                {
                    if (_config.inbound[0].newPort4LAN)
                    {
                        var inbound3 = GetInbound(_config.inbound[0], EInboundProtocol.socks2, true);
                        inbound3.listen = listen;
                        v2rayConfig.inbounds.Add(inbound3);

                        var inbound4 = GetInbound(_config.inbound[0], EInboundProtocol.http2, false);
                        inbound4.listen = listen;
                        v2rayConfig.inbounds.Add(inbound4);

                        //auth
                        if (Utils.IsNotEmpty(_config.inbound[0].user) && Utils.IsNotEmpty(_config.inbound[0].pass))
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts = new List<AccountsItem4Ray> { new AccountsItem4Ray() { user = _config.inbound[0].user, pass = _config.inbound[0].pass } };

                            inbound4.settings.auth = "password";
                            inbound4.settings.accounts = new List<AccountsItem4Ray> { new AccountsItem4Ray() { user = _config.inbound[0].user, pass = _config.inbound[0].pass } };
                        }
                    }
                    else
                    {
                        inbound.listen = listen;
                        inbound2.listen = listen;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private Inbounds4Ray GetInbound(InItem inItem, EInboundProtocol protocol, bool bSocks)
        {
            string result = Utils.GetEmbedText(Global.V2raySampleInbound);
            if (Utils.IsNullOrEmpty(result))
            {
                return new();
            }

            var inbound = JsonUtils.Deserialize<Inbounds4Ray>(result);
            if (inbound == null)
            {
                return new();
            }
            inbound.tag = protocol.ToString();
            inbound.port = inItem.localPort + (int)protocol;
            inbound.protocol = bSocks ? EInboundProtocol.socks.ToString() : EInboundProtocol.http.ToString();
            inbound.settings.udp = inItem.udpEnabled;
            inbound.sniffing.enabled = inItem.sniffingEnabled;
            inbound.sniffing.destOverride = inItem.destOverride;
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
                            if (Utils.IsNotEmpty(routing.domainStrategy))
                            {
                                v2rayConfig.routing.domainStrategy = routing.domainStrategy;
                            }
                            var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.ruleSet);
                            foreach (var item in rules)
                            {
                                if (item.enabled)
                                {
                                    var item2 = JsonUtils.Deserialize<RulesItem4Ray>(JsonUtils.Serialize(item));
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
                            var rules = JsonUtils.Deserialize<List<RulesItem>>(lockedItem.ruleSet);
                            foreach (var item in rules)
                            {
                                var item2 = JsonUtils.Deserialize<RulesItem4Ray>(JsonUtils.Serialize(item));
                                GenRoutingUserRule(item2, v2rayConfig);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenRoutingUserRule(RulesItem4Ray? rule, V2rayConfig v2rayConfig)
        {
            try
            {
                if (rule == null)
                {
                    return 0;
                }
                if (Utils.IsNullOrEmpty(rule.port))
                {
                    rule.port = null;
                }
                if (Utils.IsNullOrEmpty(rule.network))
                {
                    rule.network = null;
                }
                if (rule.domain?.Count == 0)
                {
                    rule.domain = null;
                }
                if (rule.ip?.Count == 0)
                {
                    rule.ip = null;
                }
                if (rule.protocol?.Count == 0)
                {
                    rule.protocol = null;
                }
                if (rule.inboundTag?.Count == 0)
                {
                    rule.inboundTag = null;
                }

                var hasDomainIp = false;
                if (rule.domain?.Count > 0)
                {
                    var it = JsonUtils.DeepCopy(rule);
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
                if (rule.ip?.Count > 0)
                {
                    var it = JsonUtils.DeepCopy(rule);
                    it.domain = null;
                    it.type = "field";
                    v2rayConfig.routing.rules.Add(it);
                    hasDomainIp = true;
                }
                if (!hasDomainIp)
                {
                    if (Utils.IsNotEmpty(rule.port)
                        || rule.protocol?.Count > 0
                        || rule.inboundTag?.Count > 0
                        )
                    {
                        var it = JsonUtils.DeepCopy(rule);
                        it.type = "field";
                        v2rayConfig.routing.rules.Add(it);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenOutbound(ProfileItem node, Outbounds4Ray outbound)
        {
            try
            {
                switch (node.configType)
                {
                    case EConfigType.VMess:
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
                            if (Global.VmessSecurities.Contains(node.security))
                            {
                                usersItem.security = node.security;
                            }
                            else
                            {
                                usersItem.security = Global.DefaultSecurity;
                            }

                            GenOutboundMux(node, outbound, _config.coreBasicItem.muxEnabled);

                            outbound.settings.servers = null;
                            break;
                        }
                    case EConfigType.Shadowsocks:
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
                            serversItem.method = AppHandler.Instance.GetShadowsocksSecurities(node).Contains(node.security) ? node.security : "none";

                            serversItem.ota = false;
                            serversItem.level = 1;

                            GenOutboundMux(node, outbound, false);

                            outbound.settings.vnext = null;
                            break;
                        }
                    case EConfigType.SOCKS:
                    case EConfigType.HTTP:
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

                            if (Utils.IsNotEmpty(node.security)
                                && Utils.IsNotEmpty(node.id))
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

                            outbound.settings.vnext = null;
                            break;
                        }
                    case EConfigType.VLESS:
                        {
                            VnextItem4Ray vnextItem;
                            if (outbound.settings.vnext?.Count <= 0)
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
                                if (Utils.IsNotEmpty(node.flow))
                                {
                                    usersItem.flow = node.flow;

                                    GenOutboundMux(node, outbound, false);
                                }
                            }
                            if (node.streamSecurity == Global.StreamSecurityReality && Utils.IsNullOrEmpty(node.flow))
                            {
                                GenOutboundMux(node, outbound, _config.coreBasicItem.muxEnabled);
                            }

                            outbound.settings.servers = null;
                            break;
                        }
                    case EConfigType.Trojan:
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

                            outbound.settings.vnext = null;
                            break;
                        }
                }

                outbound.protocol = Global.ProtocolTypes[node.configType];
                GenBoundStreamSettings(node, outbound.streamSettings);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
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
                    outbound.mux.concurrency = _config.mux4RayItem.concurrency;
                    outbound.mux.xudpConcurrency = _config.mux4RayItem.xudpConcurrency;
                    outbound.mux.xudpProxyUDP443 = _config.mux4RayItem.xudpProxyUDP443;
                }
                else
                {
                    outbound.mux.enabled = false;
                    outbound.mux.concurrency = -1;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
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
                        useragent = Global.UserAgentTexts[_config.coreBasicItem.defUserAgent];
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
                    if (Utils.IsNotEmpty(sni))
                    {
                        tlsSettings.serverName = sni;
                    }
                    else if (Utils.IsNotEmpty(host))
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
                        show = false,
                    };

                    streamSettings.realitySettings = realitySettings;
                }

                //streamSettings
                switch (node.GetNetwork())
                {
                    case nameof(ETransport.kcp):
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
                        if (Utils.IsNotEmpty(node.path))
                        {
                            kcpSettings.seed = node.path;
                        }
                        streamSettings.kcpSettings = kcpSettings;
                        break;
                    //ws
                    case nameof(ETransport.ws):
                        WsSettings4Ray wsSettings = new();
                        wsSettings.headers = new Headers4Ray();
                        string path = node.path;
                        if (Utils.IsNotEmpty(host))
                        {
                            wsSettings.headers.Host = host;
                        }
                        if (Utils.IsNotEmpty(path))
                        {
                            wsSettings.path = path;
                        }
                        if (Utils.IsNotEmpty(useragent))
                        {
                            wsSettings.headers.UserAgent = useragent;
                        }
                        streamSettings.wsSettings = wsSettings;

                        break;
                    //httpupgrade
                    case nameof(ETransport.httpupgrade):
                        HttpupgradeSettings4Ray httpupgradeSettings = new();

                        if (Utils.IsNotEmpty(node.path))
                        {
                            httpupgradeSettings.path = node.path;
                        }
                        if (Utils.IsNotEmpty(host))
                        {
                            httpupgradeSettings.host = host;
                        }
                        streamSettings.httpupgradeSettings = httpupgradeSettings;

                        break;
                    //splithttp
                    case nameof(ETransport.splithttp):
                        SplithttpSettings4Ray splithttpSettings = new()
                        {
                            maxUploadSize = 1000000,
                            maxConcurrentUploads = 10
                        };

                        if (Utils.IsNotEmpty(node.path))
                        {
                            splithttpSettings.path = node.path;
                        }
                        if (Utils.IsNotEmpty(host))
                        {
                            splithttpSettings.host = host;
                        }
                        streamSettings.splithttpSettings = splithttpSettings;

                        break;
                    //h2
                    case nameof(ETransport.h2):
                        HttpSettings4Ray httpSettings = new();

                        if (Utils.IsNotEmpty(host))
                        {
                            httpSettings.host = Utils.String2List(host);
                        }
                        httpSettings.path = node.path;

                        streamSettings.httpSettings = httpSettings;

                        break;
                    //quic
                    case nameof(ETransport.quic):
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
                            if (Utils.IsNotEmpty(sni))
                            {
                                streamSettings.tlsSettings.serverName = sni;
                            }
                            else
                            {
                                streamSettings.tlsSettings.serverName = node.address;
                            }
                        }
                        break;

                    case nameof(ETransport.grpc):
                        GrpcSettings4Ray grpcSettings = new()
                        {
                            authority = Utils.IsNullOrEmpty(host) ? null : host,
                            serviceName = node.path,
                            multiMode = node.headerType == Global.GrpcMultiMode,
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
                            string request = Utils.GetEmbedText(Global.V2raySampleHttpRequestFileName);
                            string[] arrHost = host.Split(',');
                            string host2 = string.Join("\",\"", arrHost);
                            request = request.Replace("$requestHost$", $"\"{host2}\"");
                            //request = request.Replace("$requestHost$", string.Format("\"{0}\"", config.requestHost()));
                            request = request.Replace("$requestUserAgent$", $"\"{useragent}\"");
                            //Path
                            string pathHttp = @"/";
                            if (Utils.IsNotEmpty(node.path))
                            {
                                string[] arrPath = node.path.Split(',');
                                pathHttp = string.Join("\",\"", arrPath);
                            }
                            request = request.Replace("$requestPath$", $"\"{pathHttp}\"");
                            tcpSettings.header.request = JsonUtils.Deserialize<object>(request);

                            streamSettings.tcpSettings = tcpSettings;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenDns(ProfileItem? node, V2rayConfig v2rayConfig)
        {
            try
            {
                var item = AppHandler.Instance.GetDNSItem(ECoreType.Xray);
                var normalDNS = item?.normalDNS;
                var domainStrategy4Freedom = item?.domainStrategy4Freedom;
                if (Utils.IsNullOrEmpty(normalDNS))
                {
                    normalDNS = Utils.GetEmbedText(Global.DNSV2rayNormalFileName);
                }

                //Outbound Freedom domainStrategy
                if (Utils.IsNotEmpty(domainStrategy4Freedom))
                {
                    var outbound = v2rayConfig.outbounds[1];
                    outbound.settings.domainStrategy = domainStrategy4Freedom;
                    outbound.settings.userLevel = 0;
                }

                var obj = JsonUtils.ParseJson(normalDNS);
                if (obj is null)
                {
                    List<string> servers = [];
                    string[] arrDNS = normalDNS.Split(',');
                    foreach (string str in arrDNS)
                    {
                        servers.Add(str);
                    }
                    obj = JsonUtils.ParseJson("{}");
                    obj["servers"] = JsonUtils.SerializeToNode(servers);
                }

                // 追加至 dns 设置
                if (item.useSystemHosts)
                {
                    var systemHosts = Utils.GetSystemHosts();
                    if (systemHosts.Count > 0)
                    {
                        var normalHost = obj["hosts"];
                        if (normalHost != null)
                        {
                            foreach (var host in systemHosts)
                            {
                                if (normalHost[host.Key] != null)
                                    continue;
                                normalHost[host.Key] = host.Value;
                            }
                        }
                    }
                }

                GenDnsDomains(node, obj, item);

                v2rayConfig.dns = obj;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenDnsDomains(ProfileItem? node, JsonNode dns, DNSItem? dNSItem)
        {
            if (node == null)
            { return 0; }
            var servers = dns["servers"];
            if (servers != null)
            {
                if (Utils.IsDomain(node.address))
                {
                    var dnsServer = new DnsServer4Ray()
                    {
                        address = Utils.IsNullOrEmpty(dNSItem?.domainDNSAddress) ? Global.DomainDNSAddress.FirstOrDefault() : dNSItem?.domainDNSAddress,
                        domains = [node.address]
                    };
                    servers.AsArray().Add(JsonUtils.SerializeToNode(dnsServer));
                }
            }
            return 0;
        }

        private int GenStatistic(V2rayConfig v2rayConfig)
        {
            if (_config.guiItem.enableStatistics)
            {
                string tag = EInboundProtocol.api.ToString();
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
                    apiInbound.port = AppHandler.Instance.StatePort;
                    apiInbound.protocol = Global.InboundAPIProtocol;
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
            //fragment proxy
            if (_config.coreBasicItem.enableFragment
                && Utils.IsNotEmpty(v2rayConfig.outbounds[0].streamSettings?.security))
            {
                var fragmentOutbound = new Outbounds4Ray
                {
                    protocol = "freedom",
                    tag = $"{Global.ProxyTag}3",
                    settings = new()
                    {
                        fragment = new()
                        {
                            packets = "tlshello",
                            length = "100-200",
                            interval = "10-20"
                        }
                    }
                };

                v2rayConfig.outbounds.Add(fragmentOutbound);
                v2rayConfig.outbounds[0].streamSettings.sockopt = new()
                {
                    dialerProxy = fragmentOutbound.tag
                };
                return 0;
            }

            if (node.subid.IsNullOrEmpty())
            {
                return 0;
            }
            try
            {
                var subItem = AppHandler.Instance.GetSubItem(node.subid);
                if (subItem is null)
                {
                    return 0;
                }

                //current proxy
                var outbound = v2rayConfig.outbounds[0];
                var txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);

                //Previous proxy
                var prevNode = AppHandler.Instance.GetProfileItemViaRemarks(subItem.prevProfile);
                if (prevNode is not null
                    && prevNode.configType != EConfigType.Custom
                    && prevNode.configType != EConfigType.Hysteria2
                    && prevNode.configType != EConfigType.TUIC
                    && prevNode.configType != EConfigType.WireGuard)
                {
                    var prevOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    GenOutbound(prevNode, prevOutbound);
                    prevOutbound.tag = $"{Global.ProxyTag}2";
                    v2rayConfig.outbounds.Add(prevOutbound);

                    outbound.streamSettings.sockopt = new()
                    {
                        dialerProxy = prevOutbound.tag
                    };
                }

                //Next proxy
                var nextNode = AppHandler.Instance.GetProfileItemViaRemarks(subItem.nextProfile);
                if (nextNode is not null
                    && nextNode.configType != EConfigType.Custom
                    && nextNode.configType != EConfigType.Hysteria2
                    && nextNode.configType != EConfigType.TUIC
                    && nextNode.configType != EConfigType.WireGuard)
                {
                    var nextOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
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
                Logging.SaveLog(ex.Message, ex);
            }

            return 0;
        }

        #endregion private gen function
    }
}