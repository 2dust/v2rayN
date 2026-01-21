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
        var protocolExtra = item.GetProtocolExtra();

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
        protocolExtra.VlessEncryption = GetQueryValue(query, "encryption", Global.None);
        item.StreamSecurity = GetQueryValue(query, "security");
        ResolveUriQuery(query, ref item);

        item.SetProtocolExtra(protocolExtra);
        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var protocolExtra = item.GetProtocolExtra();

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }
        var dicQuery = new Dictionary<string, string>();
        dicQuery.Add("encryption",
            !protocolExtra.VlessEncryption.IsNullOrEmpty() ? protocolExtra.VlessEncryption : Global.None);
        ToUriQuery(item, Global.None, ref dicQuery);

        return ToUri(EConfigType.VLESS, item.Address, item.Port, item.Password, dicQuery, remark);
    }
}
