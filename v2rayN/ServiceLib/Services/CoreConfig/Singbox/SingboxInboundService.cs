namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private void GenInbounds()
    {
        try
        {
            var listen = "0.0.0.0";
            _coreConfig.inbounds = [];

            if (!context.AppConfig.TunModeItem.EnableTun
                || (context.AppConfig.TunModeItem.EnableTun && context.AppConfig.TunModeItem.EnableExInbound && AppManager.Instance.RunningCoreType == ECoreType.sing_box))
            {
                var inbound = new Inbound4Sbox()
                {
                    type = EInboundProtocol.mixed.ToString(),
                    tag = EInboundProtocol.socks.ToString(),
                    listen = Global.Loopback,
                };
                _coreConfig.inbounds.Add(inbound);

                inbound.listen_port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);

                if (context.AppConfig.Inbound.First().SecondLocalPortEnabled)
                {
                    var inbound2 = BuildInbound(inbound, EInboundProtocol.socks2, true);
                    _coreConfig.inbounds.Add(inbound2);
                }

                if (context.AppConfig.Inbound.First().AllowLANConn)
                {
                    if (context.AppConfig.Inbound.First().NewPort4LAN)
                    {
                        var inbound3 = BuildInbound(inbound, EInboundProtocol.socks3, true);
                        inbound3.listen = listen;
                        _coreConfig.inbounds.Add(inbound3);

                        //auth
                        if (context.AppConfig.Inbound.First().User.IsNotEmpty() && context.AppConfig.Inbound.First().Pass.IsNotEmpty())
                        {
                            inbound3.users = new() { new() { username = context.AppConfig.Inbound.First().User, password = context.AppConfig.Inbound.First().Pass } };
                        }
                    }
                    else
                    {
                        inbound.listen = listen;
                    }
                }
            }

            if (context.AppConfig.TunModeItem.EnableTun)
            {
                if (context.AppConfig.TunModeItem.Mtu <= 0)
                {
                    context.AppConfig.TunModeItem.Mtu = Global.TunMtus.First();
                }
                if (context.AppConfig.TunModeItem.Stack.IsNullOrEmpty())
                {
                    context.AppConfig.TunModeItem.Stack = Global.TunStacks.First();
                }

                var tunInbound = JsonUtils.Deserialize<Inbound4Sbox>(EmbedUtils.GetEmbedText(Global.TunSingboxInboundFileName)) ?? new Inbound4Sbox { };
                tunInbound.interface_name = Utils.IsMacOS() ? $"utun{new Random().Next(99)}" : "singbox_tun";
                tunInbound.mtu = context.AppConfig.TunModeItem.Mtu;
                tunInbound.auto_route = context.AppConfig.TunModeItem.AutoRoute;
                tunInbound.strict_route = context.AppConfig.TunModeItem.StrictRoute;
                tunInbound.stack = context.AppConfig.TunModeItem.Stack;
                if (context.AppConfig.TunModeItem.EnableIPv6Address == false)
                {
                    tunInbound.address = ["172.18.0.1/30"];
                }

                _coreConfig.inbounds.Add(tunInbound);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private Inbound4Sbox BuildInbound(Inbound4Sbox inItem, EInboundProtocol protocol, bool bSocks)
    {
        var inbound = JsonUtils.DeepCopy(inItem);
        inbound.tag = protocol.ToString();
        inbound.listen_port = inItem.listen_port + (int)protocol;
        inbound.type = EInboundProtocol.mixed.ToString();
        return inbound;
    }
}
