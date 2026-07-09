namespace ServiceLib.Models.Entities;

public record ProtocolExtraItem
{
    public bool? Uot { get; init; }
    public string? CongestionControl { get; init; }

    // http outbound
    public string? HttpHeaders { get; init; }

    public static bool TryParseHttpHeaders(string? httpHeaders, out JsonObject? headers)
    {
        headers = null;
        if (httpHeaders.IsNullOrEmpty())
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(httpHeaders, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var headerNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in document.RootElement.EnumerateObject())
            {
                if (!headerNames.Add(item.Name)
                    || !IsHttpHeaderName(item.Name)
                    || !IsHttpHeaderValue(item.Value))
                {
                    return false;
                }
            }
        }
        catch
        {
            return false;
        }

        if (JsonUtils.ParseJson(httpHeaders) is not JsonObject headersObject)
        {
            return false;
        }

        headers = headersObject.Count > 0 ? headersObject : null;
        return true;
    }

    private static bool IsHttpHeaderName(string name)
    {
        return name.Length > 0 && name.All(IsHttpHeaderNameChar);
    }

    private static bool IsHttpHeaderNameChar(char c)
    {
        return char.IsAsciiLetterOrDigit(c)
               || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`'
                   or '|' or '~';
    }

    private static bool IsHttpHeaderValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return IsHttpHeaderValueString(element.GetString());
        }

        return element.ValueKind == JsonValueKind.Array
               && element.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String
                                                      && IsHttpHeaderValueString(item.GetString()));
    }

    private static bool IsHttpHeaderValueString(string? value)
    {
        if (value is null)
        {
            return false;
        }

        return value.Length == 0
               || IsHttpFieldVChar(value[0])
               && IsHttpFieldVChar(value[^1])
               && value.All(c => IsHttpFieldVChar(c) || c is ' ' or '\t');
    }

    private static bool IsHttpFieldVChar(char c)
    {
        return c is >= '\u0021' and <= '\u007E' or >= '\u0080';
    }

    // vmess
    public string? AlterId { get; init; }
    public string? VmessSecurity { get; init; }

    // vless
    public string? Flow { get; init; }
    public string? VlessEncryption { get; init; }
    //public string? VisionSeed { get; init; }

    // shadowsocks
    //public string? PluginArgs { get; init; }
    public string? SsMethod { get; init; }

    // wireguard
    public string? WgPublicKey { get; init; }
    public string? WgPresharedKey { get; init; }
    public string? WgInterfaceAddress { get; init; }
    public string? WgReserved { get; init; }
    public int? WgMtu { get; init; }

    // hysteria2
    public string? SalamanderPass { get; init; }
    public int? UpMbps { get; init; }
    public int? DownMbps { get; init; }
    public string? Ports { get; init; }
    public string? HopInterval { get; init; }
    // realm://<token>@<rendezvous-host>[:port]/<realm-name>?stun=<stun-host>[:port]&stun=<stun-host>[:port]...
    // example:
    // realm://public@realm.hy2.io/57f9be7c-2810-4f5b-8cb9-260bc84d6c90?stun=example.stun:3478&stun=example2.stun:3478
    public string? Hy2RealmUrl { get; init; }
    public string? GeckoMinPacketSize { get; init; }
    public string? GeckoMaxPacketSize { get; init; }

    // naiveproxy
    public int? InsecureConcurrency { get; init; }
    public bool? NaiveQuic { get; init; }

    // group profile
    public string? GroupType { get; init; }
    public string? ChildItems { get; init; }
    public string? SubChildItems { get; init; }
    public string? Filter { get; init; }
    public EMultipleLoad? MultipleLoad { get; init; }
}
