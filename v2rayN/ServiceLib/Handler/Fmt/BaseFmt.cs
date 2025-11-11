using System.Collections.Specialized;

namespace ServiceLib.Handler.Fmt;

public class BaseFmt
{
    private static readonly string[] _allowInsecureArray = new[] { "insecure", "allowInsecure", "allow_insecure", "verify" };

    protected static string GetIpv6(string address)
    {
        if (Utils.IsIpv6(address))
        {
            // Check if the address is already surrounded by square brackets, if not, add square brackets
            return address.StartsWith('[') && address.EndsWith(']') ? address : $"[{address}]";
        }
        else
        {
            return address;
        }
    }

    protected static int ToUriQuery(ProfileItem item, string? securityDef, ref Dictionary<string, string> dicQuery)
    {
        if (item.Flow.IsNotEmpty())
        {
            dicQuery.Add("flow", item.Flow);
        }

        if (item.StreamSecurity.IsNotEmpty())
        {
            dicQuery.Add("security", item.StreamSecurity);
        }
        else
        {
            if (securityDef != null)
            {
                dicQuery.Add("security", securityDef);
            }
        }
        if (item.Sni.IsNotEmpty())
        {
            dicQuery.Add("sni", Utils.UrlEncode(item.Sni));
        }
        if (item.Fingerprint.IsNotEmpty())
        {
            dicQuery.Add("fp", Utils.UrlEncode(item.Fingerprint));
        }
        if (item.PublicKey.IsNotEmpty())
        {
            dicQuery.Add("pbk", Utils.UrlEncode(item.PublicKey));
        }
        if (item.ShortId.IsNotEmpty())
        {
            dicQuery.Add("sid", Utils.UrlEncode(item.ShortId));
        }
        if (item.SpiderX.IsNotEmpty())
        {
            dicQuery.Add("spx", Utils.UrlEncode(item.SpiderX));
        }
        if (item.Mldsa65Verify.IsNotEmpty())
        {
            dicQuery.Add("pqv", Utils.UrlEncode(item.Mldsa65Verify));
        }

        if (item.StreamSecurity.Equals(Global.StreamSecurity))
        {
            if (item.Alpn.IsNotEmpty())
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.Alpn));
            }
            ToUriQueryAllowInsecure(item, ref dicQuery);
        }

        dicQuery.Add("type", item.Network.IsNotEmpty() ? item.Network : nameof(ETransport.tcp));

        switch (item.Network)
        {
            case nameof(ETransport.tcp):
                dicQuery.Add("headerType", item.HeaderType.IsNotEmpty() ? item.HeaderType : Global.None);
                if (item.RequestHost.IsNotEmpty())
                {
                    dicQuery.Add("host", Utils.UrlEncode(item.RequestHost));
                }
                break;

            case nameof(ETransport.kcp):
                dicQuery.Add("headerType", item.HeaderType.IsNotEmpty() ? item.HeaderType : Global.None);
                if (item.Path.IsNotEmpty())
                {
                    dicQuery.Add("seed", Utils.UrlEncode(item.Path));
                }
                break;

            case nameof(ETransport.ws):
            case nameof(ETransport.httpupgrade):
                if (item.RequestHost.IsNotEmpty())
                {
                    dicQuery.Add("host", Utils.UrlEncode(item.RequestHost));
                }
                if (item.Path.IsNotEmpty())
                {
                    dicQuery.Add("path", Utils.UrlEncode(item.Path));
                }
                break;

            case nameof(ETransport.xhttp):
                if (item.RequestHost.IsNotEmpty())
                {
                    dicQuery.Add("host", Utils.UrlEncode(item.RequestHost));
                }
                if (item.Path.IsNotEmpty())
                {
                    dicQuery.Add("path", Utils.UrlEncode(item.Path));
                }
                if (item.HeaderType.IsNotEmpty() && Global.XhttpMode.Contains(item.HeaderType))
                {
                    dicQuery.Add("mode", Utils.UrlEncode(item.HeaderType));
                }
                if (item.Extra.IsNotEmpty())
                {
                    dicQuery.Add("extra", Utils.UrlEncode(item.Extra));
                }
                break;

            case nameof(ETransport.http):
            case nameof(ETransport.h2):
                dicQuery["type"] = nameof(ETransport.http);
                if (item.RequestHost.IsNotEmpty())
                {
                    dicQuery.Add("host", Utils.UrlEncode(item.RequestHost));
                }
                if (item.Path.IsNotEmpty())
                {
                    dicQuery.Add("path", Utils.UrlEncode(item.Path));
                }
                break;

            case nameof(ETransport.quic):
                dicQuery.Add("headerType", item.HeaderType.IsNotEmpty() ? item.HeaderType : Global.None);
                dicQuery.Add("quicSecurity", Utils.UrlEncode(item.RequestHost));
                dicQuery.Add("key", Utils.UrlEncode(item.Path));
                break;

            case nameof(ETransport.grpc):
                if (item.Path.IsNotEmpty())
                {
                    dicQuery.Add("authority", Utils.UrlEncode(item.RequestHost));
                    dicQuery.Add("serviceName", Utils.UrlEncode(item.Path));
                    if (item.HeaderType is Global.GrpcGunMode or Global.GrpcMultiMode)
                    {
                        dicQuery.Add("mode", Utils.UrlEncode(item.HeaderType));
                    }
                }
                break;
        }
        return 0;
    }

    protected static int ToUriQueryLite(ProfileItem item, ref Dictionary<string, string> dicQuery)
    {
        if (item.Sni.IsNotEmpty())
        {
            dicQuery.Add("sni", Utils.UrlEncode(item.Sni));
        }
        if (item.Alpn.IsNotEmpty())
        {
            dicQuery.Add("alpn", Utils.UrlEncode(item.Alpn));
        }

        ToUriQueryAllowInsecure(item, ref dicQuery);

        return 0;
    }

    private static int ToUriQueryAllowInsecure(ProfileItem item, ref Dictionary<string, string> dicQuery)
    {
        if (item.AllowInsecure.Equals(Global.AllowInsecure.First()))
        {
            // Add two for compatibility
            dicQuery.Add("insecure", "1");
            dicQuery.Add("allowInsecure", "1");
        }
        else
        {
            dicQuery.Add("insecure", "0");
            dicQuery.Add("allowInsecure", "0");
        }

        return 0;
    }

    protected static int ResolveUriQuery(NameValueCollection query, ref ProfileItem item)
    {
        item.Flow = GetQueryValue(query, "flow");
        item.StreamSecurity = GetQueryValue(query, "security");
        item.Sni = GetQueryValue(query, "sni");
        item.Alpn = GetQueryDecoded(query, "alpn");
        item.Fingerprint = GetQueryDecoded(query, "fp");
        item.PublicKey = GetQueryDecoded(query, "pbk");
        item.ShortId = GetQueryDecoded(query, "sid");
        item.SpiderX = GetQueryDecoded(query, "spx");
        item.Mldsa65Verify = GetQueryDecoded(query, "pqv");

        if (_allowInsecureArray.Any(k => GetQueryDecoded(query, k) == "1"))
        {
            item.AllowInsecure = Global.AllowInsecure.First();
        }
        else if (_allowInsecureArray.Any(k => GetQueryDecoded(query, k) == "0"))
        {
            item.AllowInsecure = Global.AllowInsecure.Skip(1).First();
        }
        else
        {
            item.AllowInsecure = string.Empty;
        }

        item.Network = GetQueryValue(query, "type", nameof(ETransport.tcp));
        switch (item.Network)
        {
            case nameof(ETransport.tcp):
                item.HeaderType = GetQueryValue(query, "headerType", Global.None);
                item.RequestHost = GetQueryDecoded(query, "host");
                break;

            case nameof(ETransport.kcp):
                item.HeaderType = GetQueryValue(query, "headerType", Global.None);
                item.Path = GetQueryDecoded(query, "seed");
                break;

            case nameof(ETransport.ws):
            case nameof(ETransport.httpupgrade):
                item.RequestHost = GetQueryDecoded(query, "host");
                item.Path = GetQueryDecoded(query, "path", "/");
                break;

            case nameof(ETransport.xhttp):
                item.RequestHost = GetQueryDecoded(query, "host");
                item.Path = GetQueryDecoded(query, "path", "/");
                item.HeaderType = GetQueryDecoded(query, "mode");
                item.Extra = GetQueryDecoded(query, "extra");
                break;

            case nameof(ETransport.http):
            case nameof(ETransport.h2):
                item.Network = nameof(ETransport.h2);
                item.RequestHost = GetQueryDecoded(query, "host");
                item.Path = GetQueryDecoded(query, "path", "/");
                break;

            case nameof(ETransport.quic):
                item.HeaderType = GetQueryValue(query, "headerType", Global.None);
                item.RequestHost = GetQueryValue(query, "quicSecurity", Global.None);
                item.Path = GetQueryDecoded(query, "key");
                break;

            case nameof(ETransport.grpc):
                item.RequestHost = GetQueryDecoded(query, "authority");
                item.Path = GetQueryDecoded(query, "serviceName");
                item.HeaderType = GetQueryDecoded(query, "mode", Global.GrpcGunMode);
                break;

            default:
                break;
        }
        return 0;
    }

    protected static bool Contains(string str, params string[] s)
    {
        return s.All(item => str.Contains(item, StringComparison.OrdinalIgnoreCase));
    }

    protected static string WriteAllText(string strData, string ext = "json")
    {
        var fileName = Utils.GetTempPath($"{Utils.GetGuid(false)}.{ext}");
        File.WriteAllText(fileName, strData);
        return fileName;
    }

    protected static string ToUri(EConfigType eConfigType, string address, object port, string userInfo, Dictionary<string, string>? dicQuery, string? remark)
    {
        var query = dicQuery != null
            ? ("?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray()))
            : string.Empty;

        var url = $"{Utils.UrlEncode(userInfo)}@{GetIpv6(address)}:{port}";
        return $"{Global.ProtocolShares[eConfigType]}{url}{query}{remark}";
    }

    protected static string GetQueryValue(NameValueCollection query, string key, string defaultValue = "")
    {
        return query[key] ?? defaultValue;
    }

    protected static string GetQueryDecoded(NameValueCollection query, string key, string defaultValue = "")
    {
        return Utils.UrlDecode(GetQueryValue(query, key, defaultValue));
    }
}
