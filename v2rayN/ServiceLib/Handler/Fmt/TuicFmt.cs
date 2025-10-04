namespace ServiceLib.Handler.Fmt;

public class TuicFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        ProfileItem item = new()
        {
            ConfigType = EConfigType.TUIC
        };

        var url = Utils.TryUri(str);
        if (url == null)
        {
            return null;
        }

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        var rawUserInfo = Utils.UrlDecode(url.UserInfo);
        var userInfoParts = rawUserInfo.Split(new[] { ':' }, 2);
        if (userInfoParts.Length == 2)
        {
            item.Id = userInfoParts.First();
            item.Security = userInfoParts.Last();
        }

        var query = Utils.ParseQueryString(url.Query);
        ResolveStdTransport(query, ref item);
        item.HeaderType = GetQueryValue(query, "congestion_control");

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
        var dicQuery = new Dictionary<string, string>();
        if (item.Sni.IsNotEmpty())
        {
            dicQuery.Add("sni", item.Sni);
        }
        if (item.Alpn.IsNotEmpty())
        {
            dicQuery.Add("alpn", Utils.UrlEncode(item.Alpn));
        }
        dicQuery.Add("congestion_control", item.HeaderType);

        return ToUri(EConfigType.TUIC, item.Address, item.Port, $"{item.Id}:{item.Security}", dicQuery, remark);
    }
}
