﻿using System.Net;
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

        public async Task<RetResult> GenerateClientConfigContent(ProfileItem node)
        {
            var ret = new RetResult();
            try
            {
                if (node == null
                    || node.Port <= 0)
                {
                    ret.Msg = ResUI.CheckServerSettings;
                    return ret;
                }

                if (node.GetNetwork() is nameof(ETransport.quic))
                {
                    ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
                    return ret;
                }

                ret.Msg = ResUI.InitialConfiguration;

                var result = Utils.GetEmbedText(Global.V2raySampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    ret.Msg = ResUI.FailedGetDefaultConfiguration;
                    return ret;
                }

                var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    ret.Msg = ResUI.FailedGenDefaultConfiguration;
                    return ret;
                }

                await GenLog(v2rayConfig);

                await GenInbounds(v2rayConfig);

                await GenRouting(v2rayConfig);

                await GenOutbound(node, v2rayConfig.outbounds.First());

                await GenMoreOutbounds(node, v2rayConfig);

                await GenDns(node, v2rayConfig);

                await GenStatistic(v2rayConfig);

                ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
                ret.Success = true;
                ret.Data = JsonUtils.Serialize(v2rayConfig);
                return ret;
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GenerateClientConfig4V2ray", ex);
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
        }

        public async Task<RetResult> GenerateClientMultipleLoadConfig(List<ProfileItem> selecteds)
        {
            var ret = new RetResult();

            try
            {
                if (_config == null)
                {
                    ret.Msg = ResUI.CheckServerSettings;
                    return ret;
                }

                ret.Msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(Global.V2raySampleClient);
                string txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);
                if (Utils.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    ret.Msg = ResUI.FailedGetDefaultConfiguration;
                    return ret;
                }

                var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    ret.Msg = ResUI.FailedGenDefaultConfiguration;
                    return ret;
                }

                await GenLog(v2rayConfig);
                await GenInbounds(v2rayConfig);
                await GenRouting(v2rayConfig);
                await GenDns(null, v2rayConfig);
                await GenStatistic(v2rayConfig);
                v2rayConfig.outbounds.RemoveAt(0);

                var tagProxy = new List<string>();
                foreach (var it in selecteds)
                {
                    if (it.ConfigType == EConfigType.Custom)
                    {
                        continue;
                    }
                    if (it.ConfigType is EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.WireGuard)
                    {
                        continue;
                    }
                    if (it.Port <= 0)
                    {
                        continue;
                    }
                    var item = await AppHandler.Instance.GetProfileItem(it.IndexId);
                    if (item is null)
                    {
                        continue;
                    }
                    if (it.ConfigType is EConfigType.VMess or EConfigType.VLESS)
                    {
                        if (Utils.IsNullOrEmpty(item.Id) || !Utils.IsGuidByParse(item.Id))
                        {
                            continue;
                        }
                    }
                    if (item.ConfigType == EConfigType.Shadowsocks
                      && !Global.SsSecuritiesInSingbox.Contains(item.Security))
                    {
                        continue;
                    }
                    if (item.ConfigType == EConfigType.VLESS && !Global.Flows.Contains(item.Flow))
                    {
                        continue;
                    }

                    //outbound
                    var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    await GenOutbound(item, outbound);
                    outbound.tag = $"{Global.ProxyTag}-{tagProxy.Count + 1}";
                    v2rayConfig.outbounds.Add(outbound);
                    tagProxy.Add(outbound.tag);
                }
                if (tagProxy.Count <= 0)
                {
                    ret.Msg = ResUI.FailedGenDefaultConfiguration;
                    return ret;
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

                ret.Success = true;
                ret.Data = JsonUtils.Serialize(v2rayConfig);
                return ret;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
        }

        public async Task<RetResult> GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds)
        {
            var ret = new RetResult();
            try
            {
                if (_config == null)
                {
                    ret.Msg = ResUI.CheckServerSettings;
                    return ret;
                }

                ret.Msg = ResUI.InitialConfiguration;

                var result = Utils.GetEmbedText(Global.V2raySampleClient);
                var txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);
                if (Utils.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    ret.Msg = ResUI.FailedGetDefaultConfiguration;
                    return ret;
                }

                var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
                if (v2rayConfig == null)
                {
                    ret.Msg = ResUI.FailedGenDefaultConfiguration;
                    return ret;
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

                await GenLog(v2rayConfig);
                v2rayConfig.inbounds.Clear();
                v2rayConfig.outbounds.Clear();
                v2rayConfig.routing.rules.Clear();

                var httpPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.speedtest);

                foreach (var it in selecteds)
                {
                    if (it.ConfigType == EConfigType.Custom)
                    {
                        continue;
                    }
                    if (it.Port <= 0)
                    {
                        continue;
                    }
                    var item = await AppHandler.Instance.GetProfileItem(it.IndexId);
                    if (it.ConfigType is EConfigType.VMess or EConfigType.VLESS)
                    {
                        if (item is null || Utils.IsNullOrEmpty(item.Id) || !Utils.IsGuidByParse(item.Id))
                        {
                            continue;
                        }
                    }

                    //find unused port
                    var port = httpPort;
                    for (var k = httpPort; k < Global.MaxPort; k++)
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
                    it.Port = port;
                    it.AllowTest = true;

                    //outbound
                    if (item is null)
                    {
                        continue;
                    }
                    if (item.ConfigType == EConfigType.Shadowsocks
                        && !Global.SsSecuritiesInXray.Contains(item.Security))
                    {
                        continue;
                    }
                    if (item.ConfigType == EConfigType.VLESS
                     && !Global.Flows.Contains(item.Flow))
                    {
                        continue;
                    }
                    if (it.ConfigType is EConfigType.VLESS or EConfigType.Trojan
                        && item.StreamSecurity == Global.StreamSecurityReality
                        && item.PublicKey.IsNullOrEmpty())
                    {
                        continue;
                    }

                    //inbound
                    Inbounds4Ray inbound = new()
                    {
                        listen = Global.Loopback,
                        port = port,
                        protocol = EInboundProtocol.http.ToString(),
                    };
                    inbound.tag = inbound.protocol + inbound.port.ToString();
                    v2rayConfig.inbounds.Add(inbound);

                    var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    await GenOutbound(item, outbound);
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

                //ret.Msg =string.Format(ResUI.SuccessfulConfiguration"), node.getSummary());
                ret.Success = true;
                ret.Data = JsonUtils.Serialize(v2rayConfig);
                return ret;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
        }

        #endregion public gen function

        #region private gen function

        public async Task<int> GenLog(V2rayConfig v2rayConfig)
        {
            try
            {
                if (_config.CoreBasicItem.LogEnabled)
                {
                    var dtNow = DateTime.Now;
                    v2rayConfig.log.loglevel = _config.CoreBasicItem.Loglevel;
                    v2rayConfig.log.access = Utils.GetLogPath($"Vaccess_{dtNow:yyyy-MM-dd}.txt");
                    v2rayConfig.log.error = Utils.GetLogPath($"Verror_{dtNow:yyyy-MM-dd}.txt");
                }
                else
                {
                    v2rayConfig.log.loglevel = _config.CoreBasicItem.Loglevel;
                    v2rayConfig.log.access = null;
                    v2rayConfig.log.error = null;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        public async Task<int> GenInbounds(V2rayConfig v2rayConfig)
        {
            try
            {
                var listen = "0.0.0.0";
                v2rayConfig.inbounds = [];

                Inbounds4Ray? inbound = GetInbound(_config.Inbound.First(), EInboundProtocol.socks, true);
                v2rayConfig.inbounds.Add(inbound);

                //http
                Inbounds4Ray? inbound2 = GetInbound(_config.Inbound.First(), EInboundProtocol.http, false);
                v2rayConfig.inbounds.Add(inbound2);

                if (_config.Inbound.First().AllowLANConn)
                {
                    if (_config.Inbound.First().NewPort4LAN)
                    {
                        var inbound3 = GetInbound(_config.Inbound.First(), EInboundProtocol.socks2, true);
                        inbound3.listen = listen;
                        v2rayConfig.inbounds.Add(inbound3);

                        var inbound4 = GetInbound(_config.Inbound.First(), EInboundProtocol.http2, false);
                        inbound4.listen = listen;
                        v2rayConfig.inbounds.Add(inbound4);

                        //auth
                        if (Utils.IsNotEmpty(_config.Inbound.First().User) && Utils.IsNotEmpty(_config.Inbound.First().Pass))
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts = new List<AccountsItem4Ray> { new AccountsItem4Ray() { user = _config.Inbound.First().User, pass = _config.Inbound.First().Pass } };

                            inbound4.settings.auth = "password";
                            inbound4.settings.accounts = new List<AccountsItem4Ray> { new AccountsItem4Ray() { user = _config.Inbound.First().User, pass = _config.Inbound.First().Pass } };
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
            inbound.port = inItem.LocalPort + (int)protocol;
            inbound.protocol = bSocks ? EInboundProtocol.socks.ToString() : EInboundProtocol.http.ToString();
            inbound.settings.udp = inItem.UdpEnabled;
            inbound.sniffing.enabled = inItem.SniffingEnabled;
            inbound.sniffing.destOverride = inItem.DestOverride;
            inbound.sniffing.routeOnly = inItem.RouteOnly;

            return inbound;
        }

        private async Task<int> GenRouting(V2rayConfig v2rayConfig)
        {
            try
            {
                if (v2rayConfig.routing?.rules != null)
                {
                    v2rayConfig.routing.domainStrategy = _config.RoutingBasicItem.DomainStrategy;
                    v2rayConfig.routing.domainMatcher = Utils.IsNullOrEmpty(_config.RoutingBasicItem.DomainMatcher) ? null : _config.RoutingBasicItem.DomainMatcher;

                    var routing = await ConfigHandler.GetDefaultRouting(_config);
                    if (routing != null)
                    {
                        if (Utils.IsNotEmpty(routing.DomainStrategy))
                        {
                            v2rayConfig.routing.domainStrategy = routing.DomainStrategy;
                        }
                        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
                        foreach (var item in rules)
                        {
                            if (item.Enabled)
                            {
                                var item2 = JsonUtils.Deserialize<RulesItem4Ray>(JsonUtils.Serialize(item));
                                await GenRoutingUserRule(item2, v2rayConfig);
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

        public async Task<int> GenRoutingUserRule(RulesItem4Ray? rule, V2rayConfig v2rayConfig)
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

        public async Task<int> GenOutbound(ProfileItem node, Outbounds4Ray outbound)
        {
            try
            {
                switch (node.ConfigType)
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
                                vnextItem = outbound.settings.vnext.First();
                            }
                            vnextItem.address = node.Address;
                            vnextItem.port = node.Port;

                            UsersItem4Ray usersItem;
                            if (vnextItem.users.Count <= 0)
                            {
                                usersItem = new UsersItem4Ray();
                                vnextItem.users.Add(usersItem);
                            }
                            else
                            {
                                usersItem = vnextItem.users.First();
                            }
                            //远程服务器用户ID
                            usersItem.id = node.Id;
                            usersItem.alterId = node.AlterId;
                            usersItem.email = Global.UserEMail;
                            if (Global.VmessSecurities.Contains(node.Security))
                            {
                                usersItem.security = node.Security;
                            }
                            else
                            {
                                usersItem.security = Global.DefaultSecurity;
                            }

                            await GenOutboundMux(node, outbound, _config.CoreBasicItem.MuxEnabled);

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
                                serversItem = outbound.settings.servers.First();
                            }
                            serversItem.address = node.Address;
                            serversItem.port = node.Port;
                            serversItem.password = node.Id;
                            serversItem.method = AppHandler.Instance.GetShadowsocksSecurities(node).Contains(node.Security) ? node.Security : "none";

                            serversItem.ota = false;
                            serversItem.level = 1;

                            await GenOutboundMux(node, outbound, false);

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
                                serversItem = outbound.settings.servers.First();
                            }
                            serversItem.address = node.Address;
                            serversItem.port = node.Port;
                            serversItem.method = null;
                            serversItem.password = null;

                            if (Utils.IsNotEmpty(node.Security)
                                && Utils.IsNotEmpty(node.Id))
                            {
                                SocksUsersItem4Ray socksUsersItem = new()
                                {
                                    user = node.Security,
                                    pass = node.Id,
                                    level = 1
                                };

                                serversItem.users = new List<SocksUsersItem4Ray>() { socksUsersItem };
                            }

                            await GenOutboundMux(node, outbound, false);

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
                                vnextItem = outbound.settings.vnext.First();
                            }
                            vnextItem.address = node.Address;
                            vnextItem.port = node.Port;

                            UsersItem4Ray usersItem;
                            if (vnextItem.users.Count <= 0)
                            {
                                usersItem = new UsersItem4Ray();
                                vnextItem.users.Add(usersItem);
                            }
                            else
                            {
                                usersItem = vnextItem.users.First();
                            }
                            usersItem.id = node.Id;
                            usersItem.email = Global.UserEMail;
                            usersItem.encryption = node.Security;

                            await GenOutboundMux(node, outbound, _config.CoreBasicItem.MuxEnabled);

                            if (node.StreamSecurity == Global.StreamSecurityReality
                                || node.StreamSecurity == Global.StreamSecurity)
                            {
                                if (Utils.IsNotEmpty(node.Flow))
                                {
                                    usersItem.flow = node.Flow;

                                    await GenOutboundMux(node, outbound, false);
                                }
                            }
                            if (node.StreamSecurity == Global.StreamSecurityReality && Utils.IsNullOrEmpty(node.Flow))
                            {
                                await GenOutboundMux(node, outbound, _config.CoreBasicItem.MuxEnabled);
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
                                serversItem = outbound.settings.servers.First();
                            }
                            serversItem.address = node.Address;
                            serversItem.port = node.Port;
                            serversItem.password = node.Id;

                            serversItem.ota = false;
                            serversItem.level = 1;

                            await GenOutboundMux(node, outbound, false);

                            outbound.settings.vnext = null;
                            break;
                        }
                }

                outbound.protocol = Global.ProtocolTypes[node.ConfigType];
                await GenBoundStreamSettings(node, outbound.streamSettings);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        public async Task<int> GenOutboundMux(ProfileItem node, Outbounds4Ray outbound, bool enabled)
        {
            try
            {
                if (enabled)
                {
                    outbound.mux.enabled = true;
                    outbound.mux.concurrency = _config.Mux4RayItem.Concurrency;
                    outbound.mux.xudpConcurrency = _config.Mux4RayItem.XudpConcurrency;
                    outbound.mux.xudpProxyUDP443 = _config.Mux4RayItem.XudpProxyUDP443;
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

        public async Task<int> GenBoundStreamSettings(ProfileItem node, StreamSettings4Ray streamSettings)
        {
            try
            {
                streamSettings.network = node.GetNetwork();
                string host = node.RequestHost.TrimEx();
                string sni = node.Sni;
                string useragent = "";
                if (!_config.CoreBasicItem.DefUserAgent.IsNullOrEmpty())
                {
                    try
                    {
                        useragent = Global.UserAgentTexts[_config.CoreBasicItem.DefUserAgent];
                    }
                    catch (KeyNotFoundException)
                    {
                        useragent = _config.CoreBasicItem.DefUserAgent;
                    }
                }

                //if tls
                if (node.StreamSecurity == Global.StreamSecurity)
                {
                    streamSettings.security = node.StreamSecurity;

                    TlsSettings4Ray tlsSettings = new()
                    {
                        allowInsecure = Utils.ToBool(node.AllowInsecure.IsNullOrEmpty() ? _config.CoreBasicItem.DefAllowInsecure.ToString().ToLower() : node.AllowInsecure),
                        alpn = node.GetAlpn(),
                        fingerprint = node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : node.Fingerprint
                    };
                    if (Utils.IsNotEmpty(sni))
                    {
                        tlsSettings.serverName = sni;
                    }
                    else if (Utils.IsNotEmpty(host))
                    {
                        tlsSettings.serverName = Utils.String2List(host)?.First();
                    }
                    streamSettings.tlsSettings = tlsSettings;
                }

                //if Reality
                if (node.StreamSecurity == Global.StreamSecurityReality)
                {
                    streamSettings.security = node.StreamSecurity;

                    TlsSettings4Ray realitySettings = new()
                    {
                        fingerprint = node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : node.Fingerprint,
                        serverName = sni,
                        publicKey = node.PublicKey,
                        shortId = node.ShortId,
                        spiderX = node.SpiderX,
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
                            mtu = _config.KcpItem.Mtu,
                            tti = _config.KcpItem.Tti
                        };

                        kcpSettings.uplinkCapacity = _config.KcpItem.UplinkCapacity;
                        kcpSettings.downlinkCapacity = _config.KcpItem.DownlinkCapacity;

                        kcpSettings.congestion = _config.KcpItem.Congestion;
                        kcpSettings.readBufferSize = _config.KcpItem.ReadBufferSize;
                        kcpSettings.writeBufferSize = _config.KcpItem.WriteBufferSize;
                        kcpSettings.header = new Header4Ray
                        {
                            type = node.HeaderType
                        };
                        if (Utils.IsNotEmpty(node.Path))
                        {
                            kcpSettings.seed = node.Path;
                        }
                        streamSettings.kcpSettings = kcpSettings;
                        break;
                    //ws
                    case nameof(ETransport.ws):
                        WsSettings4Ray wsSettings = new();
                        wsSettings.headers = new Headers4Ray();
                        string path = node.Path;
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

                        if (Utils.IsNotEmpty(node.Path))
                        {
                            httpupgradeSettings.path = node.Path;
                        }
                        if (Utils.IsNotEmpty(host))
                        {
                            httpupgradeSettings.host = host;
                        }
                        streamSettings.httpupgradeSettings = httpupgradeSettings;

                        break;
                    //xhttp
                    case nameof(ETransport.xhttp):
                        streamSettings.network = ETransport.xhttp.ToString();
                        XhttpSettings4Ray xhttpSettings = new()
                        {
                            scMaxEachPostBytes = "500000-1000000",
                            scMaxConcurrentPosts = "50-100",
                            scMinPostsIntervalMs = "30-50"
                        };

                        if (Utils.IsNotEmpty(node.Path))
                        {
                            xhttpSettings.path = node.Path;
                        }
                        if (Utils.IsNotEmpty(host))
                        {
                            xhttpSettings.host = host;
                        }
                        if (Utils.IsNotEmpty(node.HeaderType) && Global.XhttpMode.Contains(node.HeaderType))
                        {
                            xhttpSettings.mode = node.HeaderType;
                        }
                        if (Utils.IsNotEmpty(node.Extra))
                        {
                            xhttpSettings.extra = JsonUtils.ParseJson(node.Extra);
                        }

                        streamSettings.xhttpSettings = xhttpSettings;

                        break;
                    //h2
                    case nameof(ETransport.h2):
                        HttpSettings4Ray httpSettings = new();

                        if (Utils.IsNotEmpty(host))
                        {
                            httpSettings.host = Utils.String2List(host);
                        }
                        httpSettings.path = node.Path;

                        streamSettings.httpSettings = httpSettings;

                        break;
                    //quic
                    case nameof(ETransport.quic):
                        QuicSettings4Ray quicsettings = new()
                        {
                            security = host,
                            key = node.Path,
                            header = new Header4Ray
                            {
                                type = node.HeaderType
                            }
                        };
                        streamSettings.quicSettings = quicsettings;
                        if (node.StreamSecurity == Global.StreamSecurity)
                        {
                            if (Utils.IsNotEmpty(sni))
                            {
                                streamSettings.tlsSettings.serverName = sni;
                            }
                            else
                            {
                                streamSettings.tlsSettings.serverName = node.Address;
                            }
                        }
                        break;

                    case nameof(ETransport.grpc):
                        GrpcSettings4Ray grpcSettings = new()
                        {
                            authority = Utils.IsNullOrEmpty(host) ? null : host,
                            serviceName = node.Path,
                            multiMode = node.HeaderType == Global.GrpcMultiMode,
                            idle_timeout = _config.GrpcItem.IdleTimeout,
                            health_check_timeout = _config.GrpcItem.HealthCheckTimeout,
                            permit_without_stream = _config.GrpcItem.PermitWithoutStream,
                            initial_windows_size = _config.GrpcItem.InitialWindowsSize,
                        };
                        streamSettings.grpcSettings = grpcSettings;
                        break;

                    default:
                        //tcp
                        if (node.HeaderType == Global.TcpHeaderHttp)
                        {
                            TcpSettings4Ray tcpSettings = new()
                            {
                                header = new Header4Ray
                                {
                                    type = node.HeaderType
                                }
                            };

                            //request Host
                            string request = Utils.GetEmbedText(Global.V2raySampleHttpRequestFileName);
                            string[] arrHost = host.Split(',');
                            string host2 = string.Join(",".AppendQuotes(), arrHost);
                            request = request.Replace("$requestHost$", $"{host2.AppendQuotes()}");
                            request = request.Replace("$requestUserAgent$", $"{useragent.AppendQuotes()}");
                            //Path
                            string pathHttp = @"/";
                            if (Utils.IsNotEmpty(node.Path))
                            {
                                string[] arrPath = node.Path.Split(',');
                                pathHttp = string.Join(",".AppendQuotes(), arrPath);
                            }
                            request = request.Replace("$requestPath$", $"{pathHttp.AppendQuotes()}");
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

        public async Task<int> GenDns(ProfileItem? node, V2rayConfig v2rayConfig)
        {
            try
            {
                var item = await AppHandler.Instance.GetDNSItem(ECoreType.Xray);
                var normalDNS = item?.NormalDNS;
                var domainStrategy4Freedom = item?.DomainStrategy4Freedom;
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
                if (item.UseSystemHosts)
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

                await GenDnsDomains(node, obj, item);

                v2rayConfig.dns = obj;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        public async Task<int> GenDnsDomains(ProfileItem? node, JsonNode dns, DNSItem? dNSItem)
        {
            if (node == null)
            { return 0; }
            var servers = dns["servers"];
            if (servers != null)
            {
                if (Utils.IsDomain(node.Address))
                {
                    var dnsServer = new DnsServer4Ray()
                    {
                        address = Utils.IsNullOrEmpty(dNSItem?.DomainDNSAddress) ? Global.DomainDNSAddress.FirstOrDefault() : dNSItem?.DomainDNSAddress,
                        domains = [node.Address]
                    };
                    servers.AsArray().Add(JsonUtils.SerializeToNode(dnsServer));
                }
            }
            return 0;
        }

        public async Task<int> GenStatistic(V2rayConfig v2rayConfig)
        {
            if (_config.GuiItem.EnableStatistics)
            {
                string tag = EInboundProtocol.api.ToString();
                Metrics4Ray apiObj = new();
                Policy4Ray policyObj = new();
                SystemPolicy4Ray policySystemSetting = new();

                v2rayConfig.stats = new Stats4Ray();

                apiObj.tag = tag;
                v2rayConfig.metrics = apiObj;

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

        private async Task<int> GenMoreOutbounds(ProfileItem node, V2rayConfig v2rayConfig)
        {
            //fragment proxy
            if (_config.CoreBasicItem.EnableFragment
                && Utils.IsNotEmpty(v2rayConfig.outbounds.First().streamSettings?.security))
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
                v2rayConfig.outbounds.First().streamSettings.sockopt = new()
                {
                    dialerProxy = fragmentOutbound.tag
                };
                return 0;
            }

            if (node.Subid.IsNullOrEmpty())
            {
                return 0;
            }
            try
            {
                var subItem = await AppHandler.Instance.GetSubItem(node.Subid);
                if (subItem is null)
                {
                    return 0;
                }

                //current proxy
                var outbound = v2rayConfig.outbounds.First();
                var txtOutbound = Utils.GetEmbedText(Global.V2raySampleOutbound);

                //Previous proxy
                var prevNode = await AppHandler.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
                if (prevNode is not null
                    && prevNode.ConfigType != EConfigType.Custom
                    && prevNode.ConfigType != EConfigType.Hysteria2
                    && prevNode.ConfigType != EConfigType.TUIC
                    && prevNode.ConfigType != EConfigType.WireGuard)
                {
                    var prevOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    await GenOutbound(prevNode, prevOutbound);
                    prevOutbound.tag = $"{Global.ProxyTag}2";
                    v2rayConfig.outbounds.Add(prevOutbound);

                    outbound.streamSettings.sockopt = new()
                    {
                        dialerProxy = prevOutbound.tag
                    };
                }

                //Next proxy
                var nextNode = await AppHandler.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
                if (nextNode is not null
                    && nextNode.ConfigType != EConfigType.Custom
                    && nextNode.ConfigType != EConfigType.Hysteria2
                    && nextNode.ConfigType != EConfigType.TUIC
                    && nextNode.ConfigType != EConfigType.WireGuard)
                {
                    var nextOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    await GenOutbound(nextNode, nextOutbound);
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