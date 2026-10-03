namespace ServiceLib.Handler.Fmt;

public class VLESSFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        ProfileItem item = new()
        {
            ConfigType = EConfigType.VLESS,
        };

        var url = Utils.TryUri(str);
        if (url == null)
        {
            return null;
        }

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        item.Password = Utils.UrlDecode(url.UserInfo);

        var query = Utils.ParseQueryString(url.Query);
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            VlessEncryption = GetQueryValue(query, "encryption", Global.None),
            Flow = GetQueryValue(query, "flow")
        });
        item.StreamSecurity = GetQueryValue(query, "security");
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
        var dicQuery = new Dictionary<string, string>();
        dicQuery.Add("encryption",
            !item.GetProtocolExtra().VlessEncryption.IsNullOrEmpty() ? item.GetProtocolExtra().VlessEncryption : Global.None);
        if (!item.GetProtocolExtra().Flow.IsNullOrEmpty())
        {
            dicQuery.Add("flow", item.GetProtocolExtra().Flow);
        }
        ToUriQuery(item, Global.None, ref dicQuery);

        return ToUri(EConfigType.VLESS, item.Address, item.Port, item.Password, dicQuery, remark);
    }
}
