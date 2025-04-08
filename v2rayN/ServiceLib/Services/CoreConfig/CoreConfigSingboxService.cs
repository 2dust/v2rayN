using System.Data;
using System.Net;
using System.Net.NetworkInformation;
using DynamicData;
using ServiceLib.Models;

namespace ServiceLib.Services.CoreConfig;

public class CoreConfigSingboxService
{
    private Config _config;
    private static readonly string _tag = "CoreConfigSingboxService";

    public CoreConfigSingboxService(Config config)
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
            if (node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            string result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenLog(singboxConfig);

            await GenInbounds(singboxConfig);

            if (node.ConfigType == EConfigType.WireGuard)
            {
                singboxConfig.outbounds.RemoveAt(0);
                var endpoints = new Endpoints4Sbox();
                await GenEndpoint(node, endpoints);
                endpoints.tag = Global.ProxyTag;
                singboxConfig.endpoints = new() { endpoints };
            }
            else
            {
                await GenOutbound(node, singboxConfig.outbounds.First());
            }

            await GenMoreOutbounds(node, singboxConfig);

            await GenRouting(singboxConfig);

            await GenDns(node, singboxConfig);

            await GenExperimental(singboxConfig);

            await ConvertGeo2Ruleset(singboxConfig);

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
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

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
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
                Logging.SaveLog(_tag, ex);
            }

            await GenLog(singboxConfig);
            //GenDns(new(), singboxConfig);
            singboxConfig.inbounds.Clear();
            singboxConfig.outbounds.RemoveAt(0);

