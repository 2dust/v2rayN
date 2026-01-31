namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenOutbound(ProfileItem node, Outbounds4Ray outbound)
    {
        try
        {
            var muxEnabled = node.MuxEnabled ?? _config.CoreBasicItem.MuxEnabled;
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

                        await GenOutboundMux(node, outbound, muxEnabled, muxEnabled);

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
                        serversItem.method = AppManager.Instance.GetShadowsocksSecurities(node).Contains(node.Security) ? node.Security : "none";

                        serversItem.ota = false;
                        serversItem.level = 1;

                        await GenOutboundMux(node, outbound);

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

                        if (node.Security.IsNotEmpty()
                            && node.Id.IsNotEmpty())
                        {
                            SocksUsersItem4Ray socksUsersItem = new()
                            {
                                user = node.Security,
                                pass = node.Id,
                                level = 1
                            };

                            serversItem.users = new List<SocksUsersItem4Ray>() { socksUsersItem };
                        }

                        await GenOutboundMux(node, outbound);

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

                        if (node.Flow.IsNullOrEmpty())
                        {
                            await GenOutboundMux(node, outbound, muxEnabled, muxEnabled);
                        }
                        else
                        {
                            usersItem.flow = node.Flow;
                            await GenOutboundMux(node, outbound, false, muxEnabled);
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

                        await GenOutboundMux(node, outbound);

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
                            publicKey = node.PublicKey,
                            endpoint = address + ":" + node.Port.ToString()
                        };
                        var setting = new Outboundsettings4Ray
                        {
                            address = Utils.String2List(node.RequestHost),
                            secretKey = node.Id,
                            reserved = Utils.String2List(node.Path)?.Select(int.Parse).ToList(),
                            mtu = node.ShortId.IsNullOrEmpty() ? Global.TunMtus.First() : node.ShortId.ToInt(),
                            peers = new List<WireguardPeer4Ray> { peer }
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
            await GenBoundStreamSettings(node, outbound);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenOutboundMux(ProfileItem node, Outbounds4Ray outbound, bool enabledTCP = false, bool enabledUDP = false)
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
        return await Task.FromResult(0);
    }

    private async Task<int> GenBoundStreamSettings(ProfileItem node, Outbounds4Ray outbound)
    {
        try
        {
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
                    fingerprint = node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : node.Fingerprint,
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
                    fingerprint = node.Fingerprint.IsNullOrEmpty() ? _config.CoreBasicItem.DefFingerprint : node.Fingerprint,
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
                        type = node.HeaderType,
                        domain = host.NullIfEmpty()
                    };
                    if (path.IsNotEmpty())
                    {
                        kcpSettings.seed = path;
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
                    await GenOutboundMux(node, outbound);

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
                        idle_timeout = _config.GrpcItem.IdleTimeout,
                        health_check_timeout = _config.GrpcItem.HealthCheckTimeout,
                        permit_without_stream = _config.GrpcItem.PermitWithoutStream,
                        initial_windows_size = _config.GrpcItem.InitialWindowsSize,
                    };
                    streamSettings.grpcSettings = grpcSettings;
                    break;

                case "hysteria":
                    HysteriaUdpHop4Ray? udpHop = null;
                    if (node.Ports.IsNotEmpty() &&
                        (node.Ports.Contains(':') || node.Ports.Contains('-') || node.Ports.Contains(',')))
                    {
                        udpHop = new()
                        {
                            ports = node.Ports.Replace(':', '-'),
                            interval = _config.HysteriaItem.HopInterval > 0
                                ? _config.HysteriaItem.HopInterval
                                : null,
                        };
                    }
                    HysteriaSettings4Ray hysteriaSettings = new()
                    {
                        version = 2,
                        auth = node.Id,
                        up = _config.HysteriaItem.UpMbps > 0 ? $"{_config.HysteriaItem.UpMbps}mbps" : null,
                        down = _config.HysteriaItem.DownMbps > 0 ? $"{_config.HysteriaItem.DownMbps}mbps" : null,
                        udphop = udpHop,
                    };
                    streamSettings.hysteriaSettings = hysteriaSettings;
                    if (node.Path.IsNotEmpty())
                    {
                        streamSettings.udpmasks =
                            [new() { type = "salamander", settings = new() { password = node.Path.TrimEx(), } }];
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
        return 0;
    }

    private async Task<int> GenGroupOutbound(ProfileItem node, V2rayConfig v2rayConfig, string baseTagName = Global.ProxyTag, bool ignoreOriginChain = false)
    {
        try
        {
            if (!node.ConfigType.IsGroupType())
            {
                return -1;
            }
            var hasCycle = ProfileGroupItemManager.HasCycle(node.IndexId);
            if (hasCycle)
            {
                return -1;
            }

            var (childProfiles, profileGroupItem) = await ProfileGroupItemManager.GetChildProfileItems(node.IndexId);
            if (childProfiles.Count <= 0)
            {
                return -1;
            }
            switch (node.ConfigType)
            {
                case EConfigType.PolicyGroup:
                    if (ignoreOriginChain)
                    {
                        await GenOutboundsList(childProfiles, v2rayConfig, baseTagName);
                    }
                    else
                    {
                        await GenOutboundsListWithChain(childProfiles, v2rayConfig, baseTagName);
                    }
                    break;

                case EConfigType.ProxyChain:
                    await GenChainOutboundsList(childProfiles, v2rayConfig, baseTagName);
                    break;

                default:
                    break;
            }

            //add balancers
            if (node.ConfigType == EConfigType.PolicyGroup)
            {
                await GenObservatory(v2rayConfig, profileGroupItem.MultipleLoad, baseTagName);
                await GenBalancer(v2rayConfig, profileGroupItem.MultipleLoad, baseTagName);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenMoreOutbounds(ProfileItem node, V2rayConfig v2rayConfig)
    {
        //fragment proxy
        if (_config.CoreBasicItem.EnableFragment
            && v2rayConfig.outbounds.First().streamSettings?.security.IsNullOrEmpty() == false)
        {
            var fragmentOutbound = new Outbounds4Ray
            {
                protocol = "freedom",
                tag = $"frag-{Global.ProxyTag}",
                settings = new()
                {
                    fragment = new()
                    {
                        packets = _config.Fragment4RayItem?.Packets,
                        length = _config.Fragment4RayItem?.Length,
                        interval = _config.Fragment4RayItem?.Interval
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
            var subItem = await AppManager.Instance.GetSubItem(node.Subid);
            if (subItem is null)
            {
                return 0;
            }

            //current proxy
            var outbound = v2rayConfig.outbounds.First();
            var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);

            //Previous proxy
            var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
            string? prevOutboundTag = null;
            if (prevNode is not null
                && Global.XraySupportConfigType.Contains(prevNode.ConfigType))
            {
                var prevOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                await GenOutbound(prevNode, prevOutbound);
                prevOutboundTag = $"prev-{Global.ProxyTag}";
                prevOutbound.tag = prevOutboundTag;
                v2rayConfig.outbounds.Add(prevOutbound);
            }
            var nextOutbound = await GenChainOutbounds(subItem, outbound, prevOutboundTag);

            if (nextOutbound is not null)
            {
                v2rayConfig.outbounds.Insert(0, nextOutbound);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return 0;
    }

    private async Task<int> GenOutboundsListWithChain(List<ProfileItem> nodes, V2rayConfig v2rayConfig, string baseTagName = Global.ProxyTag)
    {
        try
        {
            // Get template and initialize list
            var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
            if (txtOutbound.IsNullOrEmpty())
            {
                return 0;
            }

            var resultOutbounds = new List<Outbounds4Ray>();
            var prevOutbounds = new List<Outbounds4Ray>(); // Separate list for prev outbounds and fragment

            // Cache for chain proxies to avoid duplicate generation
            var nextProxyCache = new Dictionary<string, Outbounds4Ray?>();
            var prevProxyTags = new Dictionary<string, string?>(); // Map from profile name to tag
            var prevIndex = 0; // Index for prev outbounds

            // Process nodes
            var index = 0;
            foreach (var node in nodes)
            {
                index++;

                if (node.ConfigType.IsGroupType())
                {
                    var (childProfiles, _) = await ProfileGroupItemManager.GetChildProfileItems(node.IndexId);
                    if (childProfiles.Count <= 0)
                    {
                        continue;
                    }
                    var childBaseTagName = $"{baseTagName}-{index}";
                    var ret = node.ConfigType switch
                    {
                        EConfigType.PolicyGroup =>
                            await GenOutboundsListWithChain(childProfiles, v2rayConfig, childBaseTagName),
                        EConfigType.ProxyChain =>
                            await GenChainOutboundsList(childProfiles, v2rayConfig, childBaseTagName),
                        _ => throw new NotImplementedException()
                    };
                    continue;
                }

                // Handle proxy chain
                string? prevTag = null;
                var currentOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                var nextOutbound = nextProxyCache.GetValueOrDefault(node.Subid, null);
                if (nextOutbound != null)
                {
                    nextOutbound = JsonUtils.DeepCopy(nextOutbound);
                }

                var subItem = await AppManager.Instance.GetSubItem(node.Subid);

                // current proxy
                await GenOutbound(node, currentOutbound);
                currentOutbound.tag = $"{baseTagName}-{index}";

                if (!node.Subid.IsNullOrEmpty())
                {
                    if (prevProxyTags.TryGetValue(node.Subid, out var value))
                    {
                        prevTag = value; // maybe null
                    }
                    else
                    {
                        var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
                        if (prevNode is not null
                            && Global.XraySupportConfigType.Contains(prevNode.ConfigType))
                        {
                            var prevOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                            await GenOutbound(prevNode, prevOutbound);
                            prevTag = $"prev-{baseTagName}-{++prevIndex}";
                            prevOutbound.tag = prevTag;
                            prevOutbounds.Add(prevOutbound);
                        }
                        prevProxyTags[node.Subid] = prevTag;
                    }

                    nextOutbound = await GenChainOutbounds(subItem, currentOutbound, prevTag, nextOutbound);
                    if (!nextProxyCache.ContainsKey(node.Subid))
                    {
                        nextProxyCache[node.Subid] = nextOutbound;
                    }
                }

                if (nextOutbound is not null)
                {
                    resultOutbounds.Add(nextOutbound);
                }
                resultOutbounds.Add(currentOutbound);
            }

            // Merge results: first the main chain outbounds, then other outbounds, and finally utility outbounds
            if (baseTagName == Global.ProxyTag)
            {
                resultOutbounds.AddRange(prevOutbounds);
                resultOutbounds.AddRange(v2rayConfig.outbounds);
                v2rayConfig.outbounds = resultOutbounds;
            }
            else
            {
                v2rayConfig.outbounds.AddRange(prevOutbounds);
                v2rayConfig.outbounds.AddRange(resultOutbounds);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return 0;
    }

    /// <summary>
    /// Generates a chained outbound configuration for the given subItem and outbound.
    /// The outbound's tag must be set before calling this method.
    /// Returns the next proxy's outbound configuration, which may be null if no next proxy exists.
    /// </summary>
    /// <param name="subItem">The subscription item containing proxy chain information.</param>
    /// <param name="outbound">The current outbound configuration. Its tag must be set before calling this method.</param>
    /// <param name="prevOutboundTag">The tag of the previous outbound in the chain, if any.</param>
    /// <param name="nextOutbound">The outbound for the next proxy in the chain, if already created. If null, will be created inside.</param>
    /// <returns>
    /// The outbound configuration for the next proxy in the chain, or null if no next proxy exists.
    /// </returns>
    private async Task<Outbounds4Ray?> GenChainOutbounds(SubItem subItem, Outbounds4Ray outbound, string? prevOutboundTag, Outbounds4Ray? nextOutbound = null)
    {
        try
        {
            var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);

            if (!prevOutboundTag.IsNullOrEmpty())
            {
                outbound.streamSettings.sockopt = new()
                {
                    dialerProxy = prevOutboundTag
                };
            }

            // Next proxy
            var nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
            if (nextNode is not null
                && Global.XraySupportConfigType.Contains(nextNode.ConfigType))
            {
                if (nextOutbound == null)
                {
                    nextOutbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                    await GenOutbound(nextNode, nextOutbound);
                }
                nextOutbound.tag = outbound.tag;

                outbound.tag = $"mid-{outbound.tag}";
                nextOutbound.streamSettings.sockopt = new()
                {
                    dialerProxy = outbound.tag
                };
            }
            return nextOutbound;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return null;
    }

    private async Task<int> GenOutboundsList(List<ProfileItem> nodes, V2rayConfig v2rayConfig, string baseTagName = Global.ProxyTag)
    {
        var resultOutbounds = new List<Outbounds4Ray>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null)
            {
                continue;
            }

            if (node.ConfigType.IsGroupType())
            {
                var (childProfiles, _) = await ProfileGroupItemManager.GetChildProfileItems(node.IndexId);
                if (childProfiles.Count <= 0)
                {
                    continue;
                }
                var childBaseTagName = $"{baseTagName}-{i + 1}";
                var ret = node.ConfigType switch
                {
                    EConfigType.PolicyGroup =>
                        await GenOutboundsListWithChain(childProfiles, v2rayConfig, childBaseTagName),
                    EConfigType.ProxyChain =>
                        await GenChainOutboundsList(childProfiles, v2rayConfig, childBaseTagName),
                    _ => throw new NotImplementedException()
                };
                continue;
            }
            var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
            if (txtOutbound.IsNullOrEmpty())
            {
                break;
            }
            var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
            var result = await GenOutbound(node, outbound);
            if (result != 0)
            {
                break;
            }
            outbound.tag = baseTagName + (i + 1).ToString();
            resultOutbounds.Add(outbound);
        }
        if (baseTagName == Global.ProxyTag)
        {
            resultOutbounds.AddRange(v2rayConfig.outbounds);
            v2rayConfig.outbounds = resultOutbounds;
        }
        else
        {
            v2rayConfig.outbounds.AddRange(resultOutbounds);
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenChainOutboundsList(List<ProfileItem> nodes, V2rayConfig v2rayConfig, string baseTagName = Global.ProxyTag)
    {
        // Based on actual network flow instead of data packets
        var nodesReverse = nodes.AsEnumerable().Reverse().ToList();
        var resultOutbounds = new List<Outbounds4Ray>();
        for (var i = 0; i < nodesReverse.Count; i++)
        {
            var node = nodesReverse[i];
            var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
            if (txtOutbound.IsNullOrEmpty())
            {
                break;
            }
            var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
            var result = await GenOutbound(node, outbound);

            if (result != 0)
            {
                break;
            }

            if (i == 0)
            {
                outbound.tag = baseTagName;
            }
            else
            {
                // avoid v2ray observe
                outbound.tag = "chain-" + baseTagName + i.ToString();
            }

            if (i != nodesReverse.Count - 1)
            {
                outbound.streamSettings.sockopt = new()
                {
                    dialerProxy = "chain-" + baseTagName + (i + 1).ToString()
                };
            }

            resultOutbounds.Add(outbound);
        }
        if (baseTagName == Global.ProxyTag)
        {
            resultOutbounds.AddRange(v2rayConfig.outbounds);
            v2rayConfig.outbounds = resultOutbounds;
        }
        else
        {
            v2rayConfig.outbounds.AddRange(resultOutbounds);
        }

        return await Task.FromResult(0);
    }
}
