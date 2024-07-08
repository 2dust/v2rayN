using System.Data;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using v2rayN.Enums;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.Handler.CoreConfig
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
                if (node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.splithttp))
                {
                    msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
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

                GenExperimental(singboxConfig);

                ConvertGeo2Ruleset(singboxConfig);

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
                var listen = "::";
                singboxConfig.inbounds = [];

                if (!_config.tunModeItem.enableTun
                    || (_config.tunModeItem.enableTun && _config.tunModeItem.enableExInbound && _config.runningCoreType == ECoreType.sing_box))
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
                    var inbound2 = GetInbound(inbound, EInboundProtocol.http, false);
                    singboxConfig.inbounds.Add(inbound2);

                    if (_config.inbound[0].allowLANConn)
                    {
                        if (_config.inbound[0].newPort4LAN)
                        {
                            var inbound3 = GetInbound(inbound, EInboundProtocol.socks2, true);
                            inbound3.listen = listen;
                            singboxConfig.inbounds.Add(inbound3);

                            var inbound4 = GetInbound(inbound, EInboundProtocol.http2, false);
                            inbound4.listen = listen;
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
                            inbound.listen = listen;
                            inbound2.listen = listen;
                        }
                    }
                }

                if (_config.tunModeItem.enableTun)
                {
                    if (_config.tunModeItem.mtu <= 0)
                    {
                        _config.tunModeItem.mtu = Utils.ToInt(Global.TunMtus[0]);
                    }
                    if (Utils.IsNullOrEmpty(_config.tunModeItem.stack))
                    {
                        _config.tunModeItem.stack = Global.TunStacks[0];
                    }

                    var tunInbound = JsonUtils.Deserialize<Inbound4Sbox>(Utils.GetEmbedText(Global.TunSingboxInboundFileName)) ?? new Inbound4Sbox { };
                    tunInbound.mtu = _config.tunModeItem.mtu;
                    tunInbound.strict_route = _config.tunModeItem.strictRoute;
                    tunInbound.stack = _config.tunModeItem.stack;
                    tunInbound.sniff = _config.inbound[0].sniffingEnabled;
                    //tunInbound.sniff_override_destination = _config.inbound[0].routeOnly ? false : _config.inbound[0].sniffingEnabled;
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
            var inbound = JsonUtils.DeepCopy(inItem);
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
                outbound.type = Global.ProtocolTypes[node.configType];

                switch (node.configType)
                {
                    case EConfigType.VMess:
                        {
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
                            break;
                        }
                    case EConfigType.Shadowsocks:
                        {
                            outbound.method = LazyConfig.Instance.GetShadowsocksSecurities(node).Contains(node.security) ? node.security : Global.None;
                            outbound.password = node.id;

                            GenOutboundMux(node, outbound);
                            break;
                        }
                    case EConfigType.Socks:
                        {
                            outbound.version = "5";
                            if (!Utils.IsNullOrEmpty(node.security)
                              && !Utils.IsNullOrEmpty(node.id))
                            {
                                outbound.username = node.security;
                                outbound.password = node.id;
                            }
                            break;
                        }
                    case EConfigType.Http:
                        {
                            if (!Utils.IsNullOrEmpty(node.security)
                              && !Utils.IsNullOrEmpty(node.id))
                            {
                                outbound.username = node.security;
                                outbound.password = node.id;
                            }
                            break;
                        }
                    case EConfigType.VLESS:
                        {
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
                            break;
                        }
                    case EConfigType.Trojan:
                        {
                            outbound.password = node.id;

                            GenOutboundMux(node, outbound);
                            break;
                        }
                    case EConfigType.Hysteria2:
                        {
                            outbound.password = node.id;

                            if (!Utils.IsNullOrEmpty(node.path))
                            {
                                outbound.obfs = new()
                                {
                                    type = "salamander",
                                    password = node.path.TrimEx(),
                                };
                            }

                            outbound.up_mbps = _config.hysteriaItem.up_mbps > 0 ? _config.hysteriaItem.up_mbps : null;
                            outbound.down_mbps = _config.hysteriaItem.down_mbps > 0 ? _config.hysteriaItem.down_mbps : null;
                            break;
                        }
                    case EConfigType.Tuic:
                        {
                            outbound.uuid = node.id;
                            outbound.password = node.security;
                            outbound.congestion_control = node.headerType;
                            break;
                        }
                    case EConfigType.Wireguard:
                        {
                            outbound.private_key = node.id;
                            outbound.peer_public_key = node.publicKey;
                            outbound.reserved = Utils.String2List(node.path).Select(int.Parse).ToArray();
                            outbound.local_address = [.. Utils.String2List(node.requestHost)];
                            outbound.mtu = Utils.ToInt(node.shortId.IsNullOrEmpty() ? Global.TunMtus.FirstOrDefault() : node.shortId);
                            break;
                        }
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
                if (_config.coreBasicItem.muxEnabled && !Utils.IsNullOrEmpty(_config.mux4SboxItem.protocol))
                {
                    var mux = new Multiplex4Sbox()
                    {
                        enabled = true,
                        protocol = _config.mux4SboxItem.protocol,
                        max_connections = _config.mux4SboxItem.max_connections,
                    };
                    outbound.multiplex = mux;
                }
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
                    if (!Utils.IsNullOrEmpty(node.sni))
                    {
                        server_name = node.sni;
                    }
                    else if (!Utils.IsNullOrEmpty(node.requestHost))
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
                    case nameof(ETransport.h2):
                        transport.type = nameof(ETransport.http);
                        transport.host = Utils.IsNullOrEmpty(node.requestHost) ? null : Utils.String2List(node.requestHost);
                        transport.path = Utils.IsNullOrEmpty(node.path) ? null : node.path;
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
                                transport.host = Utils.IsNullOrEmpty(node.requestHost) ? null : Utils.String2List(node.requestHost);
                                transport.path = Utils.IsNullOrEmpty(node.path) ? null : node.path;
                            }
                        }
                        break;

                    case nameof(ETransport.ws):
                        transport.type = nameof(ETransport.ws);
                        transport.path = Utils.IsNullOrEmpty(node.path) ? null : node.path;
                        if (!Utils.IsNullOrEmpty(node.requestHost))
                        {
                            transport.headers = new()
                            {
                                Host = node.requestHost
                            };
                        }
                        break;

                    case nameof(ETransport.httpupgrade):
                        transport.type = nameof(ETransport.httpupgrade);
                        transport.path = Utils.IsNullOrEmpty(node.path) ? null : node.path;
                        transport.host = Utils.IsNullOrEmpty(node.requestHost) ? null : node.requestHost;

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
                var dnsOutbound = "dns_out";
                if (!_config.inbound[0].sniffingEnabled)
                {
                    singboxConfig.route.rules.Add(new()
                    {
                        port = [53],
                        network = ["udp"],
                        outbound = dnsOutbound
                    });
                }

                if (_config.tunModeItem.enableTun)
                {
                    singboxConfig.route.auto_detect_interface = true;

                    var tunRules = JsonUtils.Deserialize<List<Rule4Sbox>>(Utils.GetEmbedText(Global.TunSingboxRulesFileName));
                    if (tunRules != null)
                    {
                        singboxConfig.route.rules.AddRange(tunRules);
                    }

                    GenRoutingDirectExe(out List<string> lstDnsExe, out List<string> lstDirectExe);
                    singboxConfig.route.rules.Add(new()
                    {
                        port = new() { 53 },
                        outbound = dnsOutbound,
                        process_name = lstDnsExe
                    });

                    singboxConfig.route.rules.Add(new()
                    {
                        outbound = Global.DirectTag,
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
                if (!Utils.IsNullOrEmpty(item.network))
                {
                    rule.network = Utils.String2List(item.network);
                }
                if (item.protocol?.Count > 0)
                {
                    rule.protocol = item.protocol;
                }
                if (item.inboundTag?.Count >= 0)
                {
                    rule.inbound = item.inboundTag;
                }
                var rule1 = JsonUtils.DeepCopy(rule);
                var rule2 = JsonUtils.DeepCopy(rule);
                var rule3 = JsonUtils.DeepCopy(rule);

                var hasDomainIp = false;
                if (item.domain?.Count > 0)
                {
                    var countDomain = 0;
                    foreach (var it in item.domain)
                    {
                        if (ParseV2Domain(it, rule1)) countDomain++;
                    }
                    if (countDomain > 0)
                    {
                        rules.Add(rule1);
                        hasDomainIp = true;
                    }
                }

                if (item.ip?.Count > 0)
                {
                    var countIp = 0;
                    foreach (var it in item.ip)
                    {
                        if (ParseV2Address(it, rule2)) countIp++;
                    }
                    if (countIp > 0)
                    {
                        rules.Add(rule2);
                        hasDomainIp = true;
                    }
                }

                if (_config.tunModeItem.enableTun && item.process?.Count > 0)
                {
                    rule3.process_name = item.process;
                    rules.Add(rule3);
                    hasDomainIp = true;
                }

                if (!hasDomainIp
                    && (rule.port != null || rule.port_range != null || rule.protocol != null || rule.inbound != null))
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

        private bool ParseV2Domain(string domain, Rule4Sbox rule)
        {
            if (domain.StartsWith("#") || domain.StartsWith("ext:") || domain.StartsWith("ext-domain:"))
            {
                return false;
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
            return true;
        }

        private bool ParseV2Address(string address, Rule4Sbox rule)
        {
            if (address.StartsWith("ext:") || address.StartsWith("ext-ip:"))
            {
                return false;
            }
            else if (address.StartsWith("geoip:!"))
            {
                return false;
            }
            else if (address.Equals("geoip:private"))
            {
                rule.ip_is_private = true;
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
            return true;
        }

        private int GenDns(ProfileItem node, SingboxConfig singboxConfig)
        {
            try
            {
                var item = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
                var strDNS = string.Empty;
                if (_config.tunModeItem.enableTun)
                {
                    strDNS = Utils.IsNullOrEmpty(item?.tunDNS) ? Utils.GetEmbedText(Global.TunSingboxDNSFileName) : item?.tunDNS;
                }
                else
                {
                    strDNS = Utils.IsNullOrEmpty(item?.normalDNS) ? Utils.GetEmbedText(Global.DNSSingboxNormalFileName) : item?.normalDNS;
                }

                var dns4Sbox = JsonUtils.Deserialize<Dns4Sbox>(strDNS);
                if (dns4Sbox is null)
                {
                    return 0;
                }
                singboxConfig.dns = dns4Sbox;

                GenDnsDomains(node, singboxConfig);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return 0;
        }

        private int GenDnsDomains(ProfileItem? node, SingboxConfig singboxConfig)
        {
            var dns4Sbox = singboxConfig.dns ?? new();
            dns4Sbox.servers ??= [];
            dns4Sbox.rules ??= [];

            var tag = "local_local";
            dns4Sbox.servers.Add(new()
            {
                tag = tag,
                address = "223.5.5.5",
                detour = Global.DirectTag,
                //strategy = strategy
            });

            var lstDomain = singboxConfig.outbounds
                           .Where(t => !Utils.IsNullOrEmpty(t.server) && Utils.IsDomain(t.server))
                           .Select(t => t.server)
                           .ToList();
            if (lstDomain != null && lstDomain.Count > 0)
            {
                dns4Sbox.rules.Insert(0, new()
                {
                    server = tag,
                    domain = lstDomain
                });
            }

            //Tun2SocksAddress
            if (_config.tunModeItem.enableTun && node?.configType == EConfigType.Socks && Utils.IsDomain(node?.sni))
            {
                dns4Sbox.rules.Insert(0, new()
                {
                    server = tag,
                    domain = [node?.sni]
                });
            }

            singboxConfig.dns = dns4Sbox;
            return 0;
        }

        private int GenExperimental(SingboxConfig singboxConfig)
        {
            if (_config.guiItem.enableStatistics)
            {
                singboxConfig.experimental ??= new Experimental4Sbox();
                singboxConfig.experimental.clash_api = new Clash_Api4Sbox()
                {
                    external_controller = $"{Global.Loopback}:{LazyConfig.Instance.StatePort2}",
                };
            }

            if (_config.coreBasicItem.enableCacheFile4Sbox)
            {
                singboxConfig.experimental ??= new Experimental4Sbox();
                singboxConfig.experimental.cache_file = new CacheFile4Sbox()
                {
                    enabled = true
                };
            }

            return 0;
        }

        private int ConvertGeo2Ruleset(SingboxConfig singboxConfig)
        {
            static void AddRuleSets(List<string> ruleSets, List<string>? rule_set)
            {
                if (rule_set != null) ruleSets.AddRange(rule_set);
            }
            var geosite = "geosite";
            var geoip = "geoip";
            var ruleSets = new List<string>();

            //convert route geosite & geoip to ruleset
            foreach (var rule in singboxConfig.route.rules.Where(t => t.geosite?.Count > 0).ToList() ?? [])
            {
                rule.rule_set = rule?.geosite?.Select(t => $"{geosite}-{t}").ToList();
                rule.geosite = null;
                AddRuleSets(ruleSets, rule.rule_set);
            }
            foreach (var rule in singboxConfig.route.rules.Where(t => t.geoip?.Count > 0).ToList() ?? [])
            {
                rule.rule_set = rule?.geoip?.Select(t => $"{geoip}-{t}").ToList();
                rule.geoip = null;
                AddRuleSets(ruleSets, rule.rule_set);
            }

            //convert dns geosite & geoip to ruleset
            foreach (var rule in singboxConfig.dns?.rules.Where(t => t.geosite?.Count > 0).ToList() ?? [])
            {
                rule.rule_set = rule?.geosite?.Select(t => $"{geosite}-{t}").ToList();
                rule.geosite = null;
            }
            foreach (var rule in singboxConfig.dns?.rules.Where(t => t.geoip?.Count > 0).ToList() ?? [])
            {
                rule.rule_set = rule?.geoip?.Select(t => $"{geoip}-{t}").ToList();
                rule.geoip = null;
            }
            foreach (var dnsRule in singboxConfig.dns?.rules.Where(t => t.rule_set?.Count > 0).ToList() ?? [])
            {
                AddRuleSets(ruleSets, dnsRule.rule_set);
            }
            //rules in rules
            foreach (var item in singboxConfig.dns?.rules.Where(t => t.rules?.Count > 0).Select(t => t.rules).ToList() ?? [])
            {
                foreach (var item2 in item ?? [])
                {
                    AddRuleSets(ruleSets, item2.rule_set);
                }
            }

            //load custom ruleset file
            List<Ruleset4Sbox> customRulesets = [];
            if (_config.routingBasicItem.enableRoutingAdvanced)
            {
                var routing = ConfigHandler.GetDefaultRouting(_config);
                if (!Utils.IsNullOrEmpty(routing.customRulesetPath4Singbox))
                {
                    var result = Utils.LoadResource(routing.customRulesetPath4Singbox);
                    if (!Utils.IsNullOrEmpty(result))
                    {
                        customRulesets = (JsonUtils.Deserialize<List<Ruleset4Sbox>>(result) ?? [])
                            .Where(t => t.tag != null)
                            .Where(t => t.type != null)
                            .Where(t => t.format != null)
                            .ToList();
                    }
                }
            }

            //Local srs files address
            var localSrss = Utils.GetBinPath("srss");

            //Add ruleset srs
            singboxConfig.route.rule_set = [];
            foreach (var item in new HashSet<string>(ruleSets))
            {
                if (Utils.IsNullOrEmpty(item)) { continue; }
                var customRuleset = customRulesets.FirstOrDefault(t => t.tag != null && t.tag.Equals(item));
                if (customRuleset is null)
                {
                    var pathSrs = Path.Combine(localSrss, $"{item}.srs");
                    if (File.Exists(pathSrs))
                    {
                        customRuleset = new()
                        {
                            type = "local",
                            format = "binary",
                            tag = item,
                            path = pathSrs
                        };
                    }
                    else
                    {
                        customRuleset = new()
                        {
                            type = "remote",
                            format = "binary",
                            tag = item,
                            url = string.Format(Global.SingboxRulesetUrl, item.StartsWith(geosite) ? geosite : geoip, item),
                            download_detour = Global.ProxyTag
                        };
                    }
                }
                singboxConfig.route.rule_set.Add(customRuleset);
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
                        if (item2 is null || Utils.IsNullOrEmpty(item2.id) || !Utils.IsGuidByParse(item2.id))
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

                GenDnsDomains(null, singboxConfig);
                //var dnsServer = singboxConfig.dns?.servers.FirstOrDefault();
                //if (dnsServer != null)
                //{
                //    dnsServer.detour = singboxConfig.route.rules.LastOrDefault()?.outbound;
                //}
                //var dnsRule = singboxConfig.dns?.rules.Where(t => t.outbound != null).FirstOrDefault();
                //if (dnsRule != null)
                //{
                //    singboxConfig.dns.rules = [];
                //    singboxConfig.dns.rules.Add(dnsRule);
                //}

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