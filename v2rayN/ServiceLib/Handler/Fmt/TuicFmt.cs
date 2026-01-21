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
        var protocolExtra = item.GetProtocolExtra();

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
            item.Password = userInfoParts.First();
            protocolExtra.Username = userInfoParts.Last();
        }

        var query = Utils.ParseQueryString(url.Query);
        ResolveUriQuery(query, ref item);
        item.HeaderType = GetQueryValue(query, "congestion_control");

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

        var dicQuery = new Dictionary<string, string>();
        ToUriQueryLite(item, ref dicQuery);

        dicQuery.Add("congestion_control", item.HeaderType);

        return ToUri(EConfigType.TUIC, item.Address, item.Port, $"{item.Password}:{item.GetProtocolExtra().Username ?? ""}", dicQuery, remark);
    }
}
