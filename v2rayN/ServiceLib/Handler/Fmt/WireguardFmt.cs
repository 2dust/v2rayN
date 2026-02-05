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

        var dicQuery = new Dictionary<string, string>();
        if (!item.GetProtocolExtra().WgPublicKey.IsNullOrEmpty())
        {
            dicQuery.Add("publickey", Utils.UrlEncode(item.GetProtocolExtra().WgPublicKey));
        }
        if (!item.GetProtocolExtra().WgReserved.IsNullOrEmpty())
        {
            dicQuery.Add("reserved", Utils.UrlEncode(item.GetProtocolExtra().WgReserved));
        }
        if (!item.GetProtocolExtra().WgInterfaceAddress.IsNullOrEmpty())
        {
            dicQuery.Add("address", Utils.UrlEncode(item.GetProtocolExtra().WgInterfaceAddress));
        }
        dicQuery.Add("mtu", Utils.UrlEncode(item.GetProtocolExtra().WgMtu > 0 ? item.GetProtocolExtra().WgMtu.ToString() : "1280"));
        return ToUri(EConfigType.WireGuard, item.Address, item.Port, item.Password, dicQuery, remark);
    }
}
