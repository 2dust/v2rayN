namespace ServiceLib.Handler.Fmt
{
    public class TuicFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;

            ProfileItem item = new()
            {
                configType = EConfigType.TUIC
            };

            Uri url = new(str);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            var userInfoParts = url.UserInfo.Split(new[] { ':' }, 2);
            if (userInfoParts.Length == 2)
            {
                item.id = userInfoParts[0];
                item.security = userInfoParts[1];
            }

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);
            item.headerType = query["congestion_control"] ?? "";

            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null) return null;
            string url = string.Empty;

            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (!Utils.IsNullOrEmpty(item.sni))
            {
                dicQuery.Add("sni", item.sni);
            }
            if (!Utils.IsNullOrEmpty(item.alpn))
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.alpn));
            }
            dicQuery.Add("congestion_control", item.headerType);

            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            $"{item.id}:{item.security}",
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.TUIC]}{url}{query}{remark}";
            return url;
        }
    }
}