namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenInbounds()
    {
        try
        {
            var listen = "0.0.0.0";
            _coreConfig.inbounds = [];

            var inbound = BuildInbound(_config.Inbound.First(), EInboundProtocol.socks, true);
            _coreConfig.inbounds.Add(inbound);

            if (_config.Inbound.First().SecondLocalPortEnabled)
            {
                var inbound2 = BuildInbound(_config.Inbound.First(), EInboundProtocol.socks2, true);
                _coreConfig.inbounds.Add(inbound2);
            }

            if (_config.Inbound.First().AllowLANConn)
            {
                if (_config.Inbound.First().NewPort4LAN)
                {
                    var inbound3 = BuildInbound(_config.Inbound.First(), EInboundProtocol.socks3, true);
                    inbound3.listen = listen;
                    _coreConfig.inbounds.Add(inbound3);

                    //auth
                    if (_config.Inbound.First().User.IsNotEmpty() && _config.Inbound.First().Pass.IsNotEmpty())
                    {
                        inbound3.settings.auth = "password";
                        inbound3.settings.accounts = new List<AccountsItem4Ray> { new() { user = _config.Inbound.First().User, pass = _config.Inbound.First().Pass } };
                    }
                }
                else
                {
                    inbound.listen = listen;
                }
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
        inbound.protocol = EInboundProtocol.mixed.ToString();
        inbound.settings.udp = inItem.UdpEnabled;
        inbound.sniffing.enabled = inItem.SniffingEnabled;
        inbound.sniffing.destOverride = inItem.DestOverride;
        inbound.sniffing.routeOnly = inItem.RouteOnly;

        return inbound;
    }
}
