namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenOutbounds()
    {
        var proxyOutboundList = BuildAllProxyOutbounds();
        _coreConfig.outbounds.InsertRange(0, proxyOutboundList);
        if (proxyOutboundList.Count(n => n.tag.StartsWith(Global.ProxyTag)) > 1)
        {
            var multipleLoad = context.Node.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
            GenObservatory(multipleLoad);
            GenBalancer(multipleLoad);
        }
    }

    private List<Outbounds4Ray> BuildAllProxyOutbounds(string baseTagName = Global.ProxyTag)
    {
        var proxyOutboundList = new List<Outbounds4Ray>();
        var node = context.Node;
        if (node.ConfigType.IsGroupType())
        {
            proxyOutboundList.AddRange(BuildGroupProxyOutbounds(baseTagName));
        }
        else
        {
            proxyOutboundList.Add(BuildProxyOutbound(baseTagName));
        }

        if (context.AppConfig.CoreBasicItem.EnableFragment)
        {
            var fragmentOutbound = new Outbounds4Ray
            {
                protocol = "freedom",
                tag = $"frag-{Global.ProxyTag}",
                settings = new()
                {
                    fragment = new()
                    {
                        packets = context.AppConfig.Fragment4RayItem?.Packets,
                        length = context.AppConfig.Fragment4RayItem?.Length,
                        interval = context.AppConfig.Fragment4RayItem?.Interval
                    }
                }
            };
            var actOutboundWithTlsList =
                proxyOutboundList.Where(n => n.streamSettings?.security.IsNullOrEmpty() == false
                                             && (n.streamSettings?.sockopt?.dialerProxy?.IsNullOrEmpty() ?? true));
            foreach (var outbound in actOutboundWithTlsList)
            {
                var fragmentOutboundClone = JsonUtils.DeepCopy(fragmentOutbound);
                fragmentOutboundClone.tag = $"frag-{outbound.tag}";
                outbound.streamSettings.sockopt = new()
                {
                    dialerProxy = fragmentOutboundClone.tag
                };
                proxyOutboundList.Add(fragmentOutboundClone);
            }
        }
        return proxyOutboundList;
    }

    private List<Outbounds4Ray> BuildGroupProxyOutbounds(string baseTagName = Global.ProxyTag)
    {
        var proxyOutboundList = new List<Outbounds4Ray>();
        var node = context.Node;
        switch (node.ConfigType)
        {
            case EConfigType.PolicyGroup:
                proxyOutboundList.AddRange(BuildOutboundsList(baseTagName));
                break;
            case EConfigType.ProxyChain:
                proxyOutboundList.AddRange(BuildChainOutboundsList(baseTagName));
                break;
        }
        return proxyOutboundList;
    }

    private Outbounds4Ray BuildProxyOutbound(string baseTagName = Global.ProxyTag)
    {
        var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
        var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
        FillOutbound(outbound);
        outbound.tag = baseTagName;
        return outbound;
    }

    private void FillOutbound(Outbounds4Ray outbound)
    {
        try
        {
            var node = context.Node;
            var protocolExtra = node.GetProtocolExtra();
            var muxEnabled = node.MuxEnabled ?? context.AppConfig.CoreBasicItem.MuxEnabled;
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

                        usersItem.id = node.Password;
                        usersItem.alterId = int.TryParse(protocolExtra?.AlterId, out var result) ? result : 0;
                        usersItem.email = Global.UserEMail;
                        if (Global.VmessSecurities.Contains(protocolExtra.VmessSecurity))
                        {
                            usersItem.security = protocolExtra.VmessSecurity;
                        }
                        else
                        {
                            usersItem.security = Global.DefaultSecurity;
                        }

                        FillOutboundMux(outbound, muxEnabled, muxEnabled);

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
                        serversItem.password = node.Password;
                        serversItem.method = AppManager.Instance.GetShadowsocksSecurities(node).Contains(protocolExtra.SsMethod)
                            ? protocolExtra.SsMethod : "none";

                        serversItem.ota = false;
                        serversItem.level = 1;

                        FillOutboundMux(outbound);

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

                        if (node.Username.IsNotEmpty()
                            && node.Password.IsNotEmpty())
                        {
                            SocksUsersItem4Ray socksUsersItem = new()
                            {
                                user = node.Username ?? "",
                                pass = node.Password,
                                level = 1
                            };

                            serversItem.users = new List<SocksUsersItem4Ray>() { socksUsersItem };
                        }

                        FillOutboundMux(outbound);

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
                        usersItem.id = node.Password;
                        usersItem.email = Global.UserEMail;
                        usersItem.encryption = protocolExtra.VlessEncryption;

                        if (!protocolExtra.Flow.IsNullOrEmpty())
                        {
                            usersItem.flow = protocolExtra.Flow;
                        }
                        else
                        {
                            FillOutboundMux(outbound, false, muxEnabled);
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
                        serversItem.password = node.Password;

                        serversItem.ota = false;
                        serversItem.level = 1;

                        FillOutboundMux(outbound);

                        outbound.settings.vnext = null;
                        break;
                    }
                case EConfigType.Hysteria2:
                    {
                        outbound.settings = new()
                        {
                            version = 2,
                            address = node.Address,
                            port = node.Port,
                        };
                        outbound.settings.vnext = null;
                        outbound.settings.servers = null;
                        break;
                    }
                case EConfigType.WireGuard:
                    {
                        var address = node.Address;
                        if (Utils.IsIpv6(address))
                        {
                            address = $"[{address}]";
                        }
                        var peer = new WireguardPeer4Ray
                        {
                            publicKey = protocolExtra.WgPublicKey ?? "",
                            endpoint = address + ":" + node.Port.ToString()
                        };
                        var setting = new Outboundsettings4Ray
                        {
                            address = Utils.String2List(protocolExtra.WgInterfaceAddress),
                            secretKey = node.Password,
                            reserved = Utils.String2List(protocolExtra.WgReserved)?.Select(int.Parse).ToList(),
                            mtu = protocolExtra.WgMtu > 0 ? protocolExtra.WgMtu : Global.TunMtus.First(),
                            peers = [peer]
                        };
                        outbound.settings = setting;
                        outbound.settings.vnext = null;
                        outbound.settings.servers = null;
                        break;
                    }
            }

            outbound.protocol = Global.ProtocolTypes[node.ConfigType];
            if (node.ConfigType == EConfigType.Hysteria2)
            {
                outbound.protocol = "hysteria";
            }
            FillBoundStreamSettings(outbound);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void FillOutboundMux(Outbounds4Ray outbound, bool enabledTCP = false, bool enabledUDP = false)
    {
        try
        {
            outbound.mux.enabled = false;
            outbound.mux.concurrency = -1;

            if (enabledTCP)
            {
                outbound.mux.enabled = true;
                outbound.mux.concurrency = context.AppConfig.Mux4RayItem.Concurrency;
            }
            else if (enabledUDP)
            {
                outbound.mux.enabled = true;
                outbound.mux.xudpConcurrency = context.AppConfig.Mux4RayItem.XudpConcurrency;
                outbound.mux.xudpProxyUDP443 = context.AppConfig.Mux4RayItem.XudpProxyUDP443;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void FillBoundStreamSettings(Outbounds4Ray outbound)
    {
        try
        {
            var node = context.Node;
            var config = context.AppConfig;
            var streamSettings = outbound.streamSettings;
            var network = node.GetNetwork();
            if (node.ConfigType == EConfigType.Hysteria2)
            {
                network = "hysteria";
            }
            streamSettings.network = network;
            var host = node.RequestHost.TrimEx();
            var path = node.Path.TrimEx();
            var sni = node.Sni.TrimEx();
            var useragent = "";
            if (!config.CoreBasicItem.DefUserAgent.IsNullOrEmpty())
            {
                try
                {
                    useragent = Global.UserAgentTexts[config.CoreBasicItem.DefUserAgent];
                }
                catch (KeyNotFoundException)
                {
                    useragent = config.CoreBasicItem.DefUserAgent;
                }
            }

            //if tls
            if (node.StreamSecurity == Global.StreamSecurity)
            {
                streamSettings.security = node.StreamSecurity;

                TlsSettings4Ray tlsSettings = new()
                {
                    allowInsecure = Utils.ToBool(node.AllowInsecure.IsNullOrEmpty() ? config.CoreBasicItem.DefAllowInsecure.ToString().ToLower() : node.AllowInsecure),
                    alpn = node.GetAlpn(),
                    fingerprint = node.Fingerprint.IsNullOrEmpty() ? config.CoreBasicItem.DefFingerprint : node.Fingerprint,
                    echConfigList = node.EchConfigList.NullIfEmpty(),
                    echForceQuery = node.EchForceQuery.NullIfEmpty()
                };
                if (sni.IsNotEmpty())
                {
                    tlsSettings.serverName = sni;
                }
                else if (host.IsNotEmpty())
                {
                    tlsSettings.serverName = Utils.String2List(host)?.First();
                }
                var certs = CertPemManager.ParsePemChain(node.Cert);
                if (certs.Count > 0)
                {
                    var certsettings = new List<CertificateSettings4Ray>();
                    foreach (var cert in certs)
                    {
                        var certPerLine = cert.Split("\n").ToList();
                        certsettings.Add(new CertificateSettings4Ray
                        {
                            certificate = certPerLine,
                            usage = "verify",
                        });
                    }
                    tlsSettings.certificates = certsettings;
                    tlsSettings.disableSystemRoot = true;
                    tlsSettings.allowInsecure = false;
                }
                else if (!node.CertSha.IsNullOrEmpty())
                {
                    tlsSettings.pinnedPeerCertSha256 = node.CertSha;
                    tlsSettings.allowInsecure = false;
                }
                streamSettings.tlsSettings = tlsSettings;
            }

            //if Reality
            if (node.StreamSecurity == Global.StreamSecurityReality)
            {
                streamSettings.security = node.StreamSecurity;

                TlsSettings4Ray realitySettings = new()
                {
                    fingerprint = node.Fingerprint.IsNullOrEmpty() ? config.CoreBasicItem.DefFingerprint : node.Fingerprint,
                    serverName = sni,
                    publicKey = node.PublicKey,
                    shortId = node.ShortId,
                    spiderX = node.SpiderX,
                    mldsa65Verify = node.Mldsa65Verify,
                    show = false,
                };

                streamSettings.realitySettings = realitySettings;
            }

            //streamSettings
            switch (network)
            {
                case nameof(ETransport.kcp):
                    KcpSettings4Ray kcpSettings = new()
                    {
                        mtu = config.KcpItem.Mtu,
                        tti = config.KcpItem.Tti
                    };

                    kcpSettings.uplinkCapacity = config.KcpItem.UplinkCapacity;
                    kcpSettings.downlinkCapacity = config.KcpItem.DownlinkCapacity;

                    kcpSettings.congestion = config.KcpItem.Congestion;
                    kcpSettings.readBufferSize = config.KcpItem.ReadBufferSize;
                    kcpSettings.writeBufferSize = config.KcpItem.WriteBufferSize;
                    streamSettings.finalmask ??= new();
                    if (Global.KcpHeaderMaskMap.TryGetValue(node.HeaderType, out var header))
                    {
                        streamSettings.finalmask.udp =
                        [
                            new Mask4Ray
                            {
                                type = header,
                                settings = node.HeaderType == "dns" && !host.IsNullOrEmpty() ? new MaskSettings4Ray { domain = host } : null
                            }
                        ];
                    }
                    streamSettings.finalmask.udp ??= [];
                    if (path.IsNullOrEmpty())
                    {
                        streamSettings.finalmask.udp.Add(new Mask4Ray
                        {
                            type = "mkcp-original"
                        });
                    }
                    else
                    {
                        streamSettings.finalmask.udp.Add(new Mask4Ray
                        {
                            type = "mkcp-aes128gcm",
                            settings = new MaskSettings4Ray { password = path }
                        });
                    }
                    streamSettings.kcpSettings = kcpSettings;
                    break;
                //ws
                case nameof(ETransport.ws):
                    WsSettings4Ray wsSettings = new();
                    wsSettings.headers = new Headers4Ray();

                    if (host.IsNotEmpty())
                    {
                        wsSettings.host = host;
                    }
                    if (path.IsNotEmpty())
                    {
                        wsSettings.path = path;
                    }
                    if (useragent.IsNotEmpty())
                    {
                        wsSettings.headers.UserAgent = useragent;
                    }
                    streamSettings.wsSettings = wsSettings;

                    break;
                //httpupgrade
                case nameof(ETransport.httpupgrade):
                    HttpupgradeSettings4Ray httpupgradeSettings = new();

                    if (path.IsNotEmpty())
                    {
                        httpupgradeSettings.path = path;
                    }
                    if (host.IsNotEmpty())
                    {
                        httpupgradeSettings.host = host;
                    }
                    streamSettings.httpupgradeSettings = httpupgradeSettings;

                    break;
                //xhttp
                case nameof(ETransport.xhttp):
                    streamSettings.network = ETransport.xhttp.ToString();
                    XhttpSettings4Ray xhttpSettings = new();

                    if (path.IsNotEmpty())
                    {
                        xhttpSettings.path = path;
                    }
                    if (host.IsNotEmpty())
                    {
                        xhttpSettings.host = host;
                    }
                    if (node.HeaderType.IsNotEmpty() && Global.XhttpMode.Contains(node.HeaderType))
                    {
                        xhttpSettings.mode = node.HeaderType;
                    }
                    if (node.Extra.IsNotEmpty())
                    {
                        xhttpSettings.extra = JsonUtils.ParseJson(node.Extra);
                    }

                    streamSettings.xhttpSettings = xhttpSettings;
                    FillOutboundMux(outbound);

                    break;
                //h2
                case nameof(ETransport.h2):
                    HttpSettings4Ray httpSettings = new();

                    if (host.IsNotEmpty())
                    {
                        httpSettings.host = Utils.String2List(host);
                    }
                    httpSettings.path = path;

                    streamSettings.httpSettings = httpSettings;

                    break;
                //quic
                case nameof(ETransport.quic):
                    QuicSettings4Ray quicsettings = new()
                    {
                        security = host,
                        key = path,
                        header = new Header4Ray
                        {
                            type = node.HeaderType
                        }
                    };
                    streamSettings.quicSettings = quicsettings;
                    if (node.StreamSecurity == Global.StreamSecurity)
                    {
                        if (sni.IsNotEmpty())
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
                        authority = host.NullIfEmpty(),
                        serviceName = path,
                        multiMode = node.HeaderType == Global.GrpcMultiMode,
                        idle_timeout = config.GrpcItem.IdleTimeout,
                        health_check_timeout = config.GrpcItem.HealthCheckTimeout,
                        permit_without_stream = config.GrpcItem.PermitWithoutStream,
                        initial_windows_size = config.GrpcItem.InitialWindowsSize,
                    };
                    streamSettings.grpcSettings = grpcSettings;
                    break;

                case "hysteria":
                    var protocolExtra = node.GetProtocolExtra();
                    var ports = protocolExtra?.Ports;
                    int? upMbps = protocolExtra?.UpMbps is { } su and >= 0
                        ? su
                        : config.HysteriaItem.UpMbps;
                    int? downMbps = protocolExtra?.DownMbps is { } sd and >= 0
                        ? sd
                        : config.HysteriaItem.UpMbps;
                    var hopInterval = !protocolExtra.HopInterval.IsNullOrEmpty()
                        ? protocolExtra.HopInterval
                        : (config.HysteriaItem.HopInterval >= 5
                            ? config.HysteriaItem.HopInterval
                            : Global.Hysteria2DefaultHopInt).ToString();
                    HysteriaUdpHop4Ray? udpHop = null;
                    if (!ports.IsNullOrEmpty() &&
                        (ports.Contains(':') || ports.Contains('-') || ports.Contains(',')))
                    {
                        udpHop = new HysteriaUdpHop4Ray
                        {
                            ports = ports.Replace(':', '-'),
                            interval = hopInterval,
                        };
                    }
                    streamSettings.hysteriaSettings = new()
                    {
                        version = 2,
                        auth = node.Password,
                        up = upMbps > 0 ? $"{upMbps}mbps" : null,
                        down = downMbps > 0 ? $"{downMbps}mbps" : null,
                        udphop = udpHop,
                    };
                    if (!protocolExtra.SalamanderPass.IsNullOrEmpty())
                    {
                        streamSettings.finalmask ??= new();
                        streamSettings.finalmask.udp =
                        [
                            new Mask4Ray
                            {
                                type = "salamander",
                                settings = new MaskSettings4Ray { password = protocolExtra.SalamanderPass.TrimEx(), }
                            }
                        ];
                    }
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
                        var request = EmbedUtils.GetEmbedText(Global.V2raySampleHttpRequestFileName);
                        var arrHost = host.Split(',');
                        var host2 = string.Join(",".AppendQuotes(), arrHost);
                        request = request.Replace("$requestHost$", $"{host2.AppendQuotes()}");
                        request = request.Replace("$requestUserAgent$", $"{useragent.AppendQuotes()}");
                        //Path
                        var pathHttp = @"/";
                        if (path.IsNotEmpty())
                        {
                            var arrPath = path.Split(',');
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
            Logging.SaveLog(_tag, ex);
        }
    }

    private List<Outbounds4Ray> BuildOutboundsList(string baseTagName = Global.ProxyTag)
    {
        var nodes = new List<ProfileItem>();
        foreach (var nodeId in Utils.String2List(context.Node.GetProtocolExtra().ChildItems) ?? [])
        {
            if (context.AllProxiesMap.TryGetValue(nodeId, out var node))
            {
                nodes.Add(node);
            }
        }
        var resultOutbounds = new List<Outbounds4Ray>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var currentTag = $"{baseTagName}-{i + 1}";

            if (node.ConfigType.IsGroupType())
            {
                var childProfiles = new CoreConfigV2rayService(context with { Node = node, }).BuildGroupProxyOutbounds(currentTag);
                resultOutbounds.AddRange(childProfiles);
                continue;
            }
            var outbound = new CoreConfigV2rayService(context with { Node = node, }).BuildProxyOutbound();
            outbound.tag = currentTag;
            resultOutbounds.Add(outbound);
        }
        return resultOutbounds;
    }

    private List<Outbounds4Ray> BuildChainOutboundsList(string baseTagName = Global.ProxyTag)
    {
        var nodes = new List<ProfileItem>();
        foreach (var nodeId in Utils.String2List(context.Node.GetProtocolExtra().ChildItems) ?? [])
        {
            if (context.AllProxiesMap.TryGetValue(nodeId, out var node))
            {
                nodes.Add(node);
            }
        }
        // Based on actual network flow instead of data packets
        var nodesReverse = nodes.AsEnumerable().Reverse().ToList();
        var resultOutbounds = new List<Outbounds4Ray>();
        for (var i = 0; i < nodesReverse.Count; i++)
        {
            var node = nodesReverse[i];
            var currentTag = i == 0 ? baseTagName : $"chain-{baseTagName}-{i}";
            var dialerProxyTag = i != nodesReverse.Count - 1 ? $"chain-{baseTagName}-{i + 1}" : null;
            if (node.ConfigType.IsGroupType())
            {
                var childProfiles = new CoreConfigV2rayService(context with { Node = node, }).BuildGroupProxyOutbounds(currentTag);
                if (!dialerProxyTag.IsNullOrEmpty())
                {
                    var chainEndNodes =
                        childProfiles.Where(n => n?.streamSettings?.sockopt?.dialerProxy?.IsNullOrEmpty() ?? true);
                    foreach (var chainEndNode in chainEndNodes)
                    {
                        chainEndNode.streamSettings.sockopt = new()
                        {
                            dialerProxy = dialerProxyTag
                        };
                    }
                }
                if (i != 0)
                {
                    var chainStartNodes = childProfiles.Where(n => n.tag.StartsWith(currentTag)).ToList();
                    var existedChainNodes = JsonUtils.DeepCopy(resultOutbounds);
                    resultOutbounds.Clear();
                    foreach (var chainStartNode in chainStartNodes)
                    {
                        var existedChainNodesClone = JsonUtils.DeepCopy(existedChainNodes);
                        for (var j = 0; j < existedChainNodesClone.Count; j++)
                        {
                            var existedChainNode = existedChainNodesClone[j];
                            var cloneTag = $"{existedChainNode.tag}-clone-{j + 1}";
                            existedChainNode.tag = cloneTag;
                            var previousDialerProxyTag = existedChainNode.streamSettings?.sockopt?.dialerProxy;
                            existedChainNode.streamSettings.sockopt = new()
                            {
                                dialerProxy = (previousDialerProxyTag == currentTag) ? chainStartNode.tag : existedChainNodesClone[j + 1].tag
                            };
                            resultOutbounds.Add(existedChainNode);
                        }
                    }
                }
                resultOutbounds.AddRange(childProfiles);
                continue;
            }
            var outbound = new CoreConfigV2rayService(context with { Node = node, }).BuildProxyOutbound();

            outbound.tag = currentTag;

            if (!dialerProxyTag.IsNullOrEmpty())
            {
                outbound.streamSettings.sockopt = new()
                {
                    dialerProxy = dialerProxyTag
                };
            }

            resultOutbounds.Add(outbound);
        }
        return resultOutbounds;
    }
}
