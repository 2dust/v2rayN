namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenOutbounds()
    {
        var proxyOutboundList = BuildAllProxyOutbounds();
        _coreConfig.outbounds.InsertRange(0, proxyOutboundList);
        if (proxyOutboundList.Count(n => n.tag.StartsWith(Global.ProxyTag)) > 1)
        {
            var multipleLoad = _node.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
            GenObservatory(multipleLoad);
            GenBalancer(multipleLoad);
        }
        if (context.IsTunEnabled)
        {
            _coreConfig.outbounds.Add(BuildDnsOutbound());
        }
    }

    private List<Outbounds4Ray> BuildAllProxyOutbounds(string baseTagName = Global.ProxyTag)
    {
        var proxyOutboundList = new List<Outbounds4Ray>();
        if (_node.ConfigType.IsGroupType())
        {
            proxyOutboundList.AddRange(BuildGroupProxyOutbounds(baseTagName));
        }
        else
        {
            proxyOutboundList.Add(BuildProxyOutbound(baseTagName));
        }
        return proxyOutboundList;
    }

    private List<Outbounds4Ray> BuildGroupProxyOutbounds(string baseTagName = Global.ProxyTag)
    {
        var proxyOutboundList = new List<Outbounds4Ray>();
        switch (_node.ConfigType)
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
            var protocolExtra = _node.GetProtocolExtra();
            var muxEnabled = _node.MuxEnabled ?? _config.CoreBasicItem.MuxEnabled;
            switch (_node.ConfigType)
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
                        vnextItem.address = _node.Address;
                        vnextItem.port = _node.Port;

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

                        usersItem.id = _node.Password;
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
                        serversItem.address = _node.Address;
                        serversItem.port = _node.Port;
                        serversItem.password = _node.Password;
                        serversItem.method = AppManager.Instance.GetShadowsocksSecurities(_node).Contains(protocolExtra.SsMethod)
                            ? protocolExtra.SsMethod : "none";
                        serversItem.uot = protocolExtra.Uot == true ? true : null;

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
                        serversItem.address = _node.Address;
                        serversItem.port = _node.Port;
                        serversItem.method = null;
                        serversItem.password = null;

                        if (_node.Username.IsNotEmpty()
                            && _node.Password.IsNotEmpty())
                        {
                            SocksUsersItem4Ray socksUsersItem = new()
                            {
                                user = _node.Username ?? "",
                                pass = _node.Password,
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
                        vnextItem.address = _node.Address;
                        vnextItem.port = _node.Port;

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
                        usersItem.id = _node.Password;
                        usersItem.email = Global.UserEMail;
                        usersItem.encryption = protocolExtra.VlessEncryption;

                        if (protocolExtra.Flow.IsNullOrEmpty())
                        {
                            FillOutboundMux(outbound, muxEnabled, muxEnabled);
                        }
                        else
                        {
                            usersItem.flow = protocolExtra.Flow;
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
                        serversItem.address = _node.Address;
                        serversItem.port = _node.Port;
                        serversItem.password = _node.Password;

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
                            address = _node.Address,
                            port = _node.Port,
                        };
                        outbound.settings.vnext = null;
                        outbound.settings.servers = null;
                        break;
                    }
                case EConfigType.WireGuard:
                    {
                        var address = _node.Address;
                        if (Utils.IsIpv6(address))
                        {
                            address = $"[{address}]";
                        }
                        var peer = new WireguardPeer4Ray
                        {
                            publicKey = protocolExtra.WgPublicKey ?? "",
                            endpoint = address + ":" + _node.Port.ToString()
                        };
                        var setting = new Outboundsettings4Ray
                        {
                            address = Utils.String2List(protocolExtra.WgInterfaceAddress),
                            secretKey = _node.Password,
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

            outbound.protocol = Global.ProtocolTypes[_node.ConfigType];
            if (_node.ConfigType == EConfigType.Hysteria2)
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
                outbound.mux.concurrency = _config.Mux4RayItem.Concurrency;
            }
            else if (enabledUDP)
            {
                outbound.mux.enabled = true;
                outbound.mux.xudpConcurrency = _config.Mux4RayItem.XudpConcurrency;
                outbound.mux.xudpProxyUDP443 = _config.Mux4RayItem.XudpProxyUDP443;
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
            var streamSettings = outbound.streamSettings;
            var network = _node.GetNetwork();
            if (_node.ConfigType == EConfigType.Hysteria2)
            {
                network = "hysteria";
            }
            streamSettings.network = network;
            var transport = _node.GetTransportExtra();
            var host = string.Empty;
            var path = string.Empty;
            var kcpSeed = string.Empty;
            var kcpMtu = 0;
            var headerType = string.Empty;
            var xhttpExtra = string.Empty;
            switch (network)
            {
                case nameof(ETransport.raw):
                    host = transport.Host?.TrimEx() ?? string.Empty;
                    path = transport.Path?.TrimEx() ?? string.Empty;
                    headerType = transport.RawHeaderType?.TrimEx() ?? string.Empty;
                    break;

                case nameof(ETransport.kcp):
                    kcpSeed = transport.KcpSeed?.TrimEx() ?? string.Empty;
                    headerType = transport.KcpHeaderType?.TrimEx() ?? string.Empty;
                    kcpMtu = transport.KcpMtu > 0 ? transport.KcpMtu!.Value : _config.KcpItem.Mtu;
                    break;

                case nameof(ETransport.ws):
                    host = transport.Host?.TrimEx() ?? string.Empty;
                    path = transport.Path?.TrimEx() ?? string.Empty;
                    break;

                case nameof(ETransport.httpupgrade):
                    host = transport.Host?.TrimEx() ?? string.Empty;
                    path = transport.Path?.TrimEx() ?? string.Empty;
                    break;

                case nameof(ETransport.xhttp):
                    host = transport.Host?.TrimEx() ?? string.Empty;
                    path = transport.Path?.TrimEx() ?? string.Empty;
                    headerType = transport.XhttpMode?.TrimEx() ?? string.Empty;
                    xhttpExtra = transport.XhttpExtra?.TrimEx() ?? string.Empty;
                    break;

                case nameof(ETransport.grpc):
                    host = transport.GrpcAuthority?.TrimEx() ?? string.Empty;
                    path = transport.GrpcServiceName?.TrimEx() ?? string.Empty;
                    headerType = transport.GrpcMode?.TrimEx() ?? string.Empty;
                    break;
            }

            var sni = _node.Sni.TrimEx();
            var useragent = _config.CoreBasicItem.DefUserAgent ?? string.Empty;

            //if tls
            if (_node.StreamSecurity == Global.StreamSecurity)
            {
                streamSettings.security = _node.StreamSecurity;

                TlsSettings4Ray tlsSettings = new()
                {
                    allowInsecure = Utils.ToBool(_node.AllowInsecure.IsNullOrEmpty() ? _config.CoreBasicItem.DefAllowInsecure.ToString().ToLower() : _node.AllowInsecure),
                    alpn = _node.GetAlpn(),
                    fingerprint = _node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : _node.Fingerprint,
                    echConfigList = _node.EchConfigList.NullIfEmpty(),
                    echForceQuery = _node.EchForceQuery.NullIfEmpty()
                };
                if (sni.IsNotEmpty())
                {
                    tlsSettings.serverName = sni;
                }
                else if (host.IsNotEmpty())
                {
                    tlsSettings.serverName = Utils.String2List(host)?.First();
                }
                var certs = CertPemManager.ParsePemChain(_node.Cert);
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
                else if (!_node.CertSha.IsNullOrEmpty())
                {
                    tlsSettings.pinnedPeerCertSha256 = _node.CertSha;
                    tlsSettings.allowInsecure = false;
                }
                streamSettings.tlsSettings = tlsSettings;
            }

            //if Reality
            if (_node.StreamSecurity == Global.StreamSecurityReality)
            {
                streamSettings.security = _node.StreamSecurity;

                TlsSettings4Ray realitySettings = new()
                {
                    fingerprint = _node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : _node.Fingerprint,
                    serverName = sni,
                    publicKey = _node.PublicKey,
                    shortId = _node.ShortId,
                    spiderX = _node.SpiderX,
                    mldsa65Verify = _node.Mldsa65Verify,
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
                        mtu = kcpMtu,
                        tti = _config.KcpItem.Tti
                    };

                    kcpSettings.uplinkCapacity = _config.KcpItem.UplinkCapacity;
                    kcpSettings.downlinkCapacity = _config.KcpItem.DownlinkCapacity;

                    kcpSettings.cwndMultiplier = _config.KcpItem.CwndMultiplier;
                    kcpSettings.maxSendingWindow = _config.KcpItem.MaxSendingWindow;
                    var kcpFinalmask = new Finalmask4Ray();
                    if (Global.KcpHeaderMaskMap.TryGetValue(headerType, out var header))
                    {
                        kcpFinalmask.udp =
                        [
                            new Mask4Ray
                            {
                                type = header,
                                settings = null
                            }
                        ];
                    }
                    kcpFinalmask.udp ??= [];
                    if (kcpSeed.IsNullOrEmpty())
                    {
                        kcpFinalmask.udp.Add(new Mask4Ray
                        {
                            type = "mkcp-original"
                        });
                    }
                    else
                    {
                        kcpFinalmask.udp.Add(new Mask4Ray
                        {
                            type = "mkcp-aes128gcm",
                            settings = new MaskSettings4Ray { password = kcpSeed }
                        });
                    }
                    streamSettings.kcpSettings = kcpSettings;
                    streamSettings.finalmask = kcpFinalmask;
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

                    if (host.IsNotEmpty())
                    {
                        httpupgradeSettings.host = host;
                    }
                    if (path.IsNotEmpty())
                    {
                        httpupgradeSettings.path = path;
                    }
                    if (useragent.IsNotEmpty())
                    {
                        httpupgradeSettings.headers.UserAgent = useragent;
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
                    if (headerType.IsNotEmpty() && Global.XhttpMode.Contains(headerType))
                    {
                        xhttpSettings.mode = headerType;
                    }
                    if (xhttpExtra.IsNotEmpty())
                    {
                        xhttpSettings.extra = JsonUtils.ParseJson(xhttpExtra);
                    }

                    streamSettings.xhttpSettings = xhttpSettings;
                    FillOutboundMux(outbound);

                    break;
                case nameof(ETransport.grpc):
                    GrpcSettings4Ray grpcSettings = new()
                    {
                        authority = host.NullIfEmpty(),
                        serviceName = path,
                        multiMode = headerType == Global.GrpcMultiMode,
                        idle_timeout = _config.GrpcItem.IdleTimeout,
                        health_check_timeout = _config.GrpcItem.HealthCheckTimeout,
                        permit_without_stream = _config.GrpcItem.PermitWithoutStream,
                        initial_windows_size = _config.GrpcItem.InitialWindowsSize,
                        user_agent = useragent.NullIfEmpty(),
                    };
                    streamSettings.grpcSettings = grpcSettings;
                    break;

                case "hysteria":
                    var protocolExtra = _node.GetProtocolExtra();
                    var ports = protocolExtra?.Ports;
                    int? upMbps = protocolExtra?.UpMbps is { } su and >= 0
                        ? su
                        : _config.HysteriaItem.UpMbps;
                    int? downMbps = protocolExtra?.DownMbps is { } sd and >= 0
                        ? sd
                        : _config.HysteriaItem.UpMbps;
                    var hopInterval = !protocolExtra.HopInterval.IsNullOrEmpty()
                        ? protocolExtra.HopInterval
                        : (_config.HysteriaItem.HopInterval >= 5
                            ? _config.HysteriaItem.HopInterval
                            : Global.Hysteria2DefaultHopInt).ToString();
                    var hy2Finalmask = new Finalmask4Ray();
                    var quicParams = new QuicParams4Ray();
                    if (!ports.IsNullOrEmpty() &&
                        (ports.Contains(':') || ports.Contains('-') || ports.Contains(',')))
                    {
                        var udpHop = new UdpHop4Ray
                        {
                            ports = ports.Replace(':', '-'),
                            interval = hopInterval,
                        };
                        quicParams.udpHop = udpHop;
                    }
                    if (upMbps > 0 || downMbps > 0)
                    {
                        quicParams.congestion = "brutal";
                        quicParams.brutalUp = upMbps > 0 ? $"{upMbps}mbps" : null;
                        quicParams.brutalDown = downMbps > 0 ? $"{downMbps}mbps" : null;
                    }
                    else
                    {
                        quicParams.congestion = "bbr";
                    }
                    hy2Finalmask.quicParams = quicParams;
                    if (!protocolExtra.SalamanderPass.IsNullOrEmpty())
                    {
                        hy2Finalmask.udp =
                            [
                                new Mask4Ray
                                {
                                    type = "salamander",
                                    settings = new MaskSettings4Ray { password = protocolExtra.SalamanderPass.TrimEx(), }
                                }
                            ];
                    }
                    streamSettings.hysteriaSettings = new()
                    {
                        version = 2,
                        auth = _node.Password,
                    };
                    streamSettings.finalmask = hy2Finalmask;
                    break;

                default:
                    // raw
                    if (headerType == Global.RawHeaderHttp)
                    {
                        RawSettings4Ray rawSettings = new()
                        {
                            header = new Header4Ray
                            {
                                type = headerType
                            }
                        };

                        //request Host
                        var request = EmbedUtils.GetEmbedText(Global.V2raySampleHttpRequestFileName);
                        var useragentValue = Global.RawHttpUserAgentTexts.GetValueOrDefault(useragent, useragent);
                        var arrHost = host.Split(',');
                        var host2 = string.Join(",".AppendQuotes(), arrHost);
                        request = request.Replace("$requestHost$", $"{host2.AppendQuotes()}");
                        request = request.Replace("$requestUserAgent$", $"{useragentValue.AppendQuotes()}");
                        //Path
                        var pathHttp = @"/";
                        if (path.IsNotEmpty())
                        {
                            var arrPath = path.Split(',');
                            pathHttp = string.Join(",".AppendQuotes(), arrPath);
                        }
                        request = request.Replace("$requestPath$", $"{pathHttp.AppendQuotes()}");
                        rawSettings.header.request = JsonUtils.Deserialize<object>(request);

                        streamSettings.rawSettings = rawSettings;
                    }
                    break;
            }

            if (!_node.Finalmask.IsNullOrEmpty())
            {
                streamSettings.finalmask = JsonUtils.ParseJson(_node.Finalmask);
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
        foreach (var nodeId in Utils.String2List(_node.GetProtocolExtra().ChildItems) ?? [])
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
            var currentTag = $"{baseTagName}-{i + 1}-{node.Remarks}";

            if (nodes.Count == 1)
            {
                currentTag = baseTagName;
            }

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
        foreach (var nodeId in Utils.String2List(_node.GetProtocolExtra().ChildItems) ?? [])
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
            var currentTag = i == 0 ? baseTagName : $"chain-{baseTagName}-{i}-{node.Remarks}";
            var dialerProxyTag = i != nodesReverse.Count - 1 ? $"chain-{baseTagName}-{i + 1}-{nodesReverse[i + 1].Remarks}" : null;
            if (node.ConfigType.IsGroupType())
            {
                var childProfiles = new CoreConfigV2rayService(context with { Node = node, }).BuildGroupProxyOutbounds(currentTag);
                if (!dialerProxyTag.IsNullOrEmpty())
                {
                    var chainEndNodes =
                        childProfiles.Where(n => n?.streamSettings?.sockopt?.dialerProxy?.IsNullOrEmpty() ?? true);
                    foreach (var chainEndNode in chainEndNodes)
                    {
                        FillDialerProxy(chainEndNode, dialerProxyTag);
                    }
                }
                if (i != 0)
                {
                    var chainStartNodes = childProfiles.Where(n => n.tag.StartsWith(currentTag)).ToList();
                    if (chainStartNodes.Count == 1)
                    {
                        var firstChainTag = chainStartNodes.First().tag;
                        foreach (var existedChainEndNode in resultOutbounds.Where(n => n.streamSettings?.sockopt?.dialerProxy == currentTag))
                        {
                            FillDialerProxy(existedChainEndNode, firstChainTag);
                        }
                    }
                    else if (chainStartNodes.Count > 1)
                    {
                        var existedChainNodes = JsonUtils.DeepCopy(resultOutbounds);
                        resultOutbounds.Clear();
                        var j = 0;
                        foreach (var chainStartNode in chainStartNodes)
                        {
                            var existedChainNodesClone = JsonUtils.DeepCopy(existedChainNodes);
                            foreach (var existedChainNode in existedChainNodesClone)
                            {
                                var cloneTag = $"{existedChainNode.tag}-clone-{j + 1}";
                                existedChainNode.tag = cloneTag;
                            }
                            for (var k = 0; k < existedChainNodesClone.Count; k++)
                            {
                                var existedChainNode = existedChainNodesClone[k];
                                var previousDialerProxyTag = existedChainNode.streamSettings?.sockopt?.dialerProxy;
                                var nextTag = k + 1 < existedChainNodesClone.Count
                                    ? existedChainNodesClone[k + 1].tag
                                    : chainStartNode.tag;
                                FillDialerProxy(existedChainNode,
                                    previousDialerProxyTag == currentTag ? chainStartNode.tag : nextTag);
                                resultOutbounds.Add(existedChainNode);
                            }
                            j++;
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
                FillDialerProxy(outbound, dialerProxyTag);
            }

            resultOutbounds.Add(outbound);
        }
        return resultOutbounds;
    }

    private static void FillDialerProxy(Outbounds4Ray outbound, string dialerProxyTag)
    {
        outbound.streamSettings ??= new();
        outbound.streamSettings.sockopt ??= new();
        outbound.streamSettings.sockopt.dialerProxy = dialerProxyTag;

        // xhttp download dialer proxy
        if (outbound?.streamSettings?.xhttpSettings?.extra is not null)
        {
            var xhttpExtra = JsonUtils.ParseJson(JsonUtils.Serialize(outbound.streamSettings.xhttpSettings!.extra));
            if (xhttpExtra is JsonObject xhttpExtraObject
                && xhttpExtraObject["downloadSettings"] is JsonObject downloadSettings)
            {
                var sockopt = downloadSettings["sockopt"] as JsonObject ?? new JsonObject();
                sockopt["dialerProxy"] = dialerProxyTag;
                downloadSettings["sockopt"] = sockopt;
                outbound.streamSettings.xhttpSettings.extra = xhttpExtraObject;
            }
        }
    }

    private static Outbounds4Ray BuildDnsOutbound()
    {
        var outbound = new Outbounds4Ray { tag = Global.DnsOutboundTag, protocol = "dns", };
        return outbound;
    }

    private void ApplyOutboundFragment()
    {
        var actOutboundWithTlsList =
            _coreConfig.outbounds.Where(n => n.streamSettings?.security.IsNullOrEmpty() == false
                                             && (n.streamSettings?.sockopt?.dialerProxy?.IsNullOrEmpty() ?? true))
                .ToList();

        var configPackets = _config.Fragment4RayItem?.Packets ?? "tlshello";
        var configLength = _config.Fragment4RayItem?.Length ?? "50-100";
        var configDelay = _config.Fragment4RayItem?.Interval ?? "10-20";

        var fragmentMask = new Mask4Ray
        {
            type = "fragment",
            settings = new MaskSettings4Ray
            {
                packets = configPackets,
                length = configLength,
                delay = configDelay,
            }
        };
        var noiseMask = new Mask4Ray
        {
            type = "noise",
            settings = new MaskSettings4Ray
            {
                length = "10-20",
                delay = "10-16",
            }
        };

        foreach (var outbound in actOutboundWithTlsList)
        {
            //var packets = configPackets;
            //if (outbound.streamSettings.security == Global.StreamSecurityReality
            //    && packets == "tlshello")
            //{
            //    packets = "1-3";
            //}
            //else if (outbound.streamSettings.security == Global.StreamSecurity
            //         && packets != "tlshello")
            //{
            //    packets = "tlshello";
            //}
            var finalMaskJsonObj = JsonUtils.ParseJson(JsonUtils.Serialize(outbound.streamSettings?.finalmask)) as JsonObject ?? new JsonObject();
            // tcp fragment
            var tcpFinalmaskList = finalMaskJsonObj["tcp"] as JsonArray ?? [];
            if (tcpFinalmaskList.Count == 0)
            {
                tcpFinalmaskList.Add(JsonUtils.SerializeToNode(fragmentMask));
                finalMaskJsonObj["tcp"] = tcpFinalmaskList;
            }
            // udp noise
            var udpFinalmaskList = finalMaskJsonObj["udp"] as JsonArray ?? [];
            if (udpFinalmaskList.Count == 0)
            {
                udpFinalmaskList.Add(JsonUtils.SerializeToNode(noiseMask));
                finalMaskJsonObj["udp"] = udpFinalmaskList;
            }
            // write back
            outbound.streamSettings.finalmask = finalMaskJsonObj;
        }
    }
}
