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

        var protocolExtra = item.GetProtocolExtra();

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        item.Password = Utils.UrlDecode(url.UserInfo);

        var query = Utils.ParseQueryString(url.Query);

        protocolExtra.WgPublicKey = GetQueryDecoded(query, "publickey");
        protocolExtra.WgReserved = GetQueryDecoded(query, "reserved");
        protocolExtra.WgInterfaceAddress = GetQueryDecoded(query, "address");
        protocolExtra.WgMtu = int.TryParse(GetQueryDecoded(query, "mtu"), out var mtu) ? mtu : 1280;
        protocolExtra.WgPresharedKey = GetQueryDecoded(query, "presharedKey");

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var protocolExtra = item.GetProtocolExtra();

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }

        var dicQuery = new Dictionary<string, string>();
        if (!protocolExtra.WgPublicKey.IsNullOrEmpty())
        {
            dicQuery.Add("publickey", Utils.UrlEncode(protocolExtra.WgPublicKey));
        }
        if (!protocolExtra.WgReserved.IsNullOrEmpty())
        {
            dicQuery.Add("reserved", Utils.UrlEncode(protocolExtra.WgReserved));
        }
        if (!protocolExtra.WgInterfaceAddress.IsNullOrEmpty())
        {
            dicQuery.Add("address", Utils.UrlEncode(protocolExtra.WgInterfaceAddress));
        }
        dicQuery.Add("mtu", Utils.UrlEncode(protocolExtra.WgMtu > 0 ? protocolExtra.WgMtu.ToString() : "1280"));
        return ToUri(EConfigType.WireGuard, item.Address, item.Port, item.Password, dicQuery, remark);
    }
}
