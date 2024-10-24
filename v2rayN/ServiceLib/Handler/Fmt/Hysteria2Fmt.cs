namespace ServiceLib.Handler.Fmt
{
    public class Hysteria2Fmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;
            ProfileItem item = new()
            {
                ConfigType = EConfigType.Hysteria2
            };

            var url = Utils.TryUri(str);
            if (url == null) return null;

            item.Address = url.IdnHost;
            item.Port = url.Port;
            item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.Id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);
            item.Path = Utils.UrlDecode(query["obfs-password"] ?? "");
            item.AllowInsecure = (query["insecure"] ?? "") == "1" ? "true" : "false";

            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null) return null;
            string url = string.Empty;

            string remark = string.Empty;
            if (Utils.IsNotEmpty(item.Remarks))
            {
                remark = "#" + Utils.UrlEncode(item.Remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (Utils.IsNotEmpty(item.Sni))
            {
                dicQuery.Add("sni", item.Sni);
            }
            if (Utils.IsNotEmpty(item.Alpn))
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.Alpn));
            }
            if (Utils.IsNotEmpty(item.Path))
            {
                dicQuery.Add("obfs", "salamander");
                dicQuery.Add("obfs-password", Utils.UrlEncode(item.Path));
            }
            dicQuery.Add("insecure", item.AllowInsecure.ToLower() == "true" ? "1" : "0");

            return ToUri(EConfigType.Hysteria2, item.Address, item.Port, item.Id, dicQuery, remark);
        }

        public static ProfileItem? ResolveFull(string strData, string? subRemarks)
        {
            if (Contains(strData, "server", "up", "down", "listen", "<html>", "<body>"))
            {
                var fileName = WriteAllText(strData);

                var profileItem = new ProfileItem
                {
                    CoreType = ECoreType.hysteria,
                    Address = fileName,
                    Remarks = subRemarks ?? "hysteria_custom"
                };
                return profileItem;
            }

            return null;
        }

        public static ProfileItem? ResolveFull2(string strData, string? subRemarks)
        {
            if (Contains(strData, "server", "auth", "up", "down", "listen"))
            {
                var fileName = WriteAllText(strData);

                var profileItem = new ProfileItem
                {
                    CoreType = ECoreType.hysteria2,
                    Address = fileName,
                    Remarks = subRemarks ?? "hysteria2_custom"
                };
                return profileItem;
            }

            return null;
        }
    }
}