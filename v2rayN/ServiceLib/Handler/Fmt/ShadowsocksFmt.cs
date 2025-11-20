namespace ServiceLib.Handler.Fmt;

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
        var pw = Utils.Base64Encode($"{item.Security}:{item.Id}", true);

        // plugin
        var plugin = string.Empty;
        var pluginArgs = string.Empty;

        if (item.Network == nameof(ETransport.tcp) && item.HeaderType == Global.TcpHeaderHttp)
        {
            plugin = "obfs-local";
            pluginArgs = $"obfs=http;obfs-host={item.RequestHost};";
        }
        else
        {
            if (item.Network == nameof(ETransport.ws))
            {
                pluginArgs += "mode=websocket;";
                pluginArgs += $"host={item.RequestHost};";
                pluginArgs += $"path={item.Path};";
            }
            else if (item.Network == nameof(ETransport.quic))
            {
                pluginArgs += "mode=quic;";
            }
            if (item.StreamSecurity == Global.StreamSecurity)
            {
                pluginArgs += "tls;";
                var certs = CertPemManager.ParsePemChain(item.Cert);
                if (certs.Count > 0)
                {
                    var cert = certs.First();
                    const string beginMarker = "-----BEGIN CERTIFICATE-----\n";
                    const string endMarker = "\n-----END CERTIFICATE-----";

                    var base64Start = beginMarker.Length;
                    var endIndex = cert.IndexOf(endMarker, base64Start, StringComparison.Ordinal);
                    var base64Content = cert.Substring(base64Start, endIndex - base64Start);

                    pluginArgs += $"certRaw={base64Content};";
                }
            }
            if (pluginArgs.Length > 0)
            {
                plugin = "v2ray-plugin";
            }
        }

        var dicQuery = new Dictionary<string, string>();
        if (plugin.IsNotEmpty())
        {
            dicQuery["plugin"] = plugin + (pluginArgs.IsNotEmpty() ? (";" + pluginArgs) : "");
        }

        return ToUri(EConfigType.Shadowsocks, item.Address, item.Port, pw, dicQuery, remark);
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
            var pluginStr = queryParameters["plugin"];
            var pluginParts = pluginStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (pluginParts.Length == 0)
            {
                return null;
            }

            var pluginName = pluginParts[0];

            // A typo in https://github.com/shadowsocks/shadowsocks-org/blob/6b1c064db4129de99c516294960e731934841c94/docs/doc/sip002.md?plain=1#L15
            // "simple-obfs" should be "obfs-local"
            if (pluginName == "simple-obfs")
            {
                pluginName = "obfs-local";
            }

            // Parse obfs-local plugin
            if (pluginName == "obfs-local")
            {
                var obfsMode = pluginParts.FirstOrDefault(t => t.StartsWith("obfs="));
                var obfsHost = pluginParts.FirstOrDefault(t => t.StartsWith("obfs-host="));

                if ((!obfsMode.IsNullOrEmpty()) && obfsMode.Contains("obfs=http") && obfsHost.IsNotEmpty())
                {
                    obfsHost = obfsHost.Replace("obfs-host=", "");
                    item.Network = Global.DefaultNetwork;
                    item.HeaderType = Global.TcpHeaderHttp;
                    item.RequestHost = obfsHost;
                }
            }
            // Parse v2ray-plugin
            else if (pluginName == "v2ray-plugin")
            {
                var mode = pluginParts.FirstOrDefault(t => t.StartsWith("mode="), "websocket");
                var host = pluginParts.FirstOrDefault(t => t.StartsWith("host="));
                var path = pluginParts.FirstOrDefault(t => t.StartsWith("path="));
                var hasTls = pluginParts.Any(t => t == "tls");
                var certRaw = pluginParts.FirstOrDefault(t => t.StartsWith("certRaw="));

                var modeValue = mode.Replace("mode=", "");
                if (modeValue == "websocket")
                {
                    item.Network = nameof(ETransport.ws);
                    if (!host.IsNullOrEmpty())
                    {
                        item.RequestHost = host.Replace("host=", "");
                    }
                    if (!path.IsNullOrEmpty())
                    {
                        item.Path = path.Replace("path=", "");
                    }
                }
                else if (modeValue == "quic")
                {
                    item.Network = nameof(ETransport.quic);
                }

                if (hasTls)
                {
                    item.StreamSecurity = Global.StreamSecurity;

                    if (!certRaw.IsNullOrEmpty())
                    {
                        var certBase64 = certRaw.Replace("certRaw=", "");
                        const string beginMarker = "-----BEGIN CERTIFICATE-----\n";
                        const string endMarker = "\n-----END CERTIFICATE-----";
                        var certPem = beginMarker + certBase64 + endMarker;
                        item.Cert = certPem;
                    }
                }
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
