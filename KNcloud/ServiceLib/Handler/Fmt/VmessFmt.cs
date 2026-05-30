namespace ServiceLib.Handler.Fmt;

public class VmessFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        ProfileItem? item;
        if (str.IndexOf('@') > 0)
        {
            item = ResolveStdVmess(str) ?? ResolveVmess(str, out msg);
        }
        else
        {
            item = ResolveVmess(str, out msg);
        }
        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var vmessQRCode = new VmessQRCode
        {
            // vmess link keeps shared transport keys; map from new transport model on export.
            v = 2,
            ps = item.Remarks.TrimEx(),
            add = item.Address,
            port = item.Port,
            id = item.Password,
            aid = int.TryParse(item.GetProtocolExtra()?.AlterId, out var result) ? result : 0,
            scy = item.GetProtocolExtra().VmessSecurity ?? "",
            net = item.GetNetwork() == nameof(ETransport.raw) ? Global.RawNetworkAlias : item.Network,
            type = item.GetNetwork() switch
            {
                nameof(ETransport.raw) => item.GetTransportExtra().RawHeaderType,
                nameof(ETransport.kcp) => item.GetTransportExtra().KcpHeaderType,
                nameof(ETransport.xhttp) => item.GetTransportExtra().XhttpMode,
                nameof(ETransport.grpc) => item.GetTransportExtra().GrpcMode,
                _ => Global.None,
            },
            host = item.GetNetwork() switch
            {
                nameof(ETransport.raw) => item.GetTransportExtra().Host,
                nameof(ETransport.ws) => item.GetTransportExtra().Host,
                nameof(ETransport.httpupgrade) => item.GetTransportExtra().Host,
                nameof(ETransport.xhttp) => item.GetTransportExtra().Host,
                nameof(ETransport.grpc) => item.GetTransportExtra().GrpcAuthority,
                _ => null,
            },
            path = item.GetNetwork() switch
            {
                nameof(ETransport.raw) => item.GetTransportExtra().Path,
                nameof(ETransport.kcp) => item.GetTransportExtra().KcpSeed,
                nameof(ETransport.ws) => item.GetTransportExtra().Path,
                nameof(ETransport.httpupgrade) => item.GetTransportExtra().Path,
                nameof(ETransport.xhttp) => item.GetTransportExtra().Path,
                nameof(ETransport.grpc) => item.GetTransportExtra().GrpcServiceName,
                _ => null,
            },
            tls = item.StreamSecurity,
            sni = item.Sni,
            alpn = item.Alpn,
            fp = item.Fingerprint,
            insecure = item.AllowInsecure.Equals(Global.AllowInsecure.First()) ? "1" : "0"
        };

        var url = JsonUtils.Serialize(vmessQRCode);
        url = Utils.Base64Encode(url);
        url = $"{Global.ProtocolShares[EConfigType.VMess]}{url}";

        return url;
    }

    private static ProfileItem? ResolveVmess(string result, out string msg)
    {
        msg = string.Empty;
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VMess
        };

        result = result[Global.ProtocolShares[EConfigType.VMess].Length..];
        result = Utils.Base64Decode(result);

        var vmessQRCode = JsonUtils.Deserialize<VmessQRCode>(result);
        if (vmessQRCode == null)
        {
            msg = ResUI.FailedConversionConfiguration;
            return null;
        }

        item.Network = Global.DefaultNetwork;
        var transport = new TransportExtraItem
        {
            RawHeaderType = Global.None,
        };

        //item.ConfigVersion = vmessQRCode.v;
        item.Remarks = Utils.ToString(vmessQRCode.ps);
        item.Address = Utils.ToString(vmessQRCode.add);
        item.Port = vmessQRCode.port;
        item.Password = Utils.ToString(vmessQRCode.id);
        item.SetProtocolExtra(new ProtocolExtraItem
        {
            AlterId = vmessQRCode.aid.ToString(),
            VmessSecurity = vmessQRCode.scy.IsNullOrEmpty() ? Global.DefaultSecurity : vmessQRCode.scy,
        });
        if (vmessQRCode.net.IsNotEmpty())
        {
            item.Network = vmessQRCode.net == Global.RawNetworkAlias ? nameof(ETransport.raw) : vmessQRCode.net;
        }
        if (vmessQRCode.type.IsNotEmpty())
        {
            transport = item.GetNetwork() switch
            {
                nameof(ETransport.raw) => transport with { RawHeaderType = vmessQRCode.type },
                nameof(ETransport.kcp) => transport with { KcpHeaderType = vmessQRCode.type },
                nameof(ETransport.xhttp) => transport with { XhttpMode = vmessQRCode.type },
                nameof(ETransport.grpc) => transport with { GrpcMode = vmessQRCode.type },
                _ => transport,
            };
        }
        transport = item.GetNetwork() switch
        {
            nameof(ETransport.raw) => transport with { Host = Utils.ToString(vmessQRCode.host), Path = Utils.ToString(vmessQRCode.path) },
            nameof(ETransport.kcp) => transport with { KcpSeed = Utils.ToString(vmessQRCode.path) },
            nameof(ETransport.ws) => transport with { Host = Utils.ToString(vmessQRCode.host), Path = Utils.ToString(vmessQRCode.path) },
            nameof(ETransport.httpupgrade) => transport with { Host = Utils.ToString(vmessQRCode.host), Path = Utils.ToString(vmessQRCode.path) },
            nameof(ETransport.xhttp) => transport with { Host = Utils.ToString(vmessQRCode.host), Path = Utils.ToString(vmessQRCode.path) },
            nameof(ETransport.grpc) => transport with { GrpcAuthority = Utils.ToString(vmessQRCode.host), GrpcServiceName = Utils.ToString(vmessQRCode.path) },
            _ => transport,
        };
        item.SetTransportExtra(transport);
        item.StreamSecurity = Utils.ToString(vmessQRCode.tls);
        item.Sni = Utils.ToString(vmessQRCode.sni);
        item.Alpn = Utils.ToString(vmessQRCode.alpn);
        item.Fingerprint = Utils.ToString(vmessQRCode.fp);
        item.AllowInsecure = vmessQRCode.insecure == "1" ? Global.AllowInsecure.First() : string.Empty;

        return item;
    }

    public static ProfileItem? ResolveStdVmess(string str)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VMess,
        };

        var url = Utils.TryUri(str);
        if (url == null)
        {
            return null;
        }

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        item.Password = Utils.UrlDecode(url.UserInfo);

        item.SetProtocolExtra(new ProtocolExtraItem
        {
            VmessSecurity = Global.DefaultSecurity,
        });

        var query = Utils.ParseQueryString(url.Query);
        ResolveUriQuery(query, ref item);

        return item;
    }
}
