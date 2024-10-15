namespace ServiceLib.Handler.Fmt
{
    public class TrojanFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;

            ProfileItem item = new()
            {
                configType = EConfigType.Trojan
            };

            Uri url = new(str);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);

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
            GetStdTransport(item, null, ref dicQuery);

            return ToUri(EConfigType.Trojan, item.address, item.port, item.id, dicQuery, remark);
        }
    }
}