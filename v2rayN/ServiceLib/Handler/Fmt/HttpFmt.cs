namespace ServiceLib.Handler.Fmt;

public class HttpFmt : BaseFmt
{
    private static string NormalizeColon(string value)
    {
        return value.Replace('：', ':');
    }

    private static string EncodeUserInfoPart(string value)
    {
        return Uri.EscapeDataString(value);
    }

    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        var item = ResolveHttpNew(str) ?? ResolveHttp(str);
        if (item == null)
        {
            return null;
        }
        if (item.Address.Length == 0 || item.Port == 0)
        {
            return null;
        }

        item.ConfigType = EConfigType.HTTP;

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

        var userInfo = string.Empty;
        if (item.Security.IsNotEmpty() && item.Id.IsNotEmpty())
        {
            var security = EncodeUserInfoPart(NormalizeColon(item.Security));
            var id = EncodeUserInfoPart(NormalizeColon(item.Id));
            userInfo = $"{security}:{id}";
        }

        var protocol = item.StreamSecurity == "tls" ? "https://" : "http://";
        if (userInfo.IsNotEmpty())
        {
            return $"{protocol}{userInfo}@{GetIpv6(NormalizeColon(item.Address))}:{item.Port}{remark}";
        }
        else
        {
            return $"{protocol}{GetIpv6(NormalizeColon(item.Address))}:{item.Port}{remark}";
        }
    }

    private static ProfileItem? ResolveHttp(string result)
    {
        ProfileItem item = new()
        {
            ConfigType = EConfigType.HTTP
        };

        // Determine if HTTPS
        var isHttps = result.StartsWith("https://");
        var protocolPrefix = isHttps ? "https://" : "http://";
        
        result = result[protocolPrefix.Length..];
        
        if (isHttps)
        {
            item.StreamSecurity = "tls";
            item.AllowInsecure = Global.AllowInsecure.First(); // Skip certificate verification for HTTPS
        }

        //remark
        var indexRemark = result.IndexOf('#');
        if (indexRemark > 0)
        {
            try
            {
                item.Remarks = Utils.UrlDecode(result.Substring(indexRemark + 1));
            }
            catch { }
            result = result[..indexRemark];
        }

        //parse user info
        var indexS = result.IndexOf('@');
        if (indexS > 0)
        {
            var userInfo = result[..indexS];
            result = result[(indexS + 1)..];
            
            var arr = userInfo.Split(':');
            if (arr.Length == 2)
            {
                item.Security = arr[0];
                item.Id = arr[1];
            }
        }

        // Parse address and port
        var indexPort = result.LastIndexOf(":");
        if (indexPort < 0)
        {
            return null;
        }

        item.Address = result[..indexPort];
        // Remove square brackets from IPv6 addresses
        if (item.Address.StartsWith('[') && item.Address.EndsWith(']'))
        {
            item.Address = item.Address[1..^1];
        }
        
        item.Port = result[(indexPort + 1)..].ToInt();

        return item;
    }

    private static ProfileItem? ResolveHttpNew(string result)
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

        // Determine if HTTPS
        if (parsedUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            item.StreamSecurity = "tls";
            item.AllowInsecure = Global.AllowInsecure.First(); // Skip certificate verification for HTTPS
        }

        // Parse UserInfo
        if (parsedUrl.UserInfo.IsNotEmpty())
        {
            var userInfo = Utils.UrlDecode(parsedUrl.UserInfo);
            var userInfoParts = userInfo.Split([':'], 2);
            if (userInfoParts.Length == 2)
            {
                item.Security = userInfoParts[0];
                item.Id = userInfoParts[1];
            }
        }

        return item;
    }
}
