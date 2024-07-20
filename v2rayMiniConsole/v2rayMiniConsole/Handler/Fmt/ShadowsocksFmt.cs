using System.Text.RegularExpressions;
using v2rayN.Enums;
using v2rayN.Models;
using v2rayMiniConsole.Resx;

namespace v2rayN.Handler.Fmt
{
    internal class ShadowsocksFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;
            ProfileItem? item;

            item = ResolveSSLegacy(str) ?? ResolveSip002(str);
            if (item == null)
            {
                return null;
            }
            if (item.address.Length == 0 || item.port == 0 || item.security.Length == 0 || item.id.Length == 0)
            {
                return null;
            }

            item.configType = EConfigType.Shadowsocks;

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
            //url = string.Format("{0}:{1}@{2}:{3}",
            //    item.security,
            //    item.id,
            //    item.address,
            //    item.port);
            //url = Utile.Base64Encode(url);
            //new Sip002
            var pw = Utils.Base64Encode($"{item.security}:{item.id}");
            url = $"{pw}@{GetIpv6(item.address)}:{item.port}";
            url = $"{Global.ProtocolShares[EConfigType.Shadowsocks]}{url}{remark}";
            return url;
        }

        private static readonly Regex UrlFinder = new(@"ss://(?<base64>[A-Za-z0-9+-/=_]+)(?:#(?<tag>\S+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DetailsParser = new(@"^((?<method>.+?):(?<password>.*)@(?<hostname>.+?):(?<port>\d+?))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static ProfileItem? ResolveSSLegacy(string result)
        {
            var match = UrlFinder.Match(result);
            if (!match.Success)
                return null;

            ProfileItem item = new();
            var base64 = match.Groups["base64"].Value.TrimEnd('/');
            var tag = match.Groups["tag"].Value;
            if (!Utils.IsNullOrEmpty(tag))
            {
                item.remarks = Utils.UrlDecode(tag);
            }
            Match details;
            try
            {
                details = DetailsParser.Match(Utils.Base64Decode(base64));
            }
            catch (FormatException)
            {
                return null;
            }
            if (!details.Success)
                return null;
            item.security = details.Groups["method"].Value;
            item.id = details.Groups["password"].Value;
            item.address = details.Groups["hostname"].Value;
            item.port = Utils.ToInt(details.Groups["port"].Value);
            return item;
        }

        private static ProfileItem? ResolveSip002(string result)
        {
            Uri parsedUrl;
            try
            {
                parsedUrl = new Uri(result);
            }
            catch (UriFormatException)
            {
                return null;
            }
            ProfileItem item = new()
            {
                remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
                address = parsedUrl.IdnHost,
                port = parsedUrl.Port,
            };
            string rawUserInfo = parsedUrl.GetComponents(UriComponents.UserInfo, UriFormat.UriEscaped);
            //2022-blake3
            if (rawUserInfo.Contains(':'))
            {
                string[] userInfoParts = rawUserInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    return null;
                }
                item.security = userInfoParts[0];
                item.id = Utils.UrlDecode(userInfoParts[1]);
            }
            else
            {
                // parse base64 UserInfo
                string userInfo = Utils.Base64Decode(rawUserInfo);
                string[] userInfoParts = userInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    return null;
                }
                item.security = userInfoParts[0];
                item.id = userInfoParts[1];
            }

            var queryParameters = Utils.ParseQueryString(parsedUrl.Query);
            if (queryParameters["plugin"] != null)
            {
                //obfs-host exists
                var obfsHost = queryParameters["plugin"]?.Split(';').FirstOrDefault(t => t.Contains("obfs-host"));
                if (queryParameters["plugin"].Contains("obfs=http") && !Utils.IsNullOrEmpty(obfsHost))
                {
                    obfsHost = obfsHost?.Replace("obfs-host=", "");
                    item.network = Global.DefaultNetwork;
                    item.headerType = Global.TcpHeaderHttp;
                    item.requestHost = obfsHost ?? "";
                }
                else
                {
                    return null;
                }
            }

            return item;
        }

        public static List<ProfileItem>? ResolveSip008(string result)
        {
            //SsSIP008
            var lstSsServer = JsonUtils.Deserialize<List<SsServer>>(result);
            if (lstSsServer?.Count <= 0)
            {
                var ssSIP008 = JsonUtils.Deserialize<SsSIP008>(result);
                if (ssSIP008?.servers?.Count > 0)
                {
                    lstSsServer = ssSIP008.servers;
                }
            }

            if (lstSsServer?.Count > 0)
            {
                List<ProfileItem> lst = [];
                foreach (var it in lstSsServer)
                {
                    var ssItem = new ProfileItem()
                    {
                        remarks = it.remarks,
                        security = it.method,
                        id = it.password,
                        address = it.server,
                        port = Utils.ToInt(it.server_port)
                    };
                    lst.Add(ssItem);
                }
                return lst;
            }
            return null;
        }
    }
}