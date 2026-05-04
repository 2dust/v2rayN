namespace ServiceLib.Handler.Fmt;

public class WireguardFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        ProfileItem item = new()
        {
            ConfigType = EConfigType.WireGuard
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

        var query = Utils.ParseQueryString(url.Query);

        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            WgPublicKey = GetQueryDecoded(query, "publickey"),
            WgPresharedKey = GetQueryDecoded(query, "presharedkey"),
            WgReserved = GetQueryDecoded(query, "reserved"),
            WgInterfaceAddress = GetQueryDecoded(query, "address"),
            WgMtu = int.TryParse(GetQueryDecoded(query, "mtu"), out var mtuVal) ? mtuVal : 1280,
        });

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }

        var protoExtra = item.GetProtocolExtra();
        var dicQuery = new Dictionary<string, string>();
        if (!protoExtra.WgPublicKey.IsNullOrEmpty())
        {
            dicQuery.Add("publickey", Utils.UrlEncode(protoExtra.WgPublicKey));
        }
        if (!protoExtra.WgPresharedKey.IsNullOrEmpty())
        {
            dicQuery.Add("presharedkey", Utils.UrlEncode(protoExtra.WgPresharedKey));
        }
        if (!protoExtra.WgReserved.IsNullOrEmpty())
        {
            dicQuery.Add("reserved", Utils.UrlEncode(protoExtra.WgReserved));
        }
        if (!protoExtra.WgInterfaceAddress.IsNullOrEmpty())
        {
            dicQuery.Add("address", Utils.UrlEncode(protoExtra.WgInterfaceAddress));
        }
        dicQuery.Add("mtu", Utils.UrlEncode(protoExtra.WgMtu > 0 ? protoExtra.WgMtu.ToString() : "1280"));
        return ToUri(EConfigType.WireGuard, item.Address, item.Port, item.Password, dicQuery, remark);
    }
}
