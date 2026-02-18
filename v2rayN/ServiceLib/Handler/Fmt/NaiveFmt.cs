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
            Port = parsedUrl.Port,
        };
        if (parsedUrl.Scheme.Contains("quic"))
        {
            item.Network = "quic";
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
            item.SetProtocolExtra(item.GetProtocolExtra() with
            {
                InsecureConcurrency = insecureConcurrency,
            });
        }

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
        if (item.GetProtocolExtra().InsecureConcurrency > 0)
        {
            dicQuery.Add("insecure-concurrency", item.GetProtocolExtra()?.InsecureConcurrency.ToString());
        }

        var query = dicQuery.Count > 0
            ? ("?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray()))
            : string.Empty;
        var url = $"{userInfo}@{GetIpv6(item.Address)}:{item.Port}";

        if (item.Network == "quic")
        {
            return $"{Global.NaiveQuicProtocolShare}{url}{query}{remark}";
        }
        else
        {
            return $"{Global.NaiveHttpsProtocolShare}{url}{query}{remark}";
        }
    }
}
