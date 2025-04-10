using static QRCoder.PayloadGenerator;

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
        item.Id = rawUserInfo;

        var query = Utils.ParseQueryString(parsedUrl.Query);
        item.Sni = query["sni"] ?? Global.None;
        item.AllowInsecure = (query["insecure"] ?? "") == "1" ? "true" : "false";

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
        var pw = item.Id;
        var dicQuery = new Dictionary<string, string>();
        if (item.Sni.IsNotEmpty())
        {
            dicQuery.Add("sni", item.Sni);
        }
        dicQuery.Add("insecure", item.AllowInsecure.ToLower() == "true" ? "1" : "0");

        return ToUri(EConfigType.Anytls, item.Address, item.Port, pw, dicQuery, remark);
    }
}
