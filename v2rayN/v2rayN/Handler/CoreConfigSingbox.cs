using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Model;
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

                string result = Utile.GetEmbedText(Global.SingboxSampleClient);
                if (Utile.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                singboxConfig = JsonUtile.Deserialize<SingboxConfig>(result);
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
                if (_config.coreBasicItem.loglevel == Global.None)
                {
                    singboxConfig.log.disabled = true;
                }
                if (_config.coreBasicItem.logEnabled)
                {
                    var dtNow = DateTime.Now;
                    singboxConfig.log.output = Utile.GetLogPath($"sbox_{dtNow:yyyy-MM-dd}.txt");
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
                        type = EInboundProtocol.socks.ToString(),
                        tag = EInboundProtocol.socks.ToString(),
                        listen = Global.Loopback,
                    };
                    singboxConfig.inbounds.Add(inbound);

                    inbound.listen_port = LazyConfig.Instance.GetLocalPort(EInboundProtocol.socks);
                    inbound.sniff = _config.inbound[0].sniffingEnabled;
                    inbound.sniff_override_destination = _config.inbound[0].routeOnly ? false : _config.inbound[0].sniffingEnabled;
                    inbound.domain_strategy = Utile.IsNullOrEmpty(_config.routingBasicItem.domainStrategy4Singbox) ? null : _config.routingBasicItem.domainStrategy4Singbox;

                    if (_config.routingBasicItem.enableRoutingAdvanced)
                    {
                        var routing = ConfigHandler.GetDefaultRouting(_config);
                        if (!Utile.IsNullOrEmpty(routing.domainStrategy4Singbox))
                        {
                            inbound.domain_strategy = routing.domainStrategy4Singbox;
                        }
                    }

                    //http
                    var inbound2 = GetInbound(inbound, EInboundProtocol.http, false);
                    singboxConfig.inbounds.Add(inbound2);

                    if (_config.inbound[0].allowLANConn)
                    {
                        if (_config.inbound[0].newPort4LAN)
                        {
                            var inbound3 = GetInbound(inbound, EInboundProtocol.socks2, true);
                            inbound3.listen = "::";
                            singboxConfig.inbounds.Add(inbound3);

                            var inbound4 = GetInbound(inbound, EInboundProtocol.http2, false);
                            inbound4.listen = "::";
                            singboxConfig.inbounds.Add(inbound4);

                            //auth
                            if (!Utile.IsNullOrEmpty(_config.inbound[0].user) && !Utile.IsNullOrEmpty(_config.inbound[0].pass))
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
                        _config.tunModeItem.mtu = Utile.ToInt(Global.TunMtus[0]);
                    }
                    if (Utile.IsNullOrEmpty(_config.tunModeItem.stack))
                    {
                        _config.tunModeItem.stack = Global.TunStacks[0];
                    }

                    var tunInbound = JsonUtile.Deserialize<Inbound4Sbox>(Utile.GetEmbedText(Global.TunSingboxInboundFileName)) ?? new Inbound4Sbox { };
                    tunInbound.mtu = _config.tunModeItem.mtu;
                    tunInbound.strict_route = _config.tunModeItem.strictRoute;
                    tunInbound.stack = _config.tunModeItem.stack;
                    tunInbound.sniff = _config.inbound[0].sniffingEnabled;
                    tunInbound.sniff_override_destination = _config.inbound[0].routeOnly ? false : _config.inbound[0].sniffingEnabled;
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

        private Inbound4Sbox GetInbound(Inbound4Sbox inItem, EInboundProtocol protocol, bool bSocks)
        {
            var inbound = JsonUtile.DeepCopy(inItem);
            inbound.tag = protocol.ToString();
            inbound.listen_port = inItem.listen_port + (int)protocol;
            inbound.type = bSocks ? EInboundProtocol.socks.ToString() : EInboundProtocol.http.ToString();
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
                    if (Global.VmessSecurities.Contains(node.security))
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

                    outbound.method = LazyConfig.Instance.GetShadowsocksSecurities(node).Contains(node.security) ? node.security : Global.None;
                    outbound.password = node.id;

                    GenOutboundMux(node, outbound);
                }
                else if (node.configType == EConfigType.Socks)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Socks];

                    outbound.version = "5";
                    if (!Utile.IsNullOrEmpty(node.security)
                      && !Utile.IsNullOrEmpty(node.id))
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

                    if (Utile.IsNullOrEmpty(node.flow))
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

                    if (!Utile.IsNullOrEmpty(node.path))
                    {
                        outbound.obfs = new()
                        {
                            type = "salamander",
                            password = node.path.TrimEx(),
                        };
                    }

                    outbound.up_mbps = _config.hysteriaItem.up_mbps > 0 ? _config.hysteriaItem.up_mbps : null;
                    outbound.down_mbps = _config.hysteriaItem.down_mbps > 0 ? _config.hysteriaItem.down_mbps : null;
                }
                else if (node.configType == EConfigType.Tuic)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Tuic];

                    outbound.uuid = node.id;
                    outbound.password = node.security;
                    outbound.congestion_control = node.headerType;
                }
                else if (node.configType == EConfigType.Wireguard)
                {
                    outbound.type = Global.ProtocolTypes[EConfigType.Wireguard];

                    outbound.private_key = node.id;
                    outbound.peer_public_key = node.publicKey;
                    outbound.reserved = Utile.String2List(node.path).Select(int.Parse).ToArray();
                    outbound.local_address = [.. Utile.String2List(node.requestHost)];
                    outbound.mtu = Utile.ToInt(node.shortId.IsNullOrEmpty() ? Global.TunMtus.FirstOrDefault() : node.shortId);
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
                //        protocol = _config.mux4SboxItem.protocol,
                //        max_connections = _config.mux4SboxItem.max_connections,
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
                        server_name = Utile.String2List(node.requestHost)[0];
                    }
                    var tls = new Tls4Sbox()
                    {
                        enabled = true,
                        server_name = server_name,
                        insecure = Utile.ToBool(node.allowInsecure.IsNullOrEmpty() ? _config.coreBasicItem.defAllowInsecure.ToString().ToLower() : node.allowInsecure),
                        alpn = node.GetAlpn(),
                    };
                    if (!Utile.IsNullOrEmpty(node.fingerprint))
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
                    case nameof(ETransport.h2):
                        transport.type = nameof(ETransport.http);
                        transport.host = Utile.IsNullOrEmpty(node.requestHost) ? null : Utile.String2List(node.requestHost);
                        transport.path = Utile.IsNullOrEmpty(node.path) ? null : node.path;
                        break;

                    case nameof(ETransport.tcp):   //http
                        if (node.headerType == Global.TcpHeaderHttp)
                        {
                            if (node.configType == EConfigType.Shadowsocks)
                            {
                                outbound.plugin = "obfs-local";
                                outbound.plugin_opts = $"obfs=http;obfs-host={node.requestHost};";
                            }
                            else
                            {
                                transport.type = nameof(ETransport.http);
                                transport.host = Utile.IsNullOrEmpty(node.requestHost) ? null : Utile.String2List(node.requestHost);
                                transport.path = Utile.IsNullOrEmpty(node.path) ? null : node.path;
                            }
                        }
                        break;

                    case nameof(ETransport.ws):
                        transport.type = nameof(ETransport.ws);
                        transport.path = Utile.IsNullOrEmpty(node.path) ? null : node.path;
                        if (!Utile.IsNullOrEmpty(node.requestHost))
                        {
                            transport.headers = new()
                            {
                                Host = node.requestHost
                            };
                        }
                        break;

                    case nameof(ETransport.httpupgrade):
                        transport.type = nameof(ETransport.httpupgrade);
                        transport.path = Utile.IsNullOrEmpty(node.path) ? null : node.path;
                        transport.host = Utile.IsNullOrEmpty(node.requestHost) ? null : node.requestHost;

                        break;

                    case nameof(ETransport.quic):
                        transport.type = nameof(ETransport.quic);
                        break;

                    case nameof(ETransport.grpc):
                        transport.type = nameof(ETransport.grpc);
                        transport.service_name = node.path;
                        transport.idle_timeout = _config.grpcItem.idle_timeout.ToString("##s");
                        transport.ping_timeout = _config.grpcItem.health_check_timeout.ToString("##s");
                        transport.permit_without_stream = _config.grpcItem.permit_without_stream;
                        break;

                    default:
                        break;
                }
                if (transport.type != null)
                {
                    outbound.transport = transport;
                }
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
                var txtOutbound = Utile.GetEmbedText(Global.SingboxSampleOutbound);

                //Previous proxy
                var prevNode = LazyConfig.Instance.GetProfileItemViaRemarks(subItem.prevProfile!);
                if (prevNode is not null
                    && prevNode.configType != EConfigType.Custom)
                {
                    var prevOutbound = JsonUtile.Deserialize<Outbound4Sbox>(txtOutbound);
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
                    var nextOutbound = JsonUtile.Deserialize<Outbound4Sbox>(txtOutbound);
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

                    var tunRules = JsonUtile.Deserialize<List<Rule4Sbox>>(Utile.GetEmbedText(Global.TunSingboxRulesFileName));
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
                        var rules = JsonUtile.Deserialize<List<RulesItem>>(routing.ruleSet);
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
                        var rules = JsonUtile.Deserialize<List<RulesItem>>(lockedItem.ruleSet);
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
            var coreInfo = LazyConfig.Instance.GetCoreInfo();
            foreach (var it in coreInfo)
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

                if (!Utile.IsNullOrEmpty(item.port))
                {
                    if (item.port.Contains("-"))
                    {
                        rule.port_range = new List<string> { item.port.Replace("-", ":") };
                    }
                    else
                    {
                        rule.port = new List<int> { Utile.ToInt(item.port) };
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
                var rule2 = JsonUtile.DeepCopy(rule);
                var rule3 = JsonUtile.DeepCopy(rule);

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
            if (domain.StartsWith("#") || domain.StartsWith("ext:") || domain.StartsWith("ext-domain:"))
            {
                return;
            }
            else if (domain.StartsWith("geosite:"))
            {
                rule.geosite ??= [];
                rule.geosite?.Add(domain.Substring(8));
            }
            else if (domain.StartsWith("regexp:"))
            {
                rule.domain_regex ??= [];
                rule.domain_regex?.Add(domain.Replace(Global.RoutingRuleComma, ",").Substring(7));
            }
            else if (domain.StartsWith("domain:"))
            {
                rule.domain ??= [];
                rule.domain_suffix ??= [];
                rule.domain?.Add(domain.Substring(7));
                rule.domain_suffix?.Add("." + domain.Substring(7));
            }
            else if (domain.StartsWith("full:"))
            {
                rule.domain ??= [];
                rule.domain?.Add(domain.Substring(5));
            }
            else if (domain.StartsWith("keyword:"))
            {
                rule.domain_keyword ??= [];
                rule.domain_keyword?.Add(domain.Substring(8));
            }
            else
            {
                rule.domain_keyword ??= [];
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
                        tunDNS = Utile.GetEmbedText(Global.TunSingboxDNSFileName);
                    }
                    dns4Sbox = JsonUtile.Deserialize<Dns4Sbox>(tunDNS);
                }
                else
                {
                    var item = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
                    var normalDNS = item?.normalDNS;
                    if (string.IsNullOrWhiteSpace(normalDNS))
                    {
                        normalDNS = "{\"servers\":[{\"address\":\"tcp://8.8.8.8\"}]}";
                    }

                    dns4Sbox = JsonUtile.Deserialize<Dns4Sbox>(normalDNS);
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
                        external_controller = $"{Global.Loopback}:{LazyConfig.Instance.StatePort}",
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

                string result = Utile.GetEmbedText(Global.SingboxSampleClient);
                string txtOutbound = Utile.GetEmbedText(Global.SingboxSampleOutbound);
                if (Utile.IsNullOrEmpty(result) || txtOutbound.IsNullOrEmpty())
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }

                singboxConfig = JsonUtile.Deserialize<SingboxConfig>(result);
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
                //GenDns(new(), singboxConfig);
                singboxConfig.inbounds.Clear(); // Remove "proxy" service for speedtest, avoiding port conflicts.
                singboxConfig.outbounds.RemoveAt(0);

                int httpPort = LazyConfig.Instance.GetLocalPort(EInboundProtocol.speedtest);

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
                        if (item2 is null || Utile.IsNullOrEmpty(item2.id) || !Utile.IsGuidByParse(item2.id))
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
                    Inbound4Sbox inbound = new()
                    {
                        listen = Global.Loopback,
                        listen_port = port,
                        type = EInboundProtocol.http.ToString(),
                    };
                    inbound.tag = inbound.type + inbound.listen_port.ToString();
                    singboxConfig.inbounds.Add(inbound);

                    //outbound
                    var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                    if (item is null)
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.Shadowsocks
                        && !Global.SsSecuritiesInSingbox.Contains(item.security))
                    {
                        continue;
                    }
                    if (item.configType == EConfigType.VLESS
                     && !Global.Flows.Contains(item.flow))
                    {
                        continue;
                    }

                    var outbound = JsonUtile.Deserialize<Outbound4Sbox>(txtOutbound);
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