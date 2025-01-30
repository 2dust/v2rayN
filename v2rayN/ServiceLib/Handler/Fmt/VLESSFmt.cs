namespace ServiceLib.Handler.Fmt
{
    public class VLESSFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;

            ProfileItem item = new()
            {
                ConfigType = EConfigType.VLESS,
                Security = Global.None
            };

            var url = Utils.TryUri(str);
            if (url == null)
                return null;

            item.Address = url.IdnHost;
            item.Port = url.Port;
            item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.Id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            item.Security = query["encryption"] ?? Global.None;
            item.StreamSecurity = query["security"] ?? "";
            ResolveStdTransport(query, ref item);

            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null)
                return null;
            string url = string.Empty;

            string remark = string.Empty;
            if (Utils.IsNotEmpty(item.Remarks))
            {
                remark = "#" + Utils.UrlEncode(item.Remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (Utils.IsNotEmpty(item.Security))
            {
                dicQuery.Add("encryption", item.Security);
            }
            else
            {
                dicQuery.Add("encryption", Global.None);
            }
            GetStdTransport(item, Global.None, ref dicQuery);

            return ToUri(EConfigType.VLESS, item.Address, item.Port, item.Id, dicQuery, remark);
        }
    }
}
