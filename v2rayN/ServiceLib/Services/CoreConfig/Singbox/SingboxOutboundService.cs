namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<int> GenOutbound(ProfileItem node, Outbound4Sbox outbound)
    {
        try
        {
            var extraItem = node.GetExtraItem();
            outbound.server = node.Address;
            outbound.server_port = node.Port;
            outbound.type = Global.ProtocolTypes[node.ConfigType];

            switch (node.ConfigType)
            {
                case EConfigType.VMess:
                    {
                        outbound.uuid = node.Id;
                        outbound.alter_id = int.TryParse(extraItem?.AlterId, out var result) ? result : 0;
                        if (Global.VmessSecurities.Contains(node.Security))
                        {
                            outbound.security = node.Security;
                        }
                        else
                        {
                            outbound.security = Global.DefaultSecurity;
                        }

                        await GenOutboundMux(node, outbound);
                        await GenOutboundTransport(node, outbound);
                        break;
                    }
                case EConfigType.Shadowsocks:
                    {
                        outbound.method = AppManager.Instance.GetShadowsocksSecurities(node).Contains(node.Security) ? node.Security : Global.None;
                        outbound.password = node.Id;

                        if (node.Network == nameof(ETransport.tcp) && node.HeaderType == Global.TcpHeaderHttp)
                        {
                            outbound.plugin = "obfs-local";
                            outbound.plugin_opts = $"obfs=http;obfs-host={node.RequestHost};";
                        }
                        else
                        {
                            var pluginArgs = string.Empty;
                            if (node.Network == nameof(ETransport.ws))
                            {
                                pluginArgs += "mode=websocket;";
                                pluginArgs += $"host={node.RequestHost};";
                                // https://github.com/shadowsocks/v2ray-plugin/blob/e9af1cdd2549d528deb20a4ab8d61c5fbe51f306/args.go#L172
                                // Equal signs and commas [and backslashes] must be escaped with a backslash.
                                var path = node.Path.Replace("\\", "\\\\").Replace("=", "\\=").Replace(",", "\\,");
                                pluginArgs += $"path={path};";
                            }
                            else if (node.Network == nameof(ETransport.quic))
                            {
                                pluginArgs += "mode=quic;";
                            }
                            if (node.StreamSecurity == Global.StreamSecurity)
                            {
                                pluginArgs += "tls;";
                                var certs = CertPemManager.ParsePemChain(node.Cert);
                                if (certs.Count > 0)
                                {
                                    var cert = certs.First();
                                    const string beginMarker = "-----BEGIN CERTIFICATE-----\n";
                                    const string endMarker = "\n-----END CERTIFICATE-----";

                                    var base64Content = cert.Replace(beginMarker, "").Replace(endMarker, "").Trim();

                                    base64Content = base64Content.Replace("=", "\\=");

                                    pluginArgs += $"certRaw={base64Content};";
                                }
                            }
                            if (pluginArgs.Length > 0)
                            {
                                outbound.plugin = "v2ray-plugin";
                                pluginArgs += "mux=0;";
                                // pluginStr remove last ';'
                                pluginArgs = pluginArgs[..^1];
                                outbound.plugin_opts = pluginArgs;
                            }
                        }

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

                        if (extraItem.Flow.IsNullOrEmpty())
                        {
                            await GenOutboundMux(node, outbound);
                        }
                        else
                        {
                            outbound.flow = extraItem.Flow;
                        }

                        await GenOutboundTransport(node, outbound);
                        break;
                    }
                case EConfigType.Trojan:
                    {
                        outbound.password = node.Id;

                        await GenOutboundMux(node, outbound);
                        await GenOutboundTransport(node, outbound);
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

                        var extra = node.GetExtraItem();
                        outbound.up_mbps = int.TryParse(extra?.UpMbps, out var upMbps) && upMbps >= 0 ? upMbps : (_config.HysteriaItem.UpMbps > 0 ? _config.HysteriaItem.UpMbps : null);
                        outbound.down_mbps = int.TryParse(extra?.DownMbps, out var downMbps) && downMbps >= 0 ? downMbps : (_config.HysteriaItem.DownMbps > 0 ? _config.HysteriaItem.DownMbps : null);
                        var ports = extra?.Ports?.IsNullOrEmpty() == false ? extra.Ports : null;
                        if ((!ports.IsNullOrEmpty()) && (ports.Contains(':') || ports.Contains('-') || ports.Contains(',')))
                        {
                            outbound.server_port = null;
                            outbound.server_ports = ports.Split(',')
                                .Select(p => p.Trim())
                                .Where(p => p.IsNotEmpty())
                                .Select(p =>
                                {
                                    var port = p.Replace('-', ':');
                                    return port.Contains(':') ? port : $"{port}:{port}";
                                })
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
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenEndpoint(ProfileItem node, Endpoints4Sbox endpoint)
    {
        try
        {
            endpoint.address = Utils.String2List(node.RequestHost);
            endpoint.type = Global.ProtocolTypes[node.ConfigType];

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
                        endpoint.private_key = node.Id;
                        endpoint.mtu = node.ShortId.IsNullOrEmpty() ? Global.TunMtus.First() : node.ShortId.ToInt();
                        endpoint.peers = new() { peer };
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

    private async Task<BaseServer4Sbox?> GenServer(ProfileItem node)
    {
        try
        {
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (node.ConfigType == EConfigType.WireGuard)
            {
                var endpoint = JsonUtils.Deserialize<Endpoints4Sbox>(txtOutbound);
                var ret = await GenEndpoint(node, endpoint);
                if (ret != 0)
                {
                    return null;
                }
                return endpoint;
            }
            else
            {
                var outbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                var ret = await GenOutbound(node, outbound);
                if (ret != 0)
                {
                    return null;
                }
                return outbound;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult<BaseServer4Sbox?>(null);
    }

    private async Task<int> GenOutboundMux(ProfileItem node, Outbound4Sbox outbound)
    {
        try
        {
            var muxEnabled = node.MuxEnabled ?? _config.CoreBasicItem.MuxEnabled;
            if (muxEnabled && _config.Mux4SboxItem.Protocol.IsNotEmpty())
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
            if (node.StreamSecurity is not (Global.StreamSecurityReality or Global.StreamSecurity))
            {
                return await Task.FromResult(0);
            }
            if (node.ConfigType is EConfigType.Shadowsocks or EConfigType.SOCKS or EConfigType.WireGuard)
            {
                return await Task.FromResult(0);
            }
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
                record_fragment = _config.CoreBasicItem.EnableFragment ? true : null,
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
            if (node.StreamSecurity == Global.StreamSecurity)
            {
                var certs = CertPemManager.ParsePemChain(node.Cert);
                if (certs.Count > 0)
                {
                    tls.certificate = certs;
                    tls.insecure = false;
                }
            }
            else if (node.StreamSecurity == Global.StreamSecurityReality)
            {
                tls.reality = new Reality4Sbox()
                {
                    enabled = true,
                    public_key = node.PublicKey,
                    short_id = node.ShortId
                };
                tls.insecure = false;
            }
            var (ech, _) = ParseEchParam(node.EchConfigList);
            if (ech is not null)
            {
                tls.ech = ech;
            }
            outbound.tls = tls;
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
                    transport.path = node.Path.NullIfEmpty();
                    break;

                case nameof(ETransport.tcp):   //http
                    if (node.HeaderType == Global.TcpHeaderHttp)
                    {
                        transport.type = nameof(ETransport.http);
                        transport.host = node.RequestHost.IsNullOrEmpty() ? null : Utils.String2List(node.RequestHost);
                        transport.path = node.Path.NullIfEmpty();
                    }
                    break;

                case nameof(ETransport.ws):
                    transport.type = nameof(ETransport.ws);
                    var wsPath = node.Path;

                    // Parse eh and ed parameters from path using regex
                    if (!wsPath.IsNullOrEmpty())
                    {
                        var edRegex = new Regex(@"[?&]ed=(\d+)");
                        var edMatch = edRegex.Match(wsPath);
                        if (edMatch.Success && int.TryParse(edMatch.Groups[1].Value, out var edValue))
                        {
                            transport.max_early_data = edValue;
                            transport.early_data_header_name = "Sec-WebSocket-Protocol";

                            wsPath = edRegex.Replace(wsPath, "");
                            wsPath = wsPath.Replace("?&", "?");
                            if (wsPath.EndsWith('?'))
                            {
                                wsPath = wsPath.TrimEnd('?');
                            }
                        }

                        var ehRegex = new Regex(@"[?&]eh=([^&]+)");
                        var ehMatch = ehRegex.Match(wsPath);
                        if (ehMatch.Success)
                        {
                            transport.early_data_header_name = Uri.UnescapeDataString(ehMatch.Groups[1].Value);
                        }
                    }

                    transport.path = wsPath.NullIfEmpty();
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
                    transport.path = node.Path.NullIfEmpty();
                    transport.host = node.RequestHost.NullIfEmpty();

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

    private async Task<int> GenGroupOutbound(ProfileItem node, SingboxConfig singboxConfig, string baseTagName = Global.ProxyTag, bool ignoreOriginChain = false)
    {
        try
        {
            if (!node.ConfigType.IsGroupType())
            {
                return -1;
            }
            var hasCycle = await GroupProfileManager.HasCycle(node);
            if (hasCycle)
            {
                return -1;
            }

            var (childProfiles, profileExtraItem) = await GroupProfileManager.GetChildProfileItems(node);
            if (childProfiles.Count <= 0)
            {
                return -1;
            }
            switch (node.ConfigType)
            {
                case EConfigType.PolicyGroup:
                    var multipleLoad = profileExtraItem?.MultipleLoad ?? EMultipleLoad.LeastPing;
                    if (ignoreOriginChain)
                    {
                        await GenOutboundsList(childProfiles, singboxConfig, multipleLoad, baseTagName);
                    }
                    else
                    {
                        await GenOutboundsListWithChain(childProfiles, singboxConfig, multipleLoad, baseTagName);
                    }

                    break;

                case EConfigType.ProxyChain:
                    await GenChainOutboundsList(childProfiles, singboxConfig, baseTagName);
                    break;

                default:
                    break;
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
            var subItem = await AppManager.Instance.GetSubItem(node.Subid);
            if (subItem is null)
            {
                return 0;
            }

            //current proxy
            BaseServer4Sbox? outbound = singboxConfig.endpoints?.FirstOrDefault(t => t.tag == Global.ProxyTag, null);
            outbound ??= singboxConfig.outbounds.First();

            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);

            //Previous proxy
            var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
            string? prevOutboundTag = null;
            if (prevNode is not null
                && Global.SingboxSupportConfigType.Contains(prevNode.ConfigType))
            {
                prevOutboundTag = $"prev-{Global.ProxyTag}";
                var prevServer = await GenServer(prevNode);
                prevServer.tag = prevOutboundTag;
                if (prevServer is Endpoints4Sbox endpoint)
                {
                    singboxConfig.endpoints ??= new();
                    singboxConfig.endpoints.Add(endpoint);
                }
                else if (prevServer is Outbound4Sbox outboundPrev)
                {
                    singboxConfig.outbounds.Add(outboundPrev);
                }
            }
            var nextServer = await GenChainOutbounds(subItem, outbound, prevOutboundTag);

            if (nextServer is not null)
            {
                if (nextServer is Endpoints4Sbox endpoint)
                {
                    singboxConfig.endpoints ??= new();
                    singboxConfig.endpoints.Insert(0, endpoint);
                }
                else if (nextServer is Outbound4Sbox outboundNext)
                {
                    singboxConfig.outbounds.Insert(0, outboundNext);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return 0;
    }

    private async Task<int> GenOutboundsListWithChain(List<ProfileItem> nodes, SingboxConfig singboxConfig, EMultipleLoad multipleLoad, string baseTagName = Global.ProxyTag)
    {
        try
        {
            // Get outbound template and initialize lists
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (txtOutbound.IsNullOrEmpty())
            {
                return 0;
            }

            var resultOutbounds = new List<Outbound4Sbox>();
            var resultEndpoints = new List<Endpoints4Sbox>(); // For endpoints
            var prevOutbounds = new List<Outbound4Sbox>(); // Separate list for prev outbounds
            var prevEndpoints = new List<Endpoints4Sbox>(); // Separate list for prev endpoints
            var proxyTags = new List<string>(); // For selector and urltest outbounds

            // Cache for chain proxies to avoid duplicate generation
            var nextProxyCache = new Dictionary<string, BaseServer4Sbox?>();
            var prevProxyTags = new Dictionary<string, string?>(); // Map from profile name to tag
            var prevIndex = 0; // Index for prev outbounds

            // Process each node
            var index = 0;
            foreach (var node in nodes)
            {
                index++;

                if (node.ConfigType.IsGroupType())
                {
                    var (childProfiles, profileExtraItem) = await GroupProfileManager.GetChildProfileItems(node);
                    if (childProfiles.Count <= 0)
                    {
                        continue;
                    }
                    var childBaseTagName = $"{baseTagName}-{index}";
                    var ret = node.ConfigType switch
                    {
                        EConfigType.PolicyGroup =>
                            await GenOutboundsListWithChain(childProfiles, singboxConfig,
                                profileExtraItem?.MultipleLoad ?? EMultipleLoad.LeastPing, childBaseTagName),
                        EConfigType.ProxyChain =>
                            await GenChainOutboundsList(childProfiles, singboxConfig, childBaseTagName),
                        _ => throw new NotImplementedException()
                    };
                    if (ret == 0)
                    {
                        proxyTags.Add(childBaseTagName);
                    }
                    continue;
                }

                // Handle proxy chain
                string? prevTag = null;
                var currentServer = await GenServer(node);
                var nextServer = nextProxyCache.GetValueOrDefault(node.Subid, null);
                if (nextServer != null)
                {
                    nextServer = JsonUtils.DeepCopy(nextServer);
                }

                var subItem = await AppManager.Instance.GetSubItem(node.Subid);

                // current proxy
                currentServer.tag = $"{baseTagName}-{index}";
                proxyTags.Add(currentServer.tag);

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
                            && Global.SingboxSupportConfigType.Contains(prevNode.ConfigType))
                        {
                            var prevOutbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                            await GenOutbound(prevNode, prevOutbound);
                            prevTag = $"prev-{baseTagName}-{++prevIndex}";
                            prevOutbound.tag = prevTag;
                            prevOutbounds.Add(prevOutbound);
                        }
                        prevProxyTags[node.Subid] = prevTag;
                    }

                    nextServer = await GenChainOutbounds(subItem, currentServer, prevTag, nextServer);
                    if (!nextProxyCache.ContainsKey(node.Subid))
                    {
                        nextProxyCache[node.Subid] = nextServer;
                    }
                }

                if (nextServer is not null)
                {
                    if (nextServer is Endpoints4Sbox nextEndpoint)
                    {
                        resultEndpoints.Add(nextEndpoint);
                    }
                    else if (nextServer is Outbound4Sbox nextOutbound)
                    {
                        resultOutbounds.Add(nextOutbound);
                    }
                }
                if (currentServer is Endpoints4Sbox currentEndpoint)
                {
                    resultEndpoints.Add(currentEndpoint);
                }
                else if (currentServer is Outbound4Sbox currentOutbound)
                {
                    resultOutbounds.Add(currentOutbound);
                }
            }

            // Add urltest outbound (auto selection based on latency)
            if (proxyTags.Count > 0)
            {
                var outUrltest = new Outbound4Sbox
                {
                    type = "urltest",
                    tag = $"{baseTagName}-auto",
                    outbounds = proxyTags,
                    interrupt_exist_connections = false,
                };

                if (multipleLoad == EMultipleLoad.Fallback)
                {
                    outUrltest.tolerance = 5000;
                }

                // Add selector outbound (manual selection)
                var outSelector = new Outbound4Sbox
                {
                    type = "selector",
                    tag = baseTagName,
                    outbounds = JsonUtils.DeepCopy(proxyTags),
                    interrupt_exist_connections = false,
                };
                outSelector.outbounds.Insert(0, outUrltest.tag);

                // Insert these at the beginning
                resultOutbounds.Insert(0, outUrltest);
                resultOutbounds.Insert(0, outSelector);
            }

            // Merge results: first the selector/urltest/proxies, then other outbounds, and finally prev outbounds
            var serverList = new List<BaseServer4Sbox>();
            serverList = serverList.Concat(prevOutbounds)
                .Concat(resultOutbounds)
                .Concat(resultEndpoints)
                .ToList();
            await AddRangeOutbounds(serverList, singboxConfig, baseTagName == Global.ProxyTag);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return 0;
    }

    private async Task<BaseServer4Sbox?> GenChainOutbounds(SubItem subItem, BaseServer4Sbox outbound, string? prevOutboundTag, BaseServer4Sbox? nextOutbound = null)
    {
        try
        {
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);

            if (!prevOutboundTag.IsNullOrEmpty())
            {
                outbound.detour = prevOutboundTag;
            }

            // Next proxy
            var nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
            if (nextNode is not null
                && Global.SingboxSupportConfigType.Contains(nextNode.ConfigType))
            {
                nextOutbound ??= await GenServer(nextNode);
                nextOutbound.tag = outbound.tag;

                outbound.tag = $"mid-{outbound.tag}";
                nextOutbound.detour = outbound.tag;
            }
            return nextOutbound;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return null;
    }

    private async Task<int> GenOutboundsList(List<ProfileItem> nodes, SingboxConfig singboxConfig, EMultipleLoad multipleLoad, string baseTagName = Global.ProxyTag)
    {
        var resultOutbounds = new List<Outbound4Sbox>();
        var resultEndpoints = new List<Endpoints4Sbox>(); // For endpoints
        var proxyTags = new List<string>(); // For selector and urltest outbounds
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null)
            {
                continue;
            }

            if (node.ConfigType.IsGroupType())
            {
                var (childProfiles, profileExtraItem) = await GroupProfileManager.GetChildProfileItems(node);
                if (childProfiles.Count <= 0)
                {
                    continue;
                }
                var childBaseTagName = $"{baseTagName}-{i + 1}";
                var ret = node.ConfigType switch
                {
                    EConfigType.PolicyGroup =>
                        await GenOutboundsList(childProfiles, singboxConfig, profileExtraItem?.MultipleLoad ?? EMultipleLoad.LeastPing, childBaseTagName),
                    EConfigType.ProxyChain =>
                        await GenChainOutboundsList(childProfiles, singboxConfig, childBaseTagName),
                    _ => throw new NotImplementedException()
                };
                if (ret == 0)
                {
                    proxyTags.Add(childBaseTagName);
                }
                continue;
            }
            var server = await GenServer(node);
            if (server is null)
            {
                break;
            }
            server.tag = baseTagName + (i + 1).ToString();
            if (server is Endpoints4Sbox endpoint)
            {
                resultEndpoints.Add(endpoint);
            }
            else if (server is Outbound4Sbox outbound)
            {
                resultOutbounds.Add(outbound);
            }
            proxyTags.Add(server.tag);
        }
        // Add urltest outbound (auto selection based on latency)
        if (proxyTags.Count > 0)
        {
            var outUrltest = new Outbound4Sbox
            {
                type = "urltest",
                tag = $"{baseTagName}-auto",
                outbounds = proxyTags,
                interrupt_exist_connections = false,
            };
            if (multipleLoad == EMultipleLoad.Fallback)
            {
                outUrltest.tolerance = 5000;
            }
            // Add selector outbound (manual selection)
            var outSelector = new Outbound4Sbox
            {
                type = "selector",
                tag = baseTagName,
                outbounds = JsonUtils.DeepCopy(proxyTags),
                interrupt_exist_connections = false,
            };
            outSelector.outbounds.Insert(0, outUrltest.tag);
            // Insert these at the beginning
            resultOutbounds.Insert(0, outUrltest);
            resultOutbounds.Insert(0, outSelector);
        }
        var serverList = new List<BaseServer4Sbox>();
        serverList = serverList.Concat(resultOutbounds)
            .Concat(resultEndpoints)
            .ToList();
        await AddRangeOutbounds(serverList, singboxConfig, baseTagName == Global.ProxyTag);
        return await Task.FromResult(0);
    }

    private async Task<int> GenChainOutboundsList(List<ProfileItem> nodes, SingboxConfig singboxConfig, string baseTagName = Global.ProxyTag)
    {
        // Based on actual network flow instead of data packets
        var nodesReverse = nodes.AsEnumerable().Reverse().ToList();
        var resultOutbounds = new List<Outbound4Sbox>();
        var resultEndpoints = new List<Endpoints4Sbox>(); // For endpoints
        for (var i = 0; i < nodesReverse.Count; i++)
        {
            var node = nodesReverse[i];
            var server = await GenServer(node);

            if (server is null)
            {
                break;
            }

            if (i == 0)
            {
                server.tag = baseTagName;
            }
            else
            {
                server.tag = baseTagName + i.ToString();
            }

            if (i != nodesReverse.Count - 1)
            {
                server.detour = baseTagName + (i + 1).ToString();
            }

            if (server is Endpoints4Sbox endpoint)
            {
                resultEndpoints.Add(endpoint);
            }
            else if (server is Outbound4Sbox outbound)
            {
                resultOutbounds.Add(outbound);
            }
        }
        var serverList = new List<BaseServer4Sbox>();
        serverList = serverList.Concat(resultOutbounds)
            .Concat(resultEndpoints)
            .ToList();
        await AddRangeOutbounds(serverList, singboxConfig, baseTagName == Global.ProxyTag);
        return await Task.FromResult(0);
    }

    private async Task<int> AddRangeOutbounds(List<BaseServer4Sbox> servers, SingboxConfig singboxConfig, bool prepend = true)
    {
        try
        {
            if (servers is null || servers.Count <= 0)
            {
                return 0;
            }
            var outbounds = servers.Where(s => s is Outbound4Sbox).Cast<Outbound4Sbox>().ToList();
            var endpoints = servers.Where(s => s is Endpoints4Sbox).Cast<Endpoints4Sbox>().ToList();
            singboxConfig.endpoints ??= new();
            if (prepend)
            {
                singboxConfig.outbounds.InsertRange(0, outbounds);
                singboxConfig.endpoints.InsertRange(0, endpoints);
            }
            else
            {
                singboxConfig.outbounds.AddRange(outbounds);
                singboxConfig.endpoints.AddRange(endpoints);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private (Ech4Sbox? ech, Server4Sbox? dnsServer) ParseEchParam(string? echConfig)
    {
        if (echConfig.IsNullOrEmpty())
        {
            return (null, null);
        }
        if (!echConfig.Contains("://"))
        {
            return (new Ech4Sbox()
            {
                enabled = true,
                config = [$"-----BEGIN ECH CONFIGS-----\n" +
                          $"{echConfig}\n" +
                          $"-----END ECH CONFIGS-----"],
            }, null);
        }
        var idx = echConfig.IndexOf('+');
        // NOTE: query_server_name, since sing-box 1.13.0
        //var queryServerName = idx > 0 ? echConfig[..idx] : null;
        var echDnsServer = idx > 0 ? echConfig[(idx + 1)..] : echConfig;
        return (new Ech4Sbox()
        {
            enabled = true,
            query_server_name = null,
        }, ParseDnsAddress(echDnsServer));
    }
}
