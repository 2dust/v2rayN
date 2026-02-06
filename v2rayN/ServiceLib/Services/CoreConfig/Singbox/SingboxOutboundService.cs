namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private void GenOutbounds()
    {
        var proxyOutbounds = BuildAllProxyOutbounds();
        FillRangeProxy(proxyOutbounds, _coreConfig, true);
    }

    private List<BaseServer4Sbox> BuildAllProxyOutbounds(string baseTagName = Global.ProxyTag, bool withSelector = true)
    {
        var proxyOutboundList = new List<BaseServer4Sbox>();
        if (!context.Node.ConfigType.IsComplexType())
        {
            var outbound = BuildProxyOutbound(baseTagName);
            proxyOutboundList.Add(outbound);
        }
        else
        {
            proxyOutboundList.AddRange(BuildGroupProxyOutbounds(baseTagName));
        }
        var proxyTags = proxyOutboundList.Where(n => n.tag.StartsWith(Global.ProxyTag)).Select(n => n.tag).ToList();
        if (proxyTags.Count > 1)
        {
            proxyOutboundList.InsertRange(0, BuildSelectorOutbounds(proxyTags, baseTagName));
        }
        return proxyOutboundList;
    }

    private BaseServer4Sbox BuildProxyOutbound(string baseTagName = Global.ProxyTag)
    {
        var outbound = BuildProxyServer();
        outbound.tag = baseTagName;
        return outbound;
    }

    private List<BaseServer4Sbox> BuildGroupProxyOutbounds(string baseTagName = Global.ProxyTag)
    {
        var proxyOutboundList = new List<BaseServer4Sbox>();
        switch (context.Node.ConfigType)
        {
            case EConfigType.PolicyGroup:
                proxyOutboundList = BuildOutboundsList(baseTagName);
                break;
            case EConfigType.ProxyChain:
                proxyOutboundList = BuildChainOutboundsList(baseTagName);
                break;
        }
        return proxyOutboundList;
    }

    private BaseServer4Sbox BuildProxyServer()
    {
        try
        {
            var node = context.Node;
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (node.ConfigType == EConfigType.WireGuard)
            {
                var endpoint = JsonUtils.Deserialize<Endpoints4Sbox>(txtOutbound);
                FillEndpoint(endpoint);
                return endpoint;
            }
            else
            {
                var outbound = JsonUtils.Deserialize<Outbound4Sbox>(txtOutbound);
                FillOutbound(outbound);
                return outbound;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        throw new InvalidOperationException();
    }

    private void FillOutbound(Outbound4Sbox outbound)
    {
        try
        {
            var node = context.Node;
            var protocolExtra = node.GetProtocolExtra();
            outbound.server = node.Address;
            outbound.server_port = node.Port;
            outbound.type = Global.ProtocolTypes[node.ConfigType];

            switch (node.ConfigType)
            {
                case EConfigType.VMess:
                    {
                        outbound.uuid = node.Password;
                        outbound.alter_id = int.TryParse(protocolExtra.AlterId, out var result) ? result : 0;
                        if (Global.VmessSecurities.Contains(protocolExtra.VmessSecurity))
                        {
                            outbound.security = protocolExtra.VmessSecurity;
                        }
                        else
                        {
                            outbound.security = Global.DefaultSecurity;
                        }

                        FillOutboundMux(outbound);
                        FillOutboundTransport(outbound);
                        break;
                    }
                case EConfigType.Shadowsocks:
                    {
                        outbound.method = AppManager.Instance.GetShadowsocksSecurities(node).Contains(protocolExtra.SsMethod)
                            ? protocolExtra.SsMethod : Global.None;
                        outbound.password = node.Password;

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

                        FillOutboundMux(outbound);
                        break;
                    }
                case EConfigType.SOCKS:
                    {
                        outbound.version = "5";
                        if (node.Username.IsNotEmpty()
                            && node.Password.IsNotEmpty())
                        {
                            outbound.username = node.Username;
                            outbound.password = node.Password;
                        }
                        break;
                    }
                case EConfigType.HTTP:
                    {
                        if (node.Username.IsNotEmpty()
                            && node.Password.IsNotEmpty())
                        {
                            outbound.username = node.Username;
                            outbound.password = node.Password;
                        }
                        break;
                    }
                case EConfigType.VLESS:
                    {
                        outbound.uuid = node.Password;

                        outbound.packet_encoding = "xudp";

                        if (!protocolExtra.Flow.IsNullOrEmpty())
                        {
                            outbound.flow = protocolExtra.Flow;
                        }
                        else
                        {
                            FillOutboundMux(outbound);
                        }

                        FillOutboundTransport(outbound);
                        break;
                    }
                case EConfigType.Trojan:
                    {
                        outbound.password = node.Password;

                        FillOutboundMux(outbound);
                        FillOutboundTransport(outbound);
                        break;
                    }
                case EConfigType.Hysteria2:
                    {
                        outbound.password = node.Password;

                        if (!protocolExtra.SalamanderPass.IsNullOrEmpty())
                        {
                            outbound.obfs = new()
                            {
                                type = "salamander",
                                password = protocolExtra.SalamanderPass.TrimEx(),
                            };
                        }

                        outbound.up_mbps = protocolExtra?.UpMbps is { } su and >= 0
                            ? su
                            : context.AppConfig.HysteriaItem.UpMbps;
                        outbound.down_mbps = protocolExtra?.DownMbps is { } sd and >= 0
                            ? sd
                            : context.AppConfig.HysteriaItem.DownMbps;
                        var ports = protocolExtra?.Ports?.IsNullOrEmpty() == false ? protocolExtra.Ports : null;
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
                            outbound.hop_interval = context.AppConfig.HysteriaItem.HopInterval >= 5
                                ? $"{context.AppConfig.HysteriaItem.HopInterval}s"
                                : $"{Global.Hysteria2DefaultHopInt}s";
                            if (int.TryParse(protocolExtra.HopInterval, out var hiResult))
                            {
                                outbound.hop_interval = hiResult >= 5 ? $"{hiResult}s" : outbound.hop_interval;
                            }
                            else if (protocolExtra.HopInterval?.Contains('-') ?? false)
                            {
                                // may be a range like 5-10
                                var parts = protocolExtra.HopInterval.Split('-');
                                if (parts.Length == 2 && int.TryParse(parts[0], out var hiL) &&
                                    int.TryParse(parts[0], out var hiH))
                                {
                                    var hi = (hiL + hiH) / 2;
                                    outbound.hop_interval = hi >= 5 ? $"{hi}s" : outbound.hop_interval;
                                }
                            }
                        }

                        break;
                    }
                case EConfigType.TUIC:
                    {
                        outbound.uuid = node.Username;
                        outbound.password = node.Password;
                        outbound.congestion_control = node.HeaderType;
                        break;
                    }
                case EConfigType.Anytls:
                    {
                        outbound.password = node.Password;
                        break;
                    }
            }

            FillOutboundTls(outbound);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void FillEndpoint(Endpoints4Sbox endpoint)
    {
        try
        {
            var node = context.Node;
            var protocolExtra = node.GetProtocolExtra();

            endpoint.address = Utils.String2List(protocolExtra.WgInterfaceAddress);
            endpoint.type = Global.ProtocolTypes[node.ConfigType];

            switch (node.ConfigType)
            {
                case EConfigType.WireGuard:
                    {
                        var peer = new Peer4Sbox
                        {
                            public_key = protocolExtra.WgPublicKey,
                            pre_shared_key = protocolExtra.WgPresharedKey,
                            reserved = Utils.String2List(protocolExtra.WgReserved)?.Select(int.Parse).ToList(),
                            address = node.Address,
                            port = node.Port,
                            // TODO default ["0.0.0.0/0", "::/0"]
                            allowed_ips = new() { "0.0.0.0/0", "::/0" },
                        };
                        endpoint.private_key = node.Password;
                        endpoint.mtu = protocolExtra.WgMtu > 0 ? protocolExtra.WgMtu : Global.TunMtus.First();
                        endpoint.peers = [peer];
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void FillOutboundMux(Outbound4Sbox outbound)
    {
        try
        {
            var config = context.AppConfig;
            var muxEnabled = context.Node.MuxEnabled ?? config.CoreBasicItem.MuxEnabled;
            if (muxEnabled && config.Mux4SboxItem.Protocol.IsNotEmpty())
            {
                var mux = new Multiplex4Sbox()
                {
                    enabled = true,
                    protocol = config.Mux4SboxItem.Protocol,
                    max_connections = config.Mux4SboxItem.MaxConnections,
                    padding = config.Mux4SboxItem.Padding,
                };
                outbound.multiplex = mux;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void FillOutboundTls(Outbound4Sbox outbound)
    {
        try
        {
            var node = context.Node;
            if (node.StreamSecurity is not (Global.StreamSecurityReality or Global.StreamSecurity))
            {
                return;
            }
            if (node.ConfigType is EConfigType.Shadowsocks or EConfigType.SOCKS or EConfigType.WireGuard)
            {
                return;
            }
            var serverName = string.Empty;
            if (node.Sni.IsNotEmpty())
            {
                serverName = node.Sni;
            }
            else if (node.RequestHost.IsNotEmpty())
            {
                serverName = Utils.String2List(node.RequestHost)?.First();
            }
            var tls = new Tls4Sbox()
            {
                enabled = true,
                record_fragment = context.AppConfig.CoreBasicItem.EnableFragment ? true : null,
                server_name = serverName,
                insecure = Utils.ToBool(node.AllowInsecure.IsNullOrEmpty() ? context.AppConfig.CoreBasicItem.DefAllowInsecure.ToString().ToLower() : node.AllowInsecure),
                alpn = node.GetAlpn(),
            };
            if (node.Fingerprint.IsNotEmpty())
            {
                tls.utls = new Utls4Sbox()
                {
                    enabled = true,
                    fingerprint = node.Fingerprint.IsNullOrEmpty() ? context.AppConfig.CoreBasicItem.DefFingerprint : node.Fingerprint
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
    }

    private void FillOutboundTransport(Outbound4Sbox outbound)
    {
        try
        {
            var node = context.Node;
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
                    transport.idle_timeout = context.AppConfig.GrpcItem.IdleTimeout?.ToString("##s");
                    transport.ping_timeout = context.AppConfig.GrpcItem.HealthCheckTimeout?.ToString("##s");
                    transport.permit_without_stream = context.AppConfig.GrpcItem.PermitWithoutStream;
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
    }

    private List<Outbound4Sbox> BuildSelectorOutbounds(List<string> proxyTags, string baseTagName = Global.ProxyTag)
    {
        var multipleLoad = context.Node.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
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

        return [outSelector, outUrltest];
    }

    private List<BaseServer4Sbox> BuildOutboundsList(string baseTagName = Global.ProxyTag)
    {
        var nodes = new List<ProfileItem>();
        foreach (var nodeId in Utils.String2List(context.Node.GetProtocolExtra().ChildItems) ?? [])
        {
            if (context.AllProxiesMap.TryGetValue(nodeId, out var node))
            {
                nodes.Add(node);
            }
        }
        var resultOutbounds = new List<BaseServer4Sbox>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var currentTag = $"{baseTagName}-{i + 1}";

            if (node.ConfigType.IsGroupType())
            {
                var childProfiles = new CoreConfigSingboxService(context with { Node = node, }).BuildGroupProxyOutbounds(currentTag);
                resultOutbounds.AddRange(childProfiles);
                continue;
            }
            var outbound = new CoreConfigSingboxService(context with { Node = node, }).BuildProxyOutbound();
            outbound.tag = currentTag;
            resultOutbounds.Add(outbound);
        }
        return resultOutbounds;
    }

    private List<BaseServer4Sbox> BuildChainOutboundsList(string baseTagName = Global.ProxyTag)
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
        var resultOutbounds = new List<BaseServer4Sbox>();
        for (var i = 0; i < nodesReverse.Count; i++)
        {
            var node = nodesReverse[i];
            var currentTag = i == 0 ? baseTagName : $"chain-{baseTagName}-{i}";
            var dialerProxyTag = i != nodesReverse.Count - 1 ? $"chain-{baseTagName}-{i + 1}" : null;
            if (node.ConfigType.IsGroupType())
            {
                var childProfiles = new CoreConfigSingboxService(context with { Node = node, }).BuildGroupProxyOutbounds(currentTag);
                if (!dialerProxyTag.IsNullOrEmpty())
                {
                    var chainEndNodes =
                        childProfiles.Where(n => n?.detour.IsNullOrEmpty() ?? true);
                    foreach (var chainEndNode in chainEndNodes)
                    {
                        chainEndNode.detour = dialerProxyTag;
                    }
                }
                if (i != 0)
                {
                    var chainStartNodes = childProfiles.Where(n => n.tag.StartsWith(currentTag)).ToList();
                    if (chainStartNodes.Count == 1)
                    {
                        foreach (var existedChainEndNode in resultOutbounds.Where(n => n.detour == currentTag))
                        {
                            existedChainEndNode.detour = chainStartNodes.First().tag;
                        }
                    }
                    else if (chainStartNodes.Count > 1)
                    {
                        var existedChainNodes = CloneOutbounds(resultOutbounds);
                        resultOutbounds.Clear();
                        var j = 0;
                        foreach (var chainStartNode in chainStartNodes)
                        {
                            var existedChainNodesClone = CloneOutbounds(existedChainNodes);
                            foreach (var existedChainNode in existedChainNodesClone)
                            {
                                var cloneTag = $"{existedChainNode.tag}-clone-{j + 1}";
                                existedChainNode.tag = cloneTag;
                            }
                            for (var k = 0; k < existedChainNodesClone.Count; k++)
                            {
                                var existedChainNode = existedChainNodesClone[k];
                                var previousDialerProxyTag = existedChainNode.detour;
                                var nextTag = k + 1 < existedChainNodesClone.Count
                                    ? existedChainNodesClone[k + 1].tag
                                    : chainStartNode.tag;
                                existedChainNode.detour = (previousDialerProxyTag == currentTag)
                                    ? chainStartNode.tag
                                    : nextTag;
                                resultOutbounds.Add(existedChainNode);
                            }
                            j++;
                        }
                    }
                }
                resultOutbounds.AddRange(childProfiles);
                continue;
            }
            var outbound = new CoreConfigSingboxService(context with { Node = node, }).BuildProxyOutbound();

            outbound.tag = currentTag;

            if (!dialerProxyTag.IsNullOrEmpty())
            {
                outbound.detour = dialerProxyTag;
            }

            resultOutbounds.Add(outbound);
        }
        return resultOutbounds;
    }

    private static List<BaseServer4Sbox> CloneOutbounds(List<BaseServer4Sbox> source)
    {
        if (source is null || source.Count == 0)
        {
            return [];
        }

        var result = new List<BaseServer4Sbox>(source.Count);
        foreach (var item in source)
        {
            BaseServer4Sbox? clone = null;
            if (item is Outbound4Sbox outbound)
            {
                clone = JsonUtils.DeepCopy(outbound);
            }
            else if (item is Endpoints4Sbox endpoint)
            {
                clone = JsonUtils.DeepCopy(endpoint);
            }
            if (clone is not null)
            {
                result.Add(clone);
            }
        }
        return result;
    }

    private static void FillRangeProxy(List<BaseServer4Sbox> servers, SingboxConfig singboxConfig, bool prepend = true)
    {
        try
        {
            if (servers is null || servers.Count <= 0)
            {
                return;
            }
            var outbounds = servers.Where(s => s is Outbound4Sbox).Cast<Outbound4Sbox>().ToList();
            var endpoints = servers.Where(s => s is Endpoints4Sbox).Cast<Endpoints4Sbox>().ToList();
            singboxConfig.endpoints ??= [];
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
    }

    private static (Ech4Sbox? ech, Server4Sbox? dnsServer) ParseEchParam(string? echConfig)
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
