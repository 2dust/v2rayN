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
            var rawUserInfo = Utils.UrlDecode(url.UserInfo);
            var userInfoParts = rawUserInfo.Split(new[] { ':' }, 2);
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
            if (Utils.IsNotEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (Utils.IsNotEmpty(item.sni))
            {
                dicQuery.Add("sni", item.sni);
            }
            if (Utils.IsNotEmpty(item.alpn))
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.alpn));
            }
            dicQuery.Add("congestion_control", item.headerType);

            return ToUri(EConfigType.TUIC, item.address, item.port, $"{item.id}:{item.security}", dicQuery, remark);
        }
    }
}