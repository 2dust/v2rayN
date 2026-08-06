namespace ServiceLib.Handler.Fmt;

public class TrojanFmt : BaseFmt
{
    private static readonly List<string> _insecureQueryKeys = new() { "allowInsecure", "insecure" };

    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        ProfileItem item = new()
        {
            ConfigType = EConfigType.Trojan
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
        if (_insecureQueryKeys.Any(q => GetQueryValue(query, q) == "1"))
        {
            item.AllowInsecure = Global.StringTrue;
        }
        item.SetProtocolExtra(item.GetProtocolExtra() with { Flow = GetQueryValue(query, "flow") });
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
        if (item.GetAllowInsecure())
        {
            _insecureQueryKeys.ForEach(q => dicQuery.Add(q, "1"));
        }
        if (!item.GetProtocolExtra().Flow.IsNullOrEmpty())
        {
            dicQuery.Add("flow", item.GetProtocolExtra().Flow);
        }
        ToUriQuery(item, null, ref dicQuery);

        return ToUri(EConfigType.Trojan, item.Address, item.Port, item.Password, dicQuery, remark);
    }
}
