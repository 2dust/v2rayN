namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenInbounds()
    {
        try
        {
            var listen = "0.0.0.0";
            var listenPort = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            _coreConfig.inbounds = [];
            var inboundConf = _config.Inbound.First();
            var inbound = BuildInbound(inboundConf, EInboundProtocol.socks, true);
            var isUsingLocalMixedPort = _node.Address == Global.Loopback && _node.Port == listenPort;

            if (!context.IsTunEnabled || !isUsingLocalMixedPort)
            {
                _coreConfig.inbounds.Add(inbound);

                if (inboundConf.SecondLocalPortEnabled)
                {
                    var inbound2 = BuildInbound(inboundConf, EInboundProtocol.socks2, true);
                    _coreConfig.inbounds.Add(inbound2);
                }

                if (inboundConf.AllowLANConn)
                {
                    if (inboundConf.NewPort4LAN)
                    {
                        var inbound3 = BuildInbound(inboundConf, EInboundProtocol.socks3, true);
                        inbound3.listen = listen;
                        _coreConfig.inbounds.Add(inbound3);

                        // auth
                        if (inboundConf.User.IsNotEmpty() && inboundConf.Pass.IsNotEmpty())
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts =
                            [
                                new()
                                {
                                    user = inboundConf.User,
                                    pass = inboundConf.Pass,
                                },

                            ];
                        }
                    }
                    else
                    {
                        inbound.listen = listen;
                    }
                }
            }

            if (context.IsTunEnabled)
            {
                if (_config.TunModeItem.Mtu <= 0)
                {
                    _config.TunModeItem.Mtu = Global.TunMtus.First();
                }
                var tunInbound =
                    JsonUtils.Deserialize<Inbounds4Ray>(EmbedUtils.GetEmbedText(Global.V2raySampleTunInbound)) ??
                    new Inbounds4Ray();
                tunInbound.settings.name = context.IsMacOS ? $"utun{new Random().Next(99)}" : "xray_tun";
                tunInbound.settings.MTU = _config.TunModeItem.Mtu;

                var address = _config.TunModeItem.IPv4Address.NullIfEmpty() ?? Global.TunIPv4Address.First();
                tunInbound.settings.gateway = [address];
                // Route both families into the tunnel regardless of EnableIPv6Address. That option only
                // controls whether the interface gets an IPv6 address; leaving ::/0 out of the routing
                // table makes IPv6 follow the system default route and bypass the tunnel entirely.
                // A host without a global IPv6 address is the exception: it has nothing to leak,
                // and IPv6 sent into the tunnel would have no way back out.
                tunInbound.settings.autoSystemRoutingTable = context.HasGlobalIPv6Address
                    ? ["0.0.0.0/0", "::/0"]
                    : ["0.0.0.0/0"];
                if (_config.TunModeItem.EnableIPv6Address == true)
                {
                    var address6 = _config.TunModeItem.IPv6Address.NullIfEmpty() ?? Global.TunIPv6Address.First();
                    tunInbound.settings.gateway.Add(address6);
                }

                var bindInterface = _config.CoreBasicItem.BindInterface?.TrimEx();
                if (!bindInterface.IsNullOrEmpty())
                {
                    tunInbound.settings.autoOutboundsInterface = bindInterface;
                }
                tunInbound.sniffing = inbound.sniffing;
                // tunInbound.sniffing.routeOnly = inbound.sniffing.routeOnly;
                tunInbound.sniffing.routeOnly = true;

                if (_config.TunModeItem.RouteExcludeAddress is { Count: > 0 })
                {
                    var wholeInternet = IPNetwork2.Parse("0.0.0.0/0");
                    var wholeInternetV6 = IPNetwork2.Parse("::/0");

                    var excludeList = _config.TunModeItem.RouteExcludeAddress.Select(IPNetwork2.Parse)
                        .Where(x => x != null).ToList();

                    var includeList = new List<IPNetwork2> { wholeInternet };
                    var includeListV6 = context.HasGlobalIPv6Address
                        ? new List<IPNetwork2> { wholeInternetV6 }
                        : new List<IPNetwork2>();

                    foreach (var exclude in excludeList)
                    {
                        var temp = new List<IPNetwork2>();
                        if (exclude.AddressFamily == AddressFamily.InterNetwork)
                        {
                            foreach (var net in includeList)
                            {
                                temp.AddRange(net.Subtract(exclude));
                            }
                            includeList = temp;
                        }
                        else if (exclude.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            foreach (var net in includeListV6)
                            {
                                temp.AddRange(net.Subtract(exclude));
                            }
                            includeListV6 = temp;
                        }
                    }

                    includeList = IPNetwork2.Supernet(includeList.ToArray()).ToList();
                    includeListV6 = IPNetwork2.Supernet(includeListV6.ToArray()).ToList();

                    tunInbound.settings.autoSystemRoutingTable = includeList.Select(x => x.ToString())
                        .Concat(includeListV6.Select(x => x.ToString())).ToList();
                }

                _coreConfig.inbounds.Add(tunInbound);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private Inbounds4Ray BuildInbound(InItem inItem, EInboundProtocol protocol, bool bSocks)
    {
        var result = EmbedUtils.GetEmbedText(Global.V2raySampleInbound);
        if (result.IsNullOrEmpty())
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
        inbound.protocol = nameof(EInboundProtocol.mixed);
        inbound.settings.udp = inItem.UdpEnabled;
        inbound.sniffing.enabled = inItem.SniffingEnabled;
        inbound.sniffing.destOverride = inItem.DestOverride;
        inbound.sniffing.routeOnly = inItem.RouteOnly;

        if (_config.SimpleDNSItem.FakeIP == true)
        {
            // Ensure destOverride contains "fakedns" if FakeIP is enabled
            inbound.sniffing.destOverride ??= [];
            if (!inbound.sniffing.destOverride.Contains("fakedns"))
            {
                inbound.sniffing.destOverride.Add("fakedns");
            }
        }

        return inbound;
    }
}
