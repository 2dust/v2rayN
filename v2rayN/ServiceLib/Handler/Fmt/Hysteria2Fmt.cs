namespace ServiceLib.Handler.Fmt;

public class Hysteria2Fmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        ProfileItem item = new()
        {
            ConfigType = EConfigType.Hysteria2
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
        ResolveUriQuery(query, ref item);
        if (item.CertSha.IsNullOrEmpty())
        {
            item.CertSha = GetQueryDecoded(query, "pinSHA256");
        }
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            Ports = GetQueryDecoded(query, "mport"),
            SalamanderPass = GetQueryDecoded(query, "obfs-password"),
        });

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var url = string.Empty;

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }
        var dicQuery = new Dictionary<string, string>();
        ToUriQueryLite(item, ref dicQuery);
        var protocolExtraItem = item.GetProtocolExtra();

        if (!protocolExtraItem.SalamanderPass.IsNullOrEmpty())
        {
            dicQuery.Add("obfs", "salamander");
            dicQuery.Add("obfs-password", Utils.UrlEncode(protocolExtraItem.SalamanderPass));
        }
        if (!protocolExtraItem.Ports.IsNullOrEmpty())
        {
            dicQuery.Add("mport", Utils.UrlEncode(protocolExtraItem.Ports.Replace(':', '-')));
        }
        if (!item.CertSha.IsNullOrEmpty())
        {
            var sha = item.CertSha;
            var idx = sha.IndexOf(',');
            if (idx > 0)
            {
                sha = sha[..idx];
            }
            dicQuery.Add("pinSHA256", Utils.UrlEncode(sha));
        }

        return ToUri(EConfigType.Hysteria2, item.Address, item.Port, item.Password, dicQuery, remark);
    }

    public static ProfileItem? ResolveFull2(string strData, string? subRemarks)
    {
        if (Contains(strData, "server", "auth", "up", "down", "listen"))
        {
            var fileName = WriteAllText(strData);

            var profileItem = new ProfileItem
            {
                CoreType = ECoreType.hysteria2,
                Address = fileName,
                Remarks = subRemarks ?? "hysteria2_custom"
            };
            return profileItem;
        }

        return null;
    }
}
