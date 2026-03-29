using System.Collections.Specialized;

namespace ServiceLib.Handler.Fmt;

public class BaseFmt
{
    private static readonly string[] _allowInsecureArray = new[] { "insecure", "allowInsecure", "allow_insecure" };
    private static string UrlEncodeSafe(string? value) => Utils.UrlEncode(value ?? string.Empty);

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
        var transport = item.GetTransportExtra();

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
        if (item.EchConfigList.IsNotEmpty())
        {
            dicQuery.Add("ech", Utils.UrlEncode(item.EchConfigList));
        }
        if (item.CertSha.IsNotEmpty())
        {
            dicQuery.Add("pcs", Utils.UrlEncode(item.CertSha));
        }
        if (item.Finalmask.IsNotEmpty())
        {
            var node = JsonUtils.ParseJson(item.Finalmask);
            var finalmask = node != null
                ? JsonUtils.Serialize(node, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
                : item.Finalmask;
            dicQuery.Add("fm", Utils.UrlEncode(finalmask));
        }

        var network = item.GetNetwork();
        if (!Global.Networks.Contains(network))
        {
            network = nameof(ETransport.raw);
        }

        //dicQuery.Add("type", network);
        dicQuery.Add("type", network == nameof(ETransport.raw) ? Global.RawNetworkAlias : network);

        switch (network)
        {
            case nameof(ETransport.raw):
                dicQuery.Add("headerType", transport.RawHeaderType.IsNotEmpty() ? transport.RawHeaderType : Global.None);
                if (transport.Host.IsNotEmpty())
                {
                    dicQuery.Add("host", UrlEncodeSafe(transport.Host));
                }
                if (transport.Path.IsNotEmpty())
                {
                    dicQuery.Add("path", UrlEncodeSafe(transport.Path));
                }
                break;

            case nameof(ETransport.kcp):
                dicQuery.Add("headerType", transport.KcpHeaderType.IsNotEmpty() ? transport.KcpHeaderType : Global.None);
                if (transport.KcpSeed.IsNotEmpty())
                {
                    dicQuery.Add("seed", UrlEncodeSafe(transport.KcpSeed));
                }
                break;

            case nameof(ETransport.ws):
            case nameof(ETransport.httpupgrade):
                if (transport.Host.IsNotEmpty())
                {
                    dicQuery.Add("host", UrlEncodeSafe(transport.Host));
                }
                if (transport.Path.IsNotEmpty())
                {
                    dicQuery.Add("path", UrlEncodeSafe(transport.Path));
                }
                break;

            case nameof(ETransport.xhttp):
                if (transport.Host.IsNotEmpty())
                {
                    dicQuery.Add("host", UrlEncodeSafe(transport.Host));
                }
                if (transport.Path.IsNotEmpty())
                {
                    dicQuery.Add("path", UrlEncodeSafe(transport.Path));
                }
                if (transport.XhttpMode.IsNotEmpty() && Global.XhttpMode.Contains(transport.XhttpMode))
                {
                    dicQuery.Add("mode", UrlEncodeSafe(transport.XhttpMode));
                }
                if (transport.XhttpExtra.IsNotEmpty())
                {
                    var node = JsonUtils.ParseJson(transport.XhttpExtra);
                    var extra = node != null
                        ? JsonUtils.Serialize(node, new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        })
                        : transport.XhttpExtra;
                    dicQuery.Add("extra", UrlEncodeSafe(extra));
                }
                break;

            case nameof(ETransport.grpc):
                if (transport.GrpcServiceName.IsNotEmpty())
                {
                    dicQuery.Add("authority", UrlEncodeSafe(transport.GrpcAuthority));
                    dicQuery.Add("serviceName", UrlEncodeSafe(transport.GrpcServiceName));
                    if (transport.GrpcMode is Global.GrpcGunMode or Global.GrpcMultiMode)
                    {
                        dicQuery.Add("mode", UrlEncodeSafe(transport.GrpcMode));
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
        var transport = item.GetTransportExtra();

        item.StreamSecurity = GetQueryValue(query, "security");
        item.Sni = GetQueryValue(query, "sni");
        item.Alpn = GetQueryDecoded(query, "alpn");
        item.Fingerprint = GetQueryDecoded(query, "fp");
        item.PublicKey = GetQueryDecoded(query, "pbk");
        item.ShortId = GetQueryDecoded(query, "sid");
        item.SpiderX = GetQueryDecoded(query, "spx");
        item.Mldsa65Verify = GetQueryDecoded(query, "pqv");
        item.EchConfigList = GetQueryDecoded(query, "ech");
        item.CertSha = GetQueryDecoded(query, "pcs");

        var finalmaskDecoded = GetQueryDecoded(query, "fm");
        if (finalmaskDecoded.IsNotEmpty())
        {
            var node = JsonUtils.ParseJson(finalmaskDecoded);
            item.Finalmask = node != null
                ? JsonUtils.Serialize(node, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
                : finalmaskDecoded;
        }
        else
        {
            item.Finalmask = string.Empty;
        }

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

        var net = GetQueryValue(query, "type", nameof(ETransport.raw));
        if (net == Global.RawNetworkAlias)
        {
            net = nameof(ETransport.raw);
        }
        if (!Global.Networks.Contains(net))
        {
            net = nameof(ETransport.raw);
        }

        item.Network = net;
        switch (item.Network)
        {
            case nameof(ETransport.raw):
                transport = transport with
                {
                    RawHeaderType = GetQueryValue(query, "headerType", Global.None),
                    Host = GetQueryDecoded(query, "host"),
                    Path = GetQueryDecoded(query, "path"),
                };
                break;

            case nameof(ETransport.kcp):
                var kcpSeed = GetQueryDecoded(query, "seed");
                transport = transport with
                {
                    KcpHeaderType = GetQueryValue(query, "headerType", Global.None),
                    KcpSeed = kcpSeed,
                };
                break;

            case nameof(ETransport.ws):
            case nameof(ETransport.httpupgrade):
                transport = transport with
                {
                    Host = GetQueryDecoded(query, "host"),
                    Path = GetQueryDecoded(query, "path", "/"),
                };
                break;

            case nameof(ETransport.xhttp):
                var xhttpExtra = GetQueryDecoded(query, "extra");
                if (xhttpExtra.IsNotEmpty())
                {
                    var node = JsonUtils.ParseJson(xhttpExtra);
                    if (node != null)
                    {
                        xhttpExtra = JsonUtils.Serialize(node, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });
                    }
                }

                transport = transport with
                {
                    Host = GetQueryDecoded(query, "host"),
                    Path = GetQueryDecoded(query, "path", "/"),
                    XhttpMode = GetQueryDecoded(query, "mode"),
                    XhttpExtra = xhttpExtra,
                };
                break;

            case nameof(ETransport.grpc):
                transport = transport with
                {
                    GrpcAuthority = GetQueryDecoded(query, "authority"),
                    GrpcServiceName = GetQueryDecoded(query, "serviceName"),
                    GrpcMode = GetQueryDecoded(query, "mode", Global.GrpcGunMode),
                };
                break;

            default:
                item.Network = nameof(ETransport.raw);
                break;
        }

        item.SetTransportExtra(transport);

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
