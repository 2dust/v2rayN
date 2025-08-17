namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<int> GenInbounds(SingboxConfig singboxConfig)
    {
        try
        {
            var listen = "0.0.0.0";
            singboxConfig.inbounds = [];

            if (!_config.TunModeItem.EnableTun
                || _config.TunModeItem.EnableTun && _config.TunModeItem.EnableExInbound && _config.RunningCoreType == ECoreType.sing_box)
            {
                var inbound = new Inbound4Sbox()
                {
                    type = EInboundProtocol.mixed.ToString(),
                    tag = EInboundProtocol.socks.ToString(),
                    listen = Global.Loopback,
                };
                singboxConfig.inbounds.Add(inbound);

                inbound.listen_port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);

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
                tunInbound.auto_route = _config.TunModeItem.AutoRoute;
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
}
