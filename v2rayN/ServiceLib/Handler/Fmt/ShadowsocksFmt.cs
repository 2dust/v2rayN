using System.Text.RegularExpressions;

namespace ServiceLib.Handler.Fmt
{
    public class ShadowsocksFmt : BaseFmt
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
            if (item.Address.Length == 0 || item.Port == 0 || item.Security.Length == 0 || item.Id.Length == 0)
            {
                return null;
            }

            item.ConfigType = EConfigType.Shadowsocks;

            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null)
            {
                return null;
            }
            var remark = string.Empty;
            if (item.Remarks.IsNotEmpty())
            {
                remark = "#" + Utils.UrlEncode(item.Remarks);
            }
            //url = string.Format("{0}:{1}@{2}:{3}",
            //    item.security,
            //    item.id,
            //    item.address,
            //    item.port);
            //url = Utile.Base64Encode(url);
            //new Sip002
            var pw = Utils.Base64Encode($"{item.Security}:{item.Id}");
            return ToUri(EConfigType.Shadowsocks, item.Address, item.Port, pw, null, remark);
        }

        private static readonly Regex UrlFinder = new(@"ss://(?<base64>[A-Za-z0-9+-/=_]+)(?:#(?<tag>\S+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DetailsParser = new(@"^((?<method>.+?):(?<password>.*)@(?<hostname>.+?):(?<port>\d+?))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static ProfileItem? ResolveSSLegacy(string result)
        {
            var match = UrlFinder.Match(result);
            if (!match.Success)
            {
                return null;
            }

            ProfileItem item = new();
            var base64 = match.Groups["base64"].Value.TrimEnd('/');
            var tag = match.Groups["tag"].Value;
            if (tag.IsNotEmpty())
            {
                item.Remarks = Utils.UrlDecode(tag);
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
            {
                return null;
            }
            item.Security = details.Groups["method"].Value;
            item.Id = details.Groups["password"].Value;
            item.Address = details.Groups["hostname"].Value;
            item.Port = details.Groups["port"].Value.ToInt();
            return item;
        }

        private static ProfileItem? ResolveSip002(string result)
        {
            var parsedUrl = Utils.TryUri(result);
            if (parsedUrl == null)
            {
                return null;
            }

            ProfileItem item = new()
            {
                Remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
                Address = parsedUrl.IdnHost,
                Port = parsedUrl.Port,
            };
            var rawUserInfo = Utils.UrlDecode(parsedUrl.UserInfo);
            //2022-blake3
            if (rawUserInfo.Contains(':'))
            {
                var userInfoParts = rawUserInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    return null;
                }
                item.Security = userInfoParts.First();
                item.Id = Utils.UrlDecode(userInfoParts.Last());
            }
            else
            {
                // parse base64 UserInfo
                var userInfo = Utils.Base64Decode(rawUserInfo);
                var userInfoParts = userInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    return null;
                }
                item.Security = userInfoParts.First();
                item.Id = userInfoParts.Last();
            }

            var queryParameters = Utils.ParseQueryString(parsedUrl.Query);
            if (queryParameters["plugin"] != null)
            {
                //obfs-host exists
                var obfsHost = queryParameters["plugin"]?.Split(';').FirstOrDefault(t => t.Contains("obfs-host"));
                if (queryParameters["plugin"].Contains("obfs=http") && obfsHost.IsNotEmpty())
                {
                    obfsHost = obfsHost?.Replace("obfs-host=", "");
                    item.Network = Global.DefaultNetwork;
                    item.HeaderType = Global.TcpHeaderHttp;
                    item.RequestHost = obfsHost ?? "";
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
                        Remarks = it.remarks,
                        Security = it.method,
                        Id = it.password,
                        Address = it.server,
                        Port = it.server_port.ToInt()
                    };
                    lst.Add(ssItem);
                }
                return lst;
            }
            return null;
        }
    }
}
