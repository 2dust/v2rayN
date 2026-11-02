namespace ServiceLib.Handler.Fmt;

public class NaiveFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        var parsedUrl = Utils.TryUri(str);
        if (parsedUrl == null)
        {
            return null;
        }

        ProfileItem item = new()
        {
            ConfigType = EConfigType.Naive,
            Remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
            Address = parsedUrl.IdnHost,
            // naive+https:// and naive+quic:// links leave the port out when it is 443
            // (https://gist.github.com/DuckSoft/ca03913b0a26fc77a1da4d01cc6ab2f1). Uri.Port answers
            // -1 for an unregistered scheme carrying no port; an explicit ":0" stays 0.
            Port = parsedUrl.Port == -1 ? 443 : parsedUrl.Port,
        };
        var protocolExtra = item.GetProtocolExtra();
        if (parsedUrl.Scheme.Contains("quic"))
        {
            protocolExtra = protocolExtra with
            {
                NaiveQuic = true,
            };
        }
        var rawUserInfo = Utils.UrlDecode(parsedUrl.UserInfo);
        if (rawUserInfo.Contains(':'))
        {
            var split = rawUserInfo.Split(':', 2);
            item.Username = split[0];
            item.Password = split[1];
        }
        else
        {
            item.Password = rawUserInfo;
        }

        var query = Utils.ParseQueryString(parsedUrl.Query);
        ResolveUriQuery(query, ref item);
        var insecureConcurrency = int.TryParse(GetQueryValue(query, "insecure-concurrency"), out var ic) ? ic : 0;
        if (insecureConcurrency > 0)
        {
            protocolExtra = protocolExtra with
            {
                InsecureConcurrency = insecureConcurrency,
            };
        }

        item.SetProtocolExtra(protocolExtra);
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
        var userInfo = item.Username.IsNotEmpty() ? $"{Utils.UrlEncode(item.Username)}:{Utils.UrlEncode(item.Password)}" : Utils.UrlEncode(item.Password);
        var dicQuery = new Dictionary<string, string>();
        ToUriQuery(item, Global.None, ref dicQuery);
        var protocolExtra = item.GetProtocolExtra();
        if (protocolExtra.InsecureConcurrency > 0)
        {
            dicQuery.Add("insecure-concurrency", protocolExtra?.InsecureConcurrency.ToString());
        }

        var query = dicQuery.Count > 0
            ? ("?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray()))
            : string.Empty;
        var url = $"{userInfo}@{GetIpv6(item.Address)}:{item.Port}";

        if (protocolExtra.NaiveQuic == true)
        {
            return $"{Global.NaiveQuicProtocolShare}{url}{query}{remark}";
        }
        else
        {
            return $"{Global.NaiveHttpsProtocolShare}{url}{query}{remark}";
        }
    }
}
