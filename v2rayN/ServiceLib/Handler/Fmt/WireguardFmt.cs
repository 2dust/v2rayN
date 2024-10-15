namespace ServiceLib.Handler.Fmt
{
    public class WireguardFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;

            ProfileItem item = new()
            {
                configType = EConfigType.WireGuard
            };

            Uri url = new(str);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);

            item.publicKey = Utils.UrlDecode(query["publickey"] ?? "");
            item.path = Utils.UrlDecode(query["reserved"] ?? "");
            item.requestHost = Utils.UrlDecode(query["address"] ?? "");
            item.shortId = Utils.UrlDecode(query["mtu"] ?? "");

            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null) return null;
            string url = string.Empty;

            string remark = string.Empty;
            if (Utils.IsNotEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }

            var dicQuery = new Dictionary<string, string>();
            if (Utils.IsNotEmpty(item.publicKey))
            {
                dicQuery.Add("publickey", Utils.UrlEncode(item.publicKey));
            }
            if (Utils.IsNotEmpty(item.path))
            {
                dicQuery.Add("reserved", Utils.UrlEncode(item.path));
            }
            if (Utils.IsNotEmpty(item.requestHost))
            {
                dicQuery.Add("address", Utils.UrlEncode(item.requestHost));
            }
            if (Utils.IsNotEmpty(item.shortId))
            {
                dicQuery.Add("mtu", Utils.UrlEncode(item.shortId));
            }
            return ToUri(EConfigType.WireGuard, item.address, item.port, item.id, dicQuery, remark);
        }
    }
}