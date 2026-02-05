namespace ServiceLib.Handler.Fmt;

public class AnytlsFmt : BaseFmt
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
            ConfigType = EConfigType.Anytls,
            Remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
            Address = parsedUrl.IdnHost,
            Port = parsedUrl.Port,
        };
        var rawUserInfo = Utils.UrlDecode(parsedUrl.UserInfo);
        item.Password = rawUserInfo;

        var query = Utils.ParseQueryString(parsedUrl.Query);
        ResolveUriQuery(query, ref item);

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
        var pw = item.Password;
        var dicQuery = new Dictionary<string, string>();
        ToUriQuery(item, Global.None, ref dicQuery);

        return ToUri(EConfigType.Anytls, item.Address, item.Port, pw, dicQuery, remark);
    }
}
