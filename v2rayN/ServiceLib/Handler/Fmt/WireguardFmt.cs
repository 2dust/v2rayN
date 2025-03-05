namespace ServiceLib.Handler.Fmt
{
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
                return null;

            item.Address = url.IdnHost;
            item.Port = url.Port;
            item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.Id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);

            item.PublicKey = Utils.UrlDecode(query["publickey"] ?? "");
            item.Path = Utils.UrlDecode(query["reserved"] ?? "");
            item.RequestHost = Utils.UrlDecode(query["address"] ?? "");
            item.ShortId = Utils.UrlDecode(query["mtu"] ?? "");

            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null)
                return null;
            string url = string.Empty;

            string remark = string.Empty;
            if (item.Remarks.IsNotEmpty())
            {
                remark = "#" + Utils.UrlEncode(item.Remarks);
            }

            var dicQuery = new Dictionary<string, string>();
            if (item.PublicKey.IsNotEmpty())
            {
                dicQuery.Add("publickey", Utils.UrlEncode(item.PublicKey));
            }
            if (item.Path.IsNotEmpty())
            {
                dicQuery.Add("reserved", Utils.UrlEncode(item.Path));
            }
            if (item.RequestHost.IsNotEmpty())
            {
                dicQuery.Add("address", Utils.UrlEncode(item.RequestHost));
            }
            if (item.ShortId.IsNotEmpty())
            {
                dicQuery.Add("mtu", Utils.UrlEncode(item.ShortId));
            }
            return ToUri(EConfigType.WireGuard, item.Address, item.Port, item.Id, dicQuery, remark);
        }
    }
}
