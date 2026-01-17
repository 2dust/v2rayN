namespace ServiceLib.Handler.Fmt;

public class VLESSFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        ProfileItem item = new()
        {
            ConfigType = EConfigType.VLESS,
            Security = AppConfig.None
        };

        var url = Utils.TryUri(str);
        if (url is null)
        {
            return null;
        }

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        item.Id = Utils.UrlDecode(url.UserInfo);

        var query = Utils.ParseQueryString(url.Query);
        item.Security = GetQueryValue(query, "encryption", AppConfig.None);
        item.StreamSecurity = GetQueryValue(query, "security");
        ResolveUriQuery(query, ref item);

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item is null)
        {
            return null;
        }

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }
        var dicQuery = new Dictionary<string, string>();
        if (item.Security.IsNotEmpty())
        {
            dicQuery.Add("encryption", item.Security);
        }
        else
        {
            dicQuery.Add("encryption", AppConfig.None);
        }
        ToUriQuery(item, AppConfig.None, ref dicQuery);

        return ToUri(EConfigType.VLESS, item.Address, item.Port, item.Id, dicQuery, remark);
    }
}
