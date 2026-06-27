using System.Collections.Specialized;

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
        ResolveHy2UriQuery(query, ref item);

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }
        var protocolExtraItem = item.GetProtocolExtra();
        if (!protocolExtraItem.Hy2RealmUrl.TrimEx().IsNullOrEmpty())
        {
            return ToUriForRealm(item);
        }

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }
        var dicQuery = new Dictionary<string, string>();
        ToUriQueryLite(item, ref dicQuery);
        ToHy2UriQuery(item, ref dicQuery);

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

    public static ProfileItem? ResolveRealm(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        ProfileItem item = new()
        {
            ConfigType = EConfigType.Hysteria2
        };
        var realmStr = str["hysteria2+".Length..];
        var result = HyRealm.TryParse(realmStr, out var realm);
        if (!result || realm == null)
        {
            return null;
        }

        var url = Utils.TryUri(str);
        if (url == null)
        {
            return null;
        }

        item.Address = realm.RendezvousHost;
        item.Port = realm.RendezvousPort;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);

        var query = Utils.ParseQueryString(url.Query);
        ResolveUriQuery(query, ref item);
        var authPassword = GetQueryDecoded(query, "auth");
        if (authPassword.IsNullOrEmpty())
        {
            return null;
        }
        item.Password = authPassword;
        ResolveHy2UriQuery(query, ref item);
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            Hy2RealmUrl = realm.ToUri(),
        });

        msg = string.Empty;
        return item;
    }

    public static string? ToUriForRealm(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }
        var protocolExtraItem = item.GetProtocolExtra();
        var result = HyRealm.TryParse(protocolExtraItem.Hy2RealmUrl, out var realm);
        if (!result || realm == null)
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
        ToHy2UriQuery(item, ref dicQuery);

        dicQuery.Add("auth", Utils.UrlEncode(item.Password));

        var queryBuilder = new StringBuilder();
        queryBuilder.Append('?');
        foreach (var kv in dicQuery)
        {
            queryBuilder.Append(kv.Key);
            queryBuilder.Append('=');
            queryBuilder.Append(kv.Value);
            queryBuilder.Append('&');
        }
        foreach (var stun in realm.StunList)
        {
            queryBuilder.Append("stun=");
            queryBuilder.Append(Utils.UrlEncode(stun));
            queryBuilder.Append('&');
        }
        var query = queryBuilder.ToString().TrimEnd('&');

        var url = $"{Utils.UrlEncode(realm.Token)}@{GetIpv6(realm.RendezvousHost)}:{realm.RendezvousPort}";
        var scheme = realm.IsHttp ? Global.Hysteria2HttpRealmProtocolShare : Global.Hysteria2RealmProtocolShare;
        return $"{scheme}{url}/{realm.RealmName}{query}{remark}";
    }

    private static void ResolveHy2UriQuery(NameValueCollection query, ref ProfileItem item)
    {
        if (item.CertSha.IsNullOrEmpty())
        {
            item.CertSha = GetQueryDecoded(query, "pinSHA256");
        }
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            Ports = GetQueryDecoded(query, "mport"),
            SalamanderPass = GetQueryDecoded(query, "obfs-password"),
            // NOTE: The "PacketSize" parameter is not defined by the official URI Scheme, may remove or rename it in the future.
            GeckoMinPacketSize = GetQueryDecoded(query, "minPacketSize"),
            GeckoMaxPacketSize = GetQueryDecoded(query, "maxPacketSize"),
        });
        if (GetQueryDecoded(query, "obfs") == "gecko")
        {
            // Ensure the "PacketSize" parameters are present for gecko obfs.
            var protocolExtraItem = item.GetProtocolExtra();
            if (protocolExtraItem.GeckoMinPacketSize.IsNullOrEmpty())
            {
                protocolExtraItem = protocolExtraItem with
                {
                    GeckoMinPacketSize = "512",
                };
            }
            if (protocolExtraItem.GeckoMaxPacketSize.IsNullOrEmpty())
            {
                protocolExtraItem = protocolExtraItem with
                {
                    GeckoMaxPacketSize = "1200",
                };
            }
            item.SetProtocolExtra(protocolExtraItem);
        }
    }

    private static void ToHy2UriQuery(ProfileItem item, ref Dictionary<string, string> dicQuery)
    {
        if (!item.CertSha.IsNullOrEmpty()
            && !item.CertSha.Contains(','))
        {
            var sha = item.CertSha;
            dicQuery.Add("pinSHA256", Utils.UrlEncode(sha));
        }
        var protocolExtraItem = item.GetProtocolExtra();
        var isGecko = !protocolExtraItem.GeckoMinPacketSize.IsNullOrEmpty() || !protocolExtraItem.GeckoMaxPacketSize.IsNullOrEmpty();
        if (!protocolExtraItem.SalamanderPass.IsNullOrEmpty())
        {
            dicQuery.Add("obfs", isGecko ? "gecko" : "salamander");
            dicQuery.Add("obfs-password", Utils.UrlEncode(protocolExtraItem.SalamanderPass));
            if (isGecko)
            {
                // NOTE: The "PacketSize" parameter is not defined by the official URI Scheme, may remove or rename it in the future.
                dicQuery.Add("minPacketSize", protocolExtraItem.GeckoMinPacketSize);
                dicQuery.Add("maxPacketSize", protocolExtraItem.GeckoMaxPacketSize);
            }
        }
        if (!protocolExtraItem.Ports.IsNullOrEmpty())
        {
            dicQuery.Add("mport", Utils.UrlEncode(protocolExtraItem.Ports.Replace(':', '-')));
        }
    }
}
