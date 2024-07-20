using v2rayN.Enums;
using v2rayN.Models;
using v2rayMiniConsole.Resx;

namespace v2rayN.Handler.Fmt
{
    internal class Hysteria2Fmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;
            ProfileItem item = new()
            {
                configType = EConfigType.Hysteria2
            };

            Uri url = new(str);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);
            item.path = Utils.UrlDecode(query["obfs-password"] ?? "");
            item.allowInsecure = (query["insecure"] ?? "") == "1" ? "true" : "false";

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
            if (!Utils.IsNullOrEmpty(item.path))
            {
                dicQuery.Add("obfs", "salamander");
                dicQuery.Add("obfs-password", Utils.UrlEncode(item.path));
            }
            dicQuery.Add("insecure", item.allowInsecure.ToLower() == "true" ? "1" : "0");

            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            item.id,
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.Hysteria2]}{url}/{query}{remark}";
            return url;
        }

        public static ProfileItem? ResolveFull(string strData, string? subRemarks)
        {
            if (Contains(strData, "server", "up", "down", "listen", "<html>", "<body>"))
            {
                var fileName = WriteAllText(strData);

                var profileItem = new ProfileItem
                {
                    coreType = ECoreType.hysteria,
                    address = fileName,
                    remarks = subRemarks ?? "hysteria_custom"
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
                    coreType = ECoreType.hysteria2,
                    address = fileName,
                    remarks = subRemarks ?? "hysteria2_custom"
                };
                return profileItem;
            }

            return null;
        }
    }
}