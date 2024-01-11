using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    internal class CoreConfigSingbox
    {
        private Config _config;

        public CoreConfigSingbox(Config config)
        {
            _config = config;
        }

        public int GenerateClientConfigContent(ProfileItem node, out SingboxConfig? singboxConfig, out string msg)
        {
            singboxConfig = null;
            try
            {
                if (node == null
                    || node.port <= 0)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(Global.SingboxSampleClient);
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
                if (singboxConfig == null)
                {
                    msg = ResUI.FailedGenDefaultConfiguration;
                    return -1;
                }

                GenLog(singboxConfig);

                GenInbounds(singboxConfig);

                GenOutbound(node, singboxConfig.outbounds[0]);

                GenMoreOutbounds(node, singboxConfig);

                GenRouting(singboxConfig);

                GenDns(node, singboxConfig);

                GenStatistic(singboxConfig);

                msg = string.Format(ResUI.SuccessfulConfiguration, "");
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GenerateClientConfig4Singbox", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
        }

        #region private gen function

        private int GenLog(SingboxConfig singboxConfig)
        {
            try
            {
                switch (_config.coreBasicItem.loglevel)
                {
                    case "debug":
                    case "info":
                    case "error":
                        singboxConfig.log.level = _config.coreBasicItem.loglevel;
                        break;

                    case "warning":
                        singboxConfig.log.level = "warn";
                        break;

                    default:
                        break;
                }
                if (_config.coreBasicItem.loglevel == "none")
                {
                    singboxConfig.log.disabled = true;
                }
                if (_config.coreBasicItem.logEnabled)
                {
                    var dtNow = DateTime.Now;
                    singboxConfig.log.output = Utils.GetLogPath($"sbox_{dtNow:yyyy-MM-dd}.txt");
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenInbounds(SingboxConfig singboxConfig)
        {
            try
            {
                singboxConfig.inbounds.Clear();

                if (!_config.tunModeItem.enableTun || (_config.tunModeItem.enableTun && _config.tunModeItem.enableExInbound))
                {
                    var inbound = new Inbound4Sbox()
                    {
                        type = Global.InboundSocks,
                        tag = Global.InboundSocks,
                        listen = Global.Loopback,
                    };
                    singboxConfig.inbounds.Add(inbound);

                    inbound.listen_port = LazyConfig.Instance.GetLocalPort(Global.InboundSocks);
                    inbound.sniff = _config.inbound[0].sniffingEnabled;
                    inbound.sniff_override_destination = _config.inbound[0].routeOnly ? false : _config.inbound[0].sniffingEnabled;
                    inbound.domain_strategy = Utils.IsNullOrEmpty(_config.routingBasicItem.domainStrategy4Singbox) ? null : _config.routingBasicItem.domainStrategy4Singbox;

                    if (_config.routingBasicItem.enableRoutingAdvanced)
                    {
                        var routing = ConfigHandler.GetDefaultRouting(_config);
                        if (!Utils.IsNullOrEmpty(routing.domainStrategy4Singbox))
                        {
                            inbound.domain_strategy = routing.domainStrategy4Singbox;
                        }
                    }

                    //http
                    var inbound2 = GetInbound(inbound, Global.InboundHttp, 1, false);
                    singboxConfig.inbounds.Add(inbound2);

                    if (_config.inbound[0].allowLANConn)
                    {
                        if (_config.inbound[0].newPort4LAN)
                        {
                            var inbound3 = GetInbound(inbound, Global.InboundSocks2, 2, true);
                            inbound3.listen = "::";
                            singboxConfig.inbounds.Add(inbound3);

                            var inbound4 = GetInbound(inbound, Global.InboundHttp2, 3, false);
                            inbound4.listen = "::";
                            singboxConfig.inbounds.Add(inbound4);

                            //auth
                            if (!Utils.IsNullOrEmpty(_config.inbound[0].user) && !Utils.IsNullOrEmpty(_config.inbound[0].pass))
                            {
                                inbound3.users = new() { new() { username = _config.inbound[0].user, password = _config.inbound[0].pass } };
                                inbound4.users = new() { new() { username = _config.inbound[0].user, password = _config.inbound[0].pass } };
                            }
                        }
                        else
                        {
                            inbound.listen = "::";
                            inbound2.listen = "::";
                        }
                    }
                }

                if (_config.tunModeItem.enableTun)
                {
                    if (_config.tunModeItem.mtu <= 0)
                    {
                        _config.tunModeItem.mtu = Convert.ToInt32(Global.TunMtus[0]);
                    }
                    if (Utils.IsNullOrEmpty(_config.tunModeItem.stack))
                    {
                        _config.tunModeItem.stack = Global.TunStacks[0];
                    }

                    var tunInbound = JsonUtils.Deserialize<Inbound4Sbox>(Utils.GetEmbedText(Global.TunSingboxInboundFileName)) ?? new Inbound4Sbox { };
                    tunInbound.mtu = _config.tunModeItem.mtu;
                    tunInbound.strict_route = _config.tunModeItem.strictRoute;
                    tunInbound.stack = _config.tunModeItem.stack;
                    if (_config.tunModeItem.enableIPv6Address == false)
                    {
                        tunInbound.inet6_address = null;
                    }

                    singboxConfig.inbounds.Add(tunInbound);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private Inbound4Sbox GetInbound(Inbound4Sbox inItem, string tag, int offset, bool bSocks)
        {
            var inbound = JsonUtils.DeepCopy(inItem);
            inbound.tag = tag;
            inbound.listen_port = inItem.listen_port + offset;
            inbound.type = bSocks ? Global.InboundSocks : Global.InboundHttp;
            return inbound;
        }

        private int GenOutbound(ProfileItem node, Outbound4Sbox outbound)
        {
            try
            {
                outbound.server = node.address;
                outbound.server_port = node.port;

                if (node.configType == EConfigType.VMess)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.VMess];

                    outbound.uuid = node.id;
                    outbound.alter_id = node.alterId;
                    if (Global.VmessSecuritys.Contains(node.security))
                    {
                        outbound.security = node.security;
                    }
                    else
                    {
                        outbound.security = Global.DefaultSecurity;
                    }

                    GenOutboundMux(node, outbound);
                }
                else if (node.configType == EConfigType.Shadowsocks)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Shadowsocks];

                    outbound.method = LazyConfig.Instance.GetShadowsocksSecuritys(node).Contains(node.security) ? node.security : "none";
                    outbound.password = node.id;

                    GenOutboundMux(node, outbound);
                }
                else if (node.configType == EConfigType.Socks)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Socks];

                    outbound.version = "5";
                    if (!Utils.IsNullOrEmpty(node.security)
                      && !Utils.IsNullOrEmpty(node.id))
                    {
                        outbound.username = node.security;
                        outbound.password = node.id;
                    }
                }
                else if (node.configType == EConfigType.VLESS)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.VLESS];

                    outbound.uuid = node.id;

                    outbound.packet_encoding = "xudp";

                    if (Utils.IsNullOrEmpty(node.flow))
                    {
                        GenOutboundMux(node, outbound);
                    }
                    else
                    {
                        outbound.flow = node.flow;
                    }
                }
                else if (node.configType == EConfigType.Trojan)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Trojan];

                    outbound.password = node.id;

                    GenOutboundMux(node, outbound);
                }
                else if (node.configType == EConfigType.Hysteria2)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Hysteria2];

                    outbound.password = node.id;

                    outbound.up_mbps = _config.hysteriaItem.up_mbps > 0 ? _config.hysteriaItem.up_mbps : null;
                    outbound.down_mbps = _config.hysteriaItem.down_mbps > 0 ? _config.hysteriaItem.down_mbps : null;

                    GenOutboundMux(node, outbound);
                }
                else if (node.configType == EConfigType.Tuic)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Tuic];

                    outbound.uuid = node.id;
                    outbound.password = node.security;
                    outbound.congestion_control = node.headerType;

                    GenOutboundMux(node, outbound);
                }

                GenOutboundTls(node, outbound);

                GenOutboundTransport(node, outbound);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenOutboundMux(ProfileItem node, Outbound4Sbox outbound)
        {
            try
            {
                //if (_config.coreBasicItem.muxEnabled)
                //{
                //    var mux = new Multiplex4Sbox()
                //    {
                //        enabled = true,
                //        protocol = _config.mux4Sbox.protocol,
                //        max_connections = _config.mux4Sbox.max_connections,
                //        min_streams = _config.mux4Sbox.min_streams,
                //        max_streams = _config.mux4Sbox.max_streams,
                //        padding = _config.mux4Sbox.padding
                //    };
                //    outbound.multiplex = mux;
                //}
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenOutboundTls(ProfileItem node, Outbound4Sbox outbound)
        {
            try
            {
                if (node.streamSecurity == Global.StreamSecurityReality || node.streamSecurity == Global.StreamSecurity)
                {
                    var server_name = string.Empty;
                    if (!string.IsNullOrWhiteSpace(node.sni))
                    {
                        server_name = node.sni;
                    }
                    else if (!string.IsNullOrWhiteSpace(node.requestHost))
                    {
                        server_name = Utils.String2List(node.requestHost)[0];
                    }
                    var tls = new Tls4Sbox()
                    {
                        enabled = true,
                        server_name = server_name,
                        insecure = Utils.ToBool(node.allowInsecure.IsNullOrEmpty() ? _config.coreBasicItem.defAllowInsecure.ToString().ToLower() : node.allowInsecure),
                        alpn = node.GetAlpn(),
                    };
                    if (!Utils.IsNullOrEmpty(node.fingerprint))
                    {
                        tls.utls = new Utls4Sbox()
                        {
                            enabled = true,
                            fingerprint = node.fingerprint.IsNullOrEmpty() ? _config.coreBasicItem.defFingerprint : node.fingerprint
                        };
                    }
                    if (node.streamSecurity == Global.StreamSecurityReality)
                    {
                        tls.reality = new Reality4Sbox()
                        {
                            enabled = true,
                            public_key = node.publicKey,
                            short_id = node.shortId
                        };
                        tls.insecure = false;
                    }
                    outbound.tls = tls;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenOutboundTransport(ProfileItem node, Outbound4Sbox outbound)
        {
            try
            {
                var transport = new Transport4Sbox();

                switch (node.GetNetwork())
                {
                    case "h2":
                        transport.type = "http";
                        transport.host = Utils.IsNullOrEmpty(node.requestHost) ? null : Utils.String2List(node.requestHost);
                        transport.path = Utils.IsNullOrEmpty(node.path) ? null : node.path;
                        break;

                    case "ws":
                        transport.type = "ws";
                        transport.path = Utils.IsNullOrEmpty(node.path) ? null : node.path;
                        if (!Utils.IsNullOrEmpty(node.requestHost))
                        {
                            transport.headers = new()
                            {
                                Host = node.requestHost
                            };
                        }
                        break;

                    case "quic":
                        transport.type = "quic";
                        break;

                    case "grpc":
                        transport.type = "grpc";
                        transport.service_name = node.path;
                        transport.idle_timeout = _config.grpcItem.idle_timeout.ToString("##s");
                        transport.ping_timeout = _config.grpcItem.health_check_timeout.ToString("##s");
                        transport.permit_without_stream = _config.grpcItem.permit_without_stream;
                        break;

                    default:
                        transport = null;
                        break;
                }

                outbound.transport = transport;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenMoreOutbounds(ProfileItem node, SingboxConfig singboxConfig)
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
                var outbound = singboxConfig.outbounds[0];
                var txtOutbound = Utils.GetEmbedText(Global.SingboxSampleOutbound);

                //Previous proxy
                var prevNode = LazyConfig.Instance.GetProfileItemViaRemarks(subItem.prevProfile!);
                if (prevNode is not null
                    && prevNode.configType != EConfigType.Custom)
                {
                    var prevOutbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    GenOutbound(prevNode, prevOutbound);
                    prevOutbound.tag = $"{Global.ProxyTag}2";
                    singboxConfig.outbounds.Add(prevOutbound);

                    outbound.detour = prevOutbound.tag;
                }

                //Next proxy
                var nextNode = LazyConfig.Instance.GetProfileItemViaRemarks(subItem.nextProfile!);
                if (nextNode is not null
                    && nextNode.configType != EConfigType.Custom)
                {
                    var nextOutbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    GenOutbound(nextNode, nextOutbound);
                    nextOutbound.tag = Global.ProxyTag;
                    singboxConfig.outbounds.Insert(0, nextOutbound);

                    outbound.tag = $"{Global.ProxyTag}1";
                    nextOutbound.detour = outbound.tag;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }

            return 0;
        }

        private int GenRouting(SingboxConfig singboxConfig)
        {
            try
            {
                if (_config.tunModeItem.enableTun)
                {
                    singboxConfig.route.auto_detect_interface = true;

                    var tunRules = JsonUtils.Deserialize<List<Rule4Sbox>>(Utils.GetEmbedText(Global.TunSingboxRulesFileName));
                    singboxConfig.route.rules.AddRange(tunRules);

                    GenRoutingDirectExe(out List<string> lstDnsExe, out List<string> lstDirectExe);
                    singboxConfig.route.rules.Add(new()
                    {
                        port = new() { 53 },
                        outbound = "dns_out",
                        process_name = lstDnsExe
                    });

                    singboxConfig.route.rules.Add(new()
                    {
                        outbound = "direct",
                        process_name = lstDirectExe
                    });
                }

                if (_config.routingBasicItem.enableRoutingAdvanced)
                {
                    var routing = ConfigHandler.GetDefaultRouting(_config);
                    if (routing != null)
                    {
                        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.ruleSet);
                        foreach (var item in rules!)
                        {
                            if (item.enabled)
                            {
                                GenRoutingUserRule(item, singboxConfig.route.rules);
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
                        foreach (var item in rules!)
                        {
                            GenRoutingUserRule(item, singboxConfig.route.rules);
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

        private void GenRoutingDirectExe(out List<string> lstDnsExe, out List<string> lstDirectExe)
        {
            lstDnsExe = new();
            lstDirectExe = new();
            var coreInfos = LazyConfig.Instance.GetCoreInfos();
            foreach (var it in coreInfos)
            {
                if (it.coreType == ECoreType.v2rayN)
                {
                    continue;
                }
                foreach (var it2 in it.coreExes)
                {
                    if (!lstDnsExe.Contains(it2) && it.coreType != ECoreType.sing_box)
                    {
                        lstDnsExe.Add($"{it2}.exe");
                    }

                    if (!lstDirectExe.Contains(it2))
                    {
                        lstDirectExe.Add($"{it2}.exe");
                    }
                }
            }
        }

        private int GenRoutingUserRule(RulesItem item, List<Rule4Sbox> rules)
        {
            try
            {
                if (item == null)
                {
                    return 0;
                }

                var rule = new Rule4Sbox()
                {
                    outbound = item.outboundTag,
                };

                if (!Utils.IsNullOrEmpty(item.port))
                {
                    if (item.port.Contains("-"))
                    {
                        rule.port_range = new List<string> { item.port.Replace("-", ":") };
                    }
                    else
                    {
                        rule.port = new List<int> { Utils.ToInt(item.port) };
                    }
                }
                if (item.protocol?.Count > 0)
                {
                    rule.protocol = item.protocol;
                }
                if (item.inboundTag?.Count >= 0)
                {
                    rule.inbound = item.inboundTag;
                }
                var rule2 = JsonUtils.DeepCopy(rule);
                var rule3 = JsonUtils.DeepCopy(rule);

                var hasDomainIp = false;
                if (item.domain?.Count > 0)
                {
                    foreach (var it in item.domain)
                    {
                        ParseV2Domain(it, rule);
                    }
                    rules.Add(rule);
                    hasDomainIp = true;
                }

                if (item.ip?.Count > 0)
                {
                    foreach (var it in item.ip)
                    {
                        ParseV2Address(it, rule2);
                    }
                    rules.Add(rule2);
                    hasDomainIp = true;
                }

                if (_config.tunModeItem.enableTun && item.process?.Count > 0)
                {
                    rule3.process_name = item.process;
                    rules.Add(rule3);
                    hasDomainIp = true;
                }

                if (!hasDomainIp)
                {
                    rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private void ParseV2Domain(string domain, Rule4Sbox rule)
        {
            if (domain.StartsWith("ext:") || domain.StartsWith("ext-domain:"))
            {
                return;
            }
            else if (domain.StartsWith("geosite:"))
            {
                if (rule.geosite is null) { rule.geosite = new(); }
                rule.geosite?.Add(domain.Substring(8));
            }
            else if (domain.StartsWith("regexp:"))
            {
                if (rule.domain_regex is null) { rule.domain_regex = new(); }
                rule.domain_regex?.Add(domain.Replace(Global.RoutingRuleComma, ",").Substring(7));
            }
            else if (domain.StartsWith("domain:"))
            {
                if (rule.domain is null) { rule.domain = new(); }
                if (rule.domain_suffix is null) { rule.domain_suffix = new(); }
                rule.domain?.Add(domain.Substring(7));
                rule.domain_suffix?.Add("." + domain.Substring(7));
            }
            else if (domain.StartsWith("full:"))
            {
                if (rule.domain is null) { rule.domain = new(); }
                rule.domain?.Add(domain.Substring(5));
            }
            else if (domain.StartsWith("keyword:"))
            {
                if (rule.domain_keyword is null) { rule.domain_keyword = new(); }
                rule.domain_keyword?.Add(domain.Substring(8));
            }
            else
            {
                if (rule.domain_keyword is null) { rule.domain_keyword = new(); }
                rule.domain_keyword?.Add(domain);
            }
        }

        private void ParseV2Address(string address, Rule4Sbox rule)
        {
            if (address.StartsWith("ext:") || address.StartsWith("ext-ip:"))
            {
                return;
            }
            else if (address.StartsWith("geoip:!"))
            {
                return;
            }
            else if (address.StartsWith("geoip:"))
            {
                if (rule.geoip is null) { rule.geoip = new(); }
                rule.geoip?.Add(address.Substring(6));
            }
            else
            {
                if (rule.ip_cidr is null) { rule.ip_cidr = new(); }
                rule.ip_cidr?.Add(address);
            }
        }

        private int GenDns(ProfileItem node, SingboxConfig singboxConfig)
        {
            try
            {
                Dns4Sbox? dns4Sbox;
                if (_config.tunModeItem.enableTun)
                {
                    var item = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
                    var tunDNS = item?.tunDNS;
                    if (string.IsNullOrWhiteSpace(tunDNS))
                    {
                        tunDNS = Utils.GetEmbedText(Global.TunSingboxDNSFileName);
                    }
                    dns4Sbox = JsonUtils.Deserialize<Dns4Sbox>(tunDNS);
                }
                else
                {
                    var item = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
                    var normalDNS = item?.normalDNS;
                    if (string.IsNullOrWhiteSpace(normalDNS))
                    {
                        normalDNS = "{\"servers\":[{\"address\":\"tcp://8.8.8.8\"}]}";
                    }

                    dns4Sbox = JsonUtils.Deserialize<Dns4Sbox>(normalDNS);
                }
                if (dns4Sbox is null)
                {
                    return 0;
                }
                //Add the dns of the remote server domain
                if (dns4Sbox.rules is null)
                {
                    dns4Sbox.rules = new();
                }
                dns4Sbox.servers.Add(new()
                {
                    tag = "local_local",
                    address = "223.5.5.5",
                    detour = "direct"
                });
                dns4Sbox.rules.Add(new()
                {
                    server = "local_local",
                    outbound = "any"
                });

                singboxConfig.dns = dns4Sbox;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenStatistic(SingboxConfig singboxConfig)
        {
            if (_config.guiItem.enableStatistics)
            {
                singboxConfig.experimental = new Experimental4Sbox()
                {
                    //cache_file = new CacheFile4Sbox()
                    //{
                    //    enabled = true
                    //},
                    //v2ray_api = new V2ray_Api4Sbox()
                    //{
                    //    listen = $"{Global.Loopback}:{Global.StatePort}",
                    //    stats = new Stats4Sbox()
                    //    {
                    //        enabled = true,
                    //    }
                    //},
                    clash_api = new Clash_Api4Sbox()
                    {
                        external_controller = $"{Global.Loopback}:{Global.StatePort}",
                    }
                };
            }
            return 0;
        }

        #endregion private gen function

        #region Gen speedtest config

        public int GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds, out SingboxConfig? singboxConfig, out string msg)
        {
            singboxConfig = null;
            try
            {
                if (_config == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                msg = ResUI.InitialConfiguration;

                string result = Utils.GetEmbedText(Global.SingboxSampleClient);
                string txtOutbound = Utils.GetEmbedText(Global.SingboxSampleOutbound);
                if (Utils.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
                if (singboxConfig == null)
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

                GenLog(singboxConfig);
                GenDns(new(), singboxConfig);
                singboxConfig.inbounds.Clear(); // Remove "proxy" service for speedtest, avoiding port conflicts.
                singboxConfig.outbounds.RemoveAt(0);

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
                    Inbound4Sbox inbound = new()
                    {
                        listen = Global.Loopback,
                        listen_port = port,
                        type = Global.InboundHttp
                    };
                    inbound.tag = Global.InboundHttp + inbound.listen_port.ToString();
                    singboxConfig.inbounds.Add(inbound);

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

                    var outbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    GenOutbound(item, outbound);
                    outbound.tag = Global.ProxyTag + inbound.listen_port.ToString();
                    singboxConfig.outbounds.Add(outbound);

                    //rule
                    Rule4Sbox rule = new()
                    {
                        inbound = new List<string> { inbound.tag },
                        outbound = outbound.tag
                    };
                    singboxConfig.route.rules.Add(rule);
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

        #endregion Gen speedtest config
    }
}