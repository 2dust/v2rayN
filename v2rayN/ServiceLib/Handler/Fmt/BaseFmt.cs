using System.Collections.Specialized;

namespace ServiceLib.Handler.Fmt;

public class BaseFmt
{
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

    protected static int GetStdTransport(ProfileItem item, string? securityDef, ref Dictionary<string, string> dicQuery)
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
            dicQuery.Add("sni", item.Sni);
        }
        if (item.Alpn.IsNotEmpty())
        {
            dicQuery.Add("alpn", Utils.UrlEncode(item.Alpn));
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
        if (item.AllowInsecure.Equals("true"))
        {
            dicQuery.Add("allowInsecure", "1");
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

    protected static int ResolveStdTransport(NameValueCollection query, ref ProfileItem item)
    {
        item.Flow = query["flow"] ?? "";
        item.StreamSecurity = query["security"] ?? "";
        item.Sni = query["sni"] ?? "";
        item.Alpn = Utils.UrlDecode(query["alpn"] ?? "");
        item.Fingerprint = Utils.UrlDecode(query["fp"] ?? "");
        item.PublicKey = Utils.UrlDecode(query["pbk"] ?? "");
        item.ShortId = Utils.UrlDecode(query["sid"] ?? "");
        item.SpiderX = Utils.UrlDecode(query["spx"] ?? "");
        item.AllowInsecure = (query["allowInsecure"] ?? "") == "1" ? "true" : "";

        item.Network = query["type"] ?? nameof(ETransport.tcp);
        switch (item.Network)
        {
            case nameof(ETransport.tcp):
                item.HeaderType = query["headerType"] ?? Global.None;
                item.RequestHost = Utils.UrlDecode(query["host"] ?? "");

                break;

            case nameof(ETransport.kcp):
                item.HeaderType = query["headerType"] ?? Global.None;
                item.Path = Utils.UrlDecode(query["seed"] ?? "");
                break;

            case nameof(ETransport.ws):
            case nameof(ETransport.httpupgrade):
                item.RequestHost = Utils.UrlDecode(query["host"] ?? "");
                item.Path = Utils.UrlDecode(query["path"] ?? "/");
                break;

            case nameof(ETransport.xhttp):
                item.RequestHost = Utils.UrlDecode(query["host"] ?? "");
                item.Path = Utils.UrlDecode(query["path"] ?? "/");
                item.HeaderType = Utils.UrlDecode(query["mode"] ?? "");
                item.Extra = Utils.UrlDecode(query["extra"] ?? "");
                break;

            case nameof(ETransport.http):
            case nameof(ETransport.h2):
                item.Network = nameof(ETransport.h2);
                item.RequestHost = Utils.UrlDecode(query["host"] ?? "");
                item.Path = Utils.UrlDecode(query["path"] ?? "/");
                break;

            case nameof(ETransport.quic):
                item.HeaderType = query["headerType"] ?? Global.None;
                item.RequestHost = query["quicSecurity"] ?? Global.None;
                item.Path = Utils.UrlDecode(query["key"] ?? "");
                break;

            case nameof(ETransport.grpc):
                item.RequestHost = Utils.UrlDecode(query["authority"] ?? "");
                item.Path = Utils.UrlDecode(query["serviceName"] ?? "");
                item.HeaderType = Utils.UrlDecode(query["mode"] ?? Global.GrpcGunMode);
                break;

            default:
                break;
        }
        return 0;
    }

    protected static bool Contains(string str, params string[] s)
    {
        foreach (var item in s)
        {
            if (str.Contains(item, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
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
}