            var initPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.speedtest);

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
                    if (item is null || item.Id.IsNullOrEmpty() || !Utils.IsGuidByParse(item.Id))
                    {
                        continue;
                    }
                }

                //find unused port
                var port = initPort;
                for (int k = initPort; k < Global.MaxPort; k++)
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
                    initPort = port + 1;
                    break;
                }

                //Port In Used
                if (lstIpEndPoints?.FindIndex(_it => _it.Port == port) >= 0)
                {
                    continue;
                }
                it.Port = port;
                it.AllowTest = true;

                //inbound
                Inbound4Sbox inbound = new()
                {
                    listen = Global.Loopback,
                    listen_port = port,
                    type = EInboundProtocol.mixed.ToString(),
                };
                inbound.tag = inbound.type + inbound.listen_port.ToString();
                singboxConfig.inbounds.Add(inbound);

                //outbound
                if (item is null)
                {
                    continue;
                }
                if (item.ConfigType == EConfigType.Shadowsocks
                    && !Global.SsSecuritiesInSingbox.Contains(item.Security))
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

                var tag = string.Empty;
                if (item.ConfigType == EConfigType.WireGuard)
                {
                    var endpoints = new Endpoints4Sbox();
                    await GenEndpoint(item, endpoints);
                    endpoints.tag = Global.ProxyTag + inbound.listen_port.ToString();
                    singboxConfig.endpoints ??= new();
                    singboxConfig.endpoints.Add(endpoints);

                    tag = endpoints.tag;
                }
                else
                {
                    var outbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    await GenOutbound(item, outbound);
                    outbound.tag = Global.ProxyTag + inbound.listen_port.ToString();
                    singboxConfig.outbounds.Add(outbound);

                    tag = outbound.tag;
                }

                //rule
                Rule4Sbox rule = new()
                {
                    inbound = new List<string> { inbound.tag },
                    outbound = tag
                };
                singboxConfig.route.rules.Add(rule);
            }

            await GenDnsDomains(null, singboxConfig, null);
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

            //ret.Msg =string.Format(ResUI.SuccessfulConfiguration"), node.getSummary());
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientSpeedtestConfig(ProfileItem node, int port)
    {
        var ret = new RetResult();
        try
        {
            if (node is not { Port: > 0 })
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }
            if (node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenLog(singboxConfig);
            if (node.ConfigType == EConfigType.WireGuard)
            {
                singboxConfig.outbounds.RemoveAt(0);
                var endpoints = new Endpoints4Sbox();
                await GenEndpoint(node, endpoints);
                endpoints.tag = Global.ProxyTag;
                singboxConfig.endpoints = new() { endpoints };
            }
            else
            {
                await GenOutbound(node, singboxConfig.outbounds.First());
            }
            await GenMoreOutbounds(node, singboxConfig);
            await GenDnsDomains(null, singboxConfig, null);

            singboxConfig.route.rules.Clear();
            singboxConfig.inbounds.Clear();
            singboxConfig.inbounds.Add(new()
            {
                tag = $"{EInboundProtocol.mixed}{port}",
                listen = Global.Loopback,
                listen_port = port,
                type = EInboundProtocol.mixed.ToString(),
            });

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
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

            string result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            string txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenLog(singboxConfig);
            await GenInbounds(singboxConfig);
            await GenRouting(singboxConfig);
            await GenExperimental(singboxConfig);
            singboxConfig.outbounds.RemoveAt(0);

            var tagProxy = new List<string>();
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
                if (item is null)
                {
                    continue;
                }
                if (it.ConfigType is EConfigType.VMess or EConfigType.VLESS)
                {
                    if (item.Id.IsNullOrEmpty() || !Utils.IsGuidByParse(item.Id))
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


                if (item.ConfigType == EConfigType.WireGuard)
                {
                    //endpoint
                    var endpoints = new Endpoints4Sbox();
                    await GenEndpoint(item, endpoints);
                    endpoints.tag = $"{Global.ProxyTag}-{tagProxy.Count + 1}";
                    singboxConfig.endpoints ??= new();
                    singboxConfig.endpoints.Add(endpoints);
                    tagProxy.Add(endpoints.tag);
                }
                else
                {
                    //outbound
                    var outbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    await GenOutbound(item, outbound);
                    outbound.tag = $"{Global.ProxyTag}-{tagProxy.Count + 1}";
                    singboxConfig.outbounds.Insert(0, outbound);
                    tagProxy.Add(outbound.tag);
                }
            }
            if (tagProxy.Count <= 0)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenDns(null, singboxConfig);
            await ConvertGeo2Ruleset(singboxConfig);

            //add urltest outbound
            var outUrltest = new Outbound4Sbox
            {
                type = "urltest",
                tag = $"{Global.ProxyTag}-auto",
                outbounds = tagProxy,
                interrupt_exist_connections = false,
            };
            singboxConfig.outbounds.Insert(0, outUrltest);

            //add selector outbound
            var outSelector = new Outbound4Sbox
            {
                type = "selector",
                tag = Global.ProxyTag,
                outbounds = JsonUtils.DeepCopy(tagProxy),
                interrupt_exist_connections = false,
            };
            outSelector.outbounds.Insert(0, outUrltest.tag);
            singboxConfig.outbounds.Insert(0, outSelector);

            ret.Success = true;
            ret.Data = JsonUtils.Serialize(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        if (node == null || fileName is null)
        {
            ret.Msg = ResUI.CheckServerSettings;
            return ret;
        }

        ret.Msg = ResUI.InitialConfiguration;

        try
        {
            if (node == null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            string addressFileName = node.Address;
            if (addressFileName.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }
            if (!File.Exists(addressFileName))
            {
                addressFileName = Path.Combine(Utils.GetConfigPath(), addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "1";
                return ret;
            }

            if (node.Address == Global.CoreMultipleLoadConfigFileName)
            {
                var txtFile = File.ReadAllText(addressFileName);
                var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(txtFile);
                if (singboxConfig == null)
                {
                    File.Copy(addressFileName, fileName);
                }
                else
                {
                    await GenInbounds(singboxConfig);
                    await GenExperimental(singboxConfig);

                    var content = JsonUtils.Serialize(singboxConfig, true);
                    await File.WriteAllTextAsync(fileName, content);
                }
            }
            else
            {
                File.Copy(addressFileName, fileName);
            }

            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "2";
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    #endregion public gen function

    #region private gen function

    private async Task<int> GenLog(SingboxConfig singboxConfig)
    {
        try
        {
            switch (_config.CoreBasicItem.Loglevel)
            {
                case "debug":
                case "info":
                case "error":
                    singboxConfig.log.level = _config.CoreBasicItem.Loglevel;
                    break;

                case "warning":
                    singboxConfig.log.level = "warn";
                    break;

                default:
                    break;
            }
            if (_config.CoreBasicItem.Loglevel == Global.None)
            {
                singboxConfig.log.disabled = true;
            }
            if (_config.CoreBasicItem.LogEnabled)
            {
                var dtNow = DateTime.Now;
                singboxConfig.log.output = Utils.GetLogPath($"sbox_{dtNow:yyyy-MM-dd}.txt");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenInbounds(SingboxConfig singboxConfig)
    {
        try
        {
            var listen = "::";
            singboxConfig.inbounds = [];

            if (!_config.TunModeItem.EnableTun
                || (_config.TunModeItem.EnableTun && _config.TunModeItem.EnableExInbound && _config.RunningCoreType == ECoreType.sing_box))
            {
                var inbound = new Inbound4Sbox()
                {
                    type = EInboundProtocol.mixed.ToString(),
                    tag = EInboundProtocol.socks.ToString(),
                    listen = Global.Loopback,
                };
                singboxConfig.inbounds.Add(inbound);

                inbound.listen_port = AppHandler.Instance.GetLocalPort(EInboundProtocol.socks);

                if (_config.Inbound.First().SecondLocalPortEnabled)
                {
                    var inbound2 = GetInbound(inbound, EInboundProtocol.socks2, true);
                    singboxConfig.inbounds.Add(inbound2);
                }

                if (_config.Inbound.First().AllowLANConn)
                {
                    if (_config.Inbound.First().NewPort4LAN)
                    {
                        var inbound3 = GetInbound(inbound, EInboundProtocol.socks3, true);
                        inbound3.listen = listen;
                        singboxConfig.inbounds.Add(inbound3);

                        //auth
                        if (_config.Inbound.First().User.IsNotEmpty() && _config.Inbound.First().Pass.IsNotEmpty())
                        {
                            inbound3.users = new() { new() { username = _config.Inbound.First().User, password = _config.Inbound.First().Pass } };
                        }
                    }
                    else
                    {
                        inbound.listen = listen;
                    }
                }
            }

            if (_config.TunModeItem.EnableTun)
            {
                if (_config.TunModeItem.Mtu <= 0)
                {
                    _config.TunModeItem.Mtu = Global.TunMtus.First();
                }
                if (_config.TunModeItem.Stack.IsNullOrEmpty())
                {
                    _config.TunModeItem.Stack = Global.TunStacks.First();
                }

                var tunInbound = JsonUtils.Deserialize<Inbound4Sbox>(EmbedUtils.GetEmbedText(Global.TunSingboxInboundFileName)) ?? new Inbound4Sbox { };
                tunInbound.interface_name = Utils.IsOSX() ? $"utun{new Random().Next(99)}" : "singbox_tun";
                tunInbound.mtu = _config.TunModeItem.Mtu;
                tunInbound.strict_route = _config.TunModeItem.StrictRoute;
                tunInbound.stack = _config.TunModeItem.Stack;
                if (_config.TunModeItem.EnableIPv6Address == false)
                {
                    tunInbound.address = ["172.18.0.1/30"];
                }

                singboxConfig.inbounds.Add(tunInbound);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private Inbound4Sbox GetInbound(Inbound4Sbox inItem, EInboundProtocol protocol, bool bSocks)
    {
        var inbound = JsonUtils.DeepCopy(inItem);
        inbound.tag = protocol.ToString();
        inbound.listen_port = inItem.listen_port + (int)protocol;
        inbound.type = EInboundProtocol.mixed.ToString();
        return inbound;
    }

    private async Task<int> GenOutbound(ProfileItem node, Outbound4Sbox outbound)
    {
        try
        {
            outbound.server = node.Address;
            outbound.server_port = node.Port;
            outbound.type = Global.ProtocolTypes[node.ConfigType];

            if (Utils.IsDomain(node.Address))
            {
                var item = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
                outbound.domain_resolver = new()
                {
                    server = "local_local",
                    strategy = string.IsNullOrEmpty(item?.DomainStrategy4Freedom) ? null : item?.DomainStrategy4Freedom
                };
            }

            switch (node.ConfigType)
            {
                case EConfigType.VMess:
                    {
                        outbound.uuid = node.Id;
                        outbound.alter_id = node.AlterId;
                        if (Global.VmessSecurities.Contains(node.Security))
                        {
                            outbound.security = node.Security;
                        }
                        else
                        {
                            outbound.security = Global.DefaultSecurity;
                        }

                        await GenOutboundMux(node, outbound);
                        break;
                    }
                case EConfigType.Shadowsocks:
                    {
                        outbound.method = AppHandler.Instance.GetShadowsocksSecurities(node).Contains(node.Security) ? node.Security : Global.None;
                        outbound.password = node.Id;

                        await GenOutboundMux(node, outbound);
                        break;
                    }
                case EConfigType.SOCKS:
                    {
                        outbound.version = "5";
                        if (node.Security.IsNotEmpty()
                          && node.Id.IsNotEmpty())
                        {
                            outbound.username = node.Security;
                            outbound.password = node.Id;
                        }
                        break;
                    }
                case EConfigType.HTTP:
                    {
                        if (node.Security.IsNotEmpty()
                          && node.Id.IsNotEmpty())
                        {
                            outbound.username = node.Security;
                            outbound.password = node.Id;
                        }
                        break;
                    }
                case EConfigType.VLESS:
                    {
                        outbound.uuid = node.Id;

                        outbound.packet_encoding = "xudp";

                        if (node.Flow.IsNullOrEmpty())
                        {
                            await GenOutboundMux(node, outbound);
                        }
                        else
                        {
                            outbound.flow = node.Flow;
                        }
                        break;
                    }
                case EConfigType.Trojan:
                    {
                        outbound.password = node.Id;

                        await GenOutboundMux(node, outbound);
                        break;
                    }
                case EConfigType.Hysteria2:
                    {
                        outbound.password = node.Id;

                        if (node.Path.IsNotEmpty())
                        {
                            outbound.obfs = new()
                            {
                                type = "salamander",
                                password = node.Path.TrimEx(),
                            };
                        }

                        outbound.up_mbps = _config.HysteriaItem.UpMbps > 0 ? _config.HysteriaItem.UpMbps : null;
                        outbound.down_mbps = _config.HysteriaItem.DownMbps > 0 ? _config.HysteriaItem.DownMbps : null;
                        if (node.Ports.IsNotEmpty())
                        {
                            outbound.server_port = null;
                            outbound.server_ports = node.Ports.Split(',')
                                .Where(p => p.Trim().IsNotEmpty())
                                .Select(p => p.Replace('-', ':'))
                                .ToList();
                            outbound.hop_interval = _config.HysteriaItem.HopInterval > 0 ? $"{_config.HysteriaItem.HopInterval}s" : null;
                        }

                        break;
                    }
                case EConfigType.TUIC:
                    {
                        outbound.uuid = node.Id;
                        outbound.password = node.Security;
                        outbound.congestion_control = node.HeaderType;
                        break;
                    }
                case EConfigType.Anytls:
                    {
                        outbound.password = node.Id;
                        break;
                    }
            }

            await GenOutboundTls(node, outbound);

            await GenOutboundTransport(node, outbound);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenEndpoint(ProfileItem node, Endpoints4Sbox endpoints)
    {
        try
        {
            endpoints.address = Utils.String2List(node.RequestHost);
            // Utils.GetFreePort() 9090 ?
            endpoints.listen_port = Utils.GetFreePort();
            endpoints.type = Global.ProtocolTypes[node.ConfigType];

            if (Utils.IsDomain(node.Address))
            {
                var item = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
                endpoints.domain_resolver = new()
                {
                    server = "local_local",
                    strategy = string.IsNullOrEmpty(item?.DomainStrategy4Freedom) ? null : item?.DomainStrategy4Freedom
                };
            }

            switch (node.ConfigType)
            {
                case EConfigType.WireGuard:
                    {
                        var peer = new Peer4Sbox
                        {
                            public_key = node.PublicKey,
                            reserved = Utils.String2List(node.Path)?.Select(int.Parse).ToList(),
                            address = node.Address,
                            port = node.Port,
                            // TODO default ["0.0.0.0/0", "::/0"]
                            allowed_ips = new() { "0.0.0.0/0", "::/0" },
                        };
                        endpoints.private_key = node.Id;
                        endpoints.mtu = node.ShortId.IsNullOrEmpty() ? Global.TunMtus.First() : node.ShortId.ToInt();
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenOutboundMux(ProfileItem node, Outbound4Sbox outbound)
    {
        try
        {
            if (_config.CoreBasicItem.MuxEnabled && _config.Mux4SboxItem.Protocol.IsNotEmpty())
            {
                var mux = new Multiplex4Sbox()
                {
                    enabled = true,
                    protocol = _config.Mux4SboxItem.Protocol,
                    max_connections = _config.Mux4SboxItem.MaxConnections,
                    padding = _config.Mux4SboxItem.Padding,
                };
                outbound.multiplex = mux;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenOutboundTls(ProfileItem node, Outbound4Sbox outbound)
    {
        try
        {
            if (node.StreamSecurity == Global.StreamSecurityReality || node.StreamSecurity == Global.StreamSecurity)
            {
                var server_name = string.Empty;
                if (node.Sni.IsNotEmpty())
                {
                    server_name = node.Sni;
                }
                else if (node.RequestHost.IsNotEmpty())
                {
                    server_name = Utils.String2List(node.RequestHost)?.First();
                }
                var tls = new Tls4Sbox()
                {
                    enabled = true,
                    server_name = server_name,
                    insecure = Utils.ToBool(node.AllowInsecure.IsNullOrEmpty() ? _config.CoreBasicItem.DefAllowInsecure.ToString().ToLower() : node.AllowInsecure),
                    alpn = node.GetAlpn(),
                };
                if (node.Fingerprint.IsNotEmpty())
                {
                    tls.utls = new Utls4Sbox()
                    {
                        enabled = true,
                        fingerprint = node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : node.Fingerprint
                    };
                }
                if (node.StreamSecurity == Global.StreamSecurityReality)
                {
                    tls.reality = new Reality4Sbox()
                    {
                        enabled = true,
                        public_key = node.PublicKey,
                        short_id = node.ShortId
                    };
                    tls.insecure = false;
                }
                outbound.tls = tls;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenOutboundTransport(ProfileItem node, Outbound4Sbox outbound)
    {
        try
        {
            var transport = new Transport4Sbox();

            switch (node.GetNetwork())
            {
                case nameof(ETransport.h2):
                    transport.type = nameof(ETransport.http);
                    transport.host = node.RequestHost.IsNullOrEmpty() ? null : Utils.String2List(node.RequestHost);
                    transport.path = node.Path.IsNullOrEmpty() ? null : node.Path;
                    break;

                case nameof(ETransport.tcp):   //http
                    if (node.HeaderType == Global.TcpHeaderHttp)
                    {
                        if (node.ConfigType == EConfigType.Shadowsocks)
                        {
                            outbound.plugin = "obfs-local";
                            outbound.plugin_opts = $"obfs=http;obfs-host={node.RequestHost};";
                        }
                        else
                        {
                            transport.type = nameof(ETransport.http);
                            transport.host = node.RequestHost.IsNullOrEmpty() ? null : Utils.String2List(node.RequestHost);
                            transport.path = node.Path.IsNullOrEmpty() ? null : node.Path;
                        }
                    }
                    break;

                case nameof(ETransport.ws):
                    transport.type = nameof(ETransport.ws);
                    transport.path = node.Path.IsNullOrEmpty() ? null : node.Path;
                    if (node.RequestHost.IsNotEmpty())
                    {
                        transport.headers = new()
                        {
                            Host = node.RequestHost
                        };
                    }
                    break;

                case nameof(ETransport.httpupgrade):
                    transport.type = nameof(ETransport.httpupgrade);
                    transport.path = node.Path.IsNullOrEmpty() ? null : node.Path;
                    transport.host = node.RequestHost.IsNullOrEmpty() ? null : node.RequestHost;

                    break;

                case nameof(ETransport.quic):
                    transport.type = nameof(ETransport.quic);
                    break;

                case nameof(ETransport.grpc):
                    transport.type = nameof(ETransport.grpc);
                    transport.service_name = node.Path;
                    transport.idle_timeout = _config.GrpcItem.IdleTimeout?.ToString("##s");
                    transport.ping_timeout = _config.GrpcItem.HealthCheckTimeout?.ToString("##s");
                    transport.permit_without_stream = _config.GrpcItem.PermitWithoutStream;
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
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenMoreOutbounds(ProfileItem node, SingboxConfig singboxConfig)
    {
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
            var endpoint = singboxConfig.endpoints?.FirstOrDefault(t => t.tag == Global.ProxyTag);
            var outbound = endpoint == null ? singboxConfig.outbounds.First() : null;

            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);

            //Previous proxy
            var prevNode = await AppHandler.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
            if (prevNode is not null
                && prevNode.ConfigType != EConfigType.Custom)
            {
                var tag = string.Empty;
                if (prevNode.ConfigType == EConfigType.WireGuard)
                {
                    var prevEndpoint = JsonUtils.Deserialize<Endpoints4Sbox>(txtOutbound);
                    await GenEndpoint(prevNode, prevEndpoint);
                    prevEndpoint.tag = $"{Global.ProxyTag}2";
                    singboxConfig.endpoints ??= new();
                    singboxConfig.endpoints.Add(prevEndpoint);

                    tag = prevEndpoint.tag;
                }
                else
                {
                    var prevOutbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    await GenOutbound(prevNode, prevOutbound);
                    prevOutbound.tag = $"{Global.ProxyTag}2";
                    singboxConfig.outbounds.Add(prevOutbound);

                    tag = prevOutbound.tag;
                }
                if (endpoint != null)
                {
                    endpoint.detour = tag;
                }
                else
                {
                    outbound.detour = tag;
                }
            }

            //Next proxy
            var nextNode = await AppHandler.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
            if (nextNode is not null
                && nextNode.ConfigType != EConfigType.Custom)
            {
                var tag = $"{Global.ProxyTag}1";
                if (endpoint != null)
                {
                    endpoint.tag = tag;
                }
                else
                {
                    outbound.tag = tag;
                }
                if (nextNode.ConfigType == EConfigType.WireGuard)
                {
                    var nextEndpoint = JsonUtils.Deserialize<Endpoints4Sbox>(txtOutbound);
                    await GenEndpoint(nextNode, nextEndpoint);
                    nextEndpoint.tag = Global.ProxyTag;
                    singboxConfig.endpoints.Insert(0, nextEndpoint);

                    nextEndpoint.detour = tag;
                }
                else
                {
                    var nextOutbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                    await GenOutbound(nextNode, nextOutbound);
                    nextOutbound.tag = Global.ProxyTag;
                    singboxConfig.outbounds.Insert(0, nextOutbound);

                    nextOutbound.detour = tag;
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return 0;
    }

    private async Task<int> GenRouting(SingboxConfig singboxConfig)
    {
        try
        {
            if (_config.TunModeItem.EnableTun)
            {
                singboxConfig.route.auto_detect_interface = true;

                var tunRules = JsonUtils.Deserialize<List<Rule4Sbox>>(EmbedUtils.GetEmbedText(Global.TunSingboxRulesFileName));
                if (tunRules != null)
                {
                    singboxConfig.route.rules.AddRange(tunRules);
                }

                GenRoutingDirectExe(out List<string> lstDnsExe, out List<string> lstDirectExe);
                singboxConfig.route.rules.Add(new()
                {
                    port = new() { 53 },
                    action = "hijack-dns",
                    process_name = lstDnsExe
                });

                singboxConfig.route.rules.Add(new()
                {
                    outbound = Global.DirectTag,
                    process_name = lstDirectExe
                });
            }

            if (_config.Inbound.First().SniffingEnabled)
            {
                singboxConfig.route.rules.Add(new()
                {
                    action = "sniff"
                });
                singboxConfig.route.rules.Add(new()
                {
                    protocol = new() { "dns" },
                    action = "hijack-dns"
                });
            }
            else
            {
                singboxConfig.route.rules.Add(new()
                {
                    port = new() { 53 },
                    network = new() { "udp" },
                    action = "hijack-dns"
                });
            }

            singboxConfig.route.rules.Add(new()
            {
                outbound = Global.DirectTag,
                clash_mode = ERuleMode.Direct.ToString()
            });
            singboxConfig.route.rules.Add(new()
            {
                outbound = Global.ProxyTag,
                clash_mode = ERuleMode.Global.ToString()
            });

            if (!(_config.Inbound.First().RouteOnly || _config.TunModeItem.EnableTun))
            {
                var domainStrategy = _config.RoutingBasicItem.DomainStrategy4Singbox.IsNullOrEmpty() ? null : _config.RoutingBasicItem.DomainStrategy4Singbox;
                var defaultRouting = await ConfigHandler.GetDefaultRouting(_config);
                if (defaultRouting.DomainStrategy4Singbox.IsNotEmpty())
                {
                    domainStrategy = defaultRouting.DomainStrategy4Singbox;
                }
                singboxConfig.route.rules.Add(new()
                {
                    action = "resolve",
                    strategy = domainStrategy
                });
            }

            var routing = await ConfigHandler.GetDefaultRouting(_config);
            if (routing != null)
            {
                var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
                foreach (var item in rules ?? [])
                {
                    if (item.Enabled)
                    {
                        await GenRoutingUserRule(item, singboxConfig.route.rules);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private void GenRoutingDirectExe(out List<string> lstDnsExe, out List<string> lstDirectExe)
    {
        lstDnsExe = new();
        lstDirectExe = new();
        var coreInfo = CoreInfoHandler.Instance.GetCoreInfo();
        foreach (var it in coreInfo)
        {
            if (it.CoreType == ECoreType.v2rayN)
            {
                continue;
            }
            foreach (var it2 in it.CoreExes)
            {
                if (!lstDnsExe.Contains(it2) && it.CoreType != ECoreType.sing_box)
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

    private async Task<int> GenRoutingUserRule(RulesItem item, List<Rule4Sbox> rules)
    {
        try
        {
            if (item == null)
            {
                return 0;
            }

            var rule = new Rule4Sbox();
            if (item.OutboundTag == "block")
            {
                rule.action = "reject";
            }
            else
            {
                rule.outbound = item.OutboundTag;
            }

            if (item.Port.IsNotEmpty())
            {
                if (item.Port.Contains("-"))
                {
                    rule.port_range = new List<string> { item.Port.Replace("-", ":") };
                }
                else
                {
                    rule.port = new List<int> { item.Port.ToInt() };
                }
            }
            if (item.Network.IsNotEmpty())
            {
                rule.network = Utils.String2List(item.Network);
            }
            if (item.Protocol?.Count > 0)
            {
                rule.protocol = item.Protocol;
            }
            if (item.InboundTag?.Count >= 0)
            {
                rule.inbound = item.InboundTag;
            }
            var rule1 = JsonUtils.DeepCopy(rule);
            var rule2 = JsonUtils.DeepCopy(rule);
            var rule3 = JsonUtils.DeepCopy(rule);

            var hasDomainIp = false;
            if (item.Domain?.Count > 0)
            {
                var countDomain = 0;
                foreach (var it in item.Domain)
                {
                    if (ParseV2Domain(it, rule1))
                        countDomain++;
                }
                if (countDomain > 0)
                {
                    rules.Add(rule1);
                    hasDomainIp = true;
                }
            }

            if (item.Ip?.Count > 0)
            {
                var countIp = 0;
                foreach (var it in item.Ip)
                {
                    if (ParseV2Address(it, rule2))
                        countIp++;
                }
                if (countIp > 0)
                {
                    rules.Add(rule2);
                    hasDomainIp = true;
                }
            }

            if (_config.TunModeItem.EnableTun && item.Process?.Count > 0)
            {
                rule3.process_name = item.Process;
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
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
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
        else if (address.Equals("geoip:private"))
        {
            rule.ip_is_private = true;
        }
        else if (address.StartsWith("geoip:"))
        {
            rule.geoip ??= new();
            rule.geoip?.Add(address.Substring(6));
        }
        else if (address.Equals("geoip:!private"))
        {
            rule.ip_is_private = false;
        }
        else if (address.StartsWith("geoip:!"))
        {
            rule.geoip ??= new();
            rule.geoip?.Add(address.Substring(6));
            rule.invert = true;
        }
        else
        {
            rule.ip_cidr ??= new();
            rule.ip_cidr?.Add(address);
        }
        return true;
    }

    private async Task<int> GenDns(ProfileItem? node, SingboxConfig singboxConfig)
    {
        try
        {
            var item = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
            var strDNS = string.Empty;
            if (_config.TunModeItem.EnableTun)
            {
                strDNS = string.IsNullOrEmpty(item?.TunDNS) ? EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName) : item?.TunDNS;
            }
            else
            {
                strDNS = string.IsNullOrEmpty(item?.NormalDNS) ? EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName) : item?.NormalDNS;
            }

            var dns4Sbox = JsonUtils.Deserialize<Dns4Sbox>(strDNS);
            if (dns4Sbox is null)
            {
                return 0;
            }
            singboxConfig.dns = dns4Sbox;

            await GenDnsDomains(node, singboxConfig, item);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsDomains(ProfileItem? node, SingboxConfig singboxConfig, DNSItem? dNSItem)
    {
        var dns4Sbox = singboxConfig.dns ?? new();
        dns4Sbox.servers ??= [];
        dns4Sbox.rules ??= [];

        var tag = "local_local";
        var localDnsAddress = string.IsNullOrEmpty(dNSItem?.DomainDNSAddress) ? Global.SingboxDomainDNSAddress.FirstOrDefault() : dNSItem?.DomainDNSAddress;
        string? localDnsType = null;
        string? dhcpDnsInterface = null;
        if (localDnsAddress == "local")
        {
            localDnsType = "local";
            localDnsAddress = null;
        }
        else if (localDnsAddress.StartsWith("dhcp") && localDnsAddress.Length > 7)
        {
            localDnsType = "dhcp";
            // dhcp://
            dhcpDnsInterface = localDnsAddress.Substring(7);
            if (dhcpDnsInterface == "auto")
            {
                dhcpDnsInterface = null;
            }
            localDnsAddress = null;
        }
        else if (localDnsAddress.StartsWith("tcp") && localDnsAddress.Length > 6)
        {
            localDnsType = "tcp";
            // tcp://
            localDnsAddress = localDnsAddress.Substring(6);
        }
        else if (localDnsAddress.StartsWith("tls") && localDnsAddress.Length > 6)
        {
            localDnsType = "tls";
            // tls://
            localDnsAddress = localDnsAddress.Substring(6);
        }
        else if (localDnsAddress.StartsWith("https") && localDnsAddress.Length > 8)
        {
            localDnsType = "https";
            // https://
            localDnsAddress = localDnsAddress.Substring(8);
        }
        else if (localDnsAddress.StartsWith("quic") && localDnsAddress.Length > 7)
        {
            localDnsType = "quic";
            // quic://
            localDnsAddress = localDnsAddress.Substring(7);
        }
        else
        {
            localDnsType = "udp";
        }

        dns4Sbox.servers.Add(new()
        {
            tag = tag,
            type = localDnsType,
            server = localDnsAddress,
            Interface = dhcpDnsInterface
        });
        dns4Sbox.rules.Insert(0, new()
        {
            server = tag,
            clash_mode = ERuleMode.Direct.ToString(),
            strategy = string.IsNullOrEmpty(dNSItem?.DomainStrategy4Freedom) ? null : dNSItem?.DomainStrategy4Freedom
        });
        dns4Sbox.rules.Insert(0, new()
        {
            server = dns4Sbox.servers.Where(t => t.detour == Global.ProxyTag).Select(t => t.tag).FirstOrDefault() ?? "remote",
            clash_mode = ERuleMode.Global.ToString()
        });

        //Tun2SocksAddress
        if (_config.TunModeItem.EnableTun && node?.ConfigType == EConfigType.SOCKS && Utils.IsDomain(node?.Sni))
        {
            dns4Sbox.rules.Insert(0, new()
            {
                server = tag,
                domain = [node?.Sni],
                strategy = string.IsNullOrEmpty(dNSItem?.DomainStrategy4Freedom) ? null : dNSItem?.DomainStrategy4Freedom
            });
        }

        singboxConfig.dns = dns4Sbox;
        return await Task.FromResult(0);
    }

    private async Task<int> GenExperimental(SingboxConfig singboxConfig)
    {
        //if (_config.guiItem.enableStatistics)
        {
            singboxConfig.experimental ??= new Experimental4Sbox();
            singboxConfig.experimental.clash_api = new Clash_Api4Sbox()
            {
                external_controller = $"{Global.Loopback}:{AppHandler.Instance.StatePort2}",
            };
        }

        if (_config.CoreBasicItem.EnableCacheFile4Sbox)
        {
            singboxConfig.experimental ??= new Experimental4Sbox();
            singboxConfig.experimental.cache_file = new CacheFile4Sbox()
            {
                enabled = true,
                path = Utils.GetBinPath("cache.db")
            };
        }

        return await Task.FromResult(0);
    }

    private async Task<int> ConvertGeo2Ruleset(SingboxConfig singboxConfig)
    {
        static void AddRuleSets(List<string> ruleSets, List<string>? rule_set)
        {
            if (rule_set != null)
                ruleSets.AddRange(rule_set);
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

        var routing = await ConfigHandler.GetDefaultRouting(_config);
        if (routing.CustomRulesetPath4Singbox.IsNotEmpty())
        {
            var result = EmbedUtils.LoadResource(routing.CustomRulesetPath4Singbox);
            if (result.IsNotEmpty())
            {
                customRulesets = (JsonUtils.Deserialize<List<Ruleset4Sbox>>(result) ?? [])
                    .Where(t => t.tag != null)
                    .Where(t => t.type != null)
                    .Where(t => t.format != null)
                    .ToList();
            }
        }

        //Local srs files address
        var localSrss = Utils.GetBinPath("srss");

        //Add ruleset srs
        singboxConfig.route.rule_set = [];
        foreach (var item in new HashSet<string>(ruleSets))
        {
            if (item.IsNullOrEmpty())
            { continue; }
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
                    var srsUrl = string.IsNullOrEmpty(_config.ConstItem.SrsSourceUrl)
                        ? Global.SingboxRulesetUrl
                        : _config.ConstItem.SrsSourceUrl;

                    customRuleset = new()
                    {
                        type = "remote",
                        format = "binary",
                        tag = item,
                        url = string.Format(srsUrl, item.StartsWith(geosite) ? geosite : geoip, item),
                        download_detour = Global.ProxyTag
                    };
                }
            }
            singboxConfig.route.rule_set.Add(customRuleset);
        }

        return 0;
    }

    #endregion private gen function
}
