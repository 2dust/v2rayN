using v2rayN.Enums;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.Handler.Fmt
{
    internal class VLESSFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;

            ProfileItem item = new()
            {
                configType = EConfigType.VLESS,
                security = Global.None
            };

            Uri url = new(str);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            item.security = query["encryption"] ?? Global.None;
            item.streamSecurity = query["security"] ?? "";
            ResolveStdTransport(query, ref item);

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
            if (!Utils.IsNullOrEmpty(item.security))
            {
                dicQuery.Add("encryption", item.security);
            }
            else
            {
                dicQuery.Add("encryption", Global.None);
            }
            GetStdTransport(item, Global.None, ref dicQuery);
            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            item.id,
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.VLESS]}{url}{query}{remark}";
            return url;
        }
    }
}