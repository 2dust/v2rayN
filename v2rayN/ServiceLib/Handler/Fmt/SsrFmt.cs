namespace ServiceLib.Handler.Fmt;

/// <summary>
/// ShadowsocksR share link (ssr://base64url(host:port:protocol:method:obfs:base64url(password)/?params))
/// </summary>
public class SsrFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        var prefix = Global.ProtocolShares[EConfigType.ShadowsocksR];
        if (!str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = Utils.Base64Decode(str[prefix.Length..]);
        if (raw.IsNullOrEmpty())
        {
            return null;
        }

        var queryIndex = raw.IndexOf("/?", StringComparison.Ordinal);
        var mainPart = queryIndex >= 0 ? raw[..queryIndex] : raw;
        var queryPart = queryIndex >= 0 ? raw[queryIndex..] : string.Empty;

        var parts = mainPart.Split(':');
        if (parts.Length < 6)
        {
            return null;
        }

        var passwordB64 = parts[^1];
        var obfs = parts[^2];
        var method = parts[^3];
        var protocol = parts[^4];
        var portStr = parts[^5];
        var address = string.Join(':', parts.Take(parts.Length - 5)).Trim('[', ']');

        var query = Utils.ParseQueryString(queryPart);

        var item = new ProfileItem
        {
            ConfigType = EConfigType.ShadowsocksR,
            Address = address,
            Port = portStr.ToInt(),
            Password = Utils.Base64Decode(passwordB64),
            Remarks = Utils.Base64Decode(query["remarks"]),
        };
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            SsMethod = method,
            SsrProtocol = protocol,
            SsrObfs = obfs,
            SsrProtocolParam = Utils.Base64Decode(query["protoparam"]),
            SsrObfsParam = Utils.Base64Decode(query["obfsparam"]),
        });

        if (item.Address.IsNullOrEmpty()
            || item.Port is <= 0 or >= 65536
            || item.Password.IsNullOrEmpty())
        {
            return null;
        }

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var extra = item.GetProtocolExtra();
        var main = $"{item.Address}:{item.Port}:{extra.SsrProtocol}:{extra.SsMethod}:{extra.SsrObfs}:{EncodeBase64Url(item.Password)}";

        var dicQuery = new Dictionary<string, string>
        {
            ["obfsparam"] = EncodeBase64Url(extra.SsrObfsParam),
            ["protoparam"] = EncodeBase64Url(extra.SsrProtocolParam),
            ["remarks"] = EncodeBase64Url(item.Remarks),
            ["group"] = EncodeBase64Url(string.Empty),
        };
        var query = string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value));

        return $"{Global.ProtocolShares[EConfigType.ShadowsocksR]}{EncodeBase64Url($"{main}/?{query}")}";
    }

    private static string EncodeBase64Url(string? text)
    {
        if (text.IsNullOrEmpty())
        {
            return string.Empty;
        }

        return Utils.Base64Encode(text, true).Replace('+', '-').Replace('/', '_');
    }
}
