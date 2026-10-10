namespace ServiceLib.Common;

public class JsonUtils
{
    private static readonly string _tag = "JsonUtils";

    private static readonly JsonSerializerOptions _defaultDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        TypeInfoResolver = AppJsonContext.Default,
    };

    private static readonly JsonSerializerOptions _defaultSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = AppJsonContext.Default,
    };

    private static readonly JsonSerializerOptions _defaultSerializeNoIndentedOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = AppJsonContext.Default,
    };

    private static readonly JsonSerializerOptions _nullValueSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = AppJsonContext.Default,
    };

    private static readonly JsonSerializerOptions _nullValueSerializeNoIndentedOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = AppJsonContext.Default,
    };

    private static readonly JsonDocumentOptions _defaultDocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// DeepCopy
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static T? DeepCopy<T>(T? obj)
    {
        if (obj is null)
        {
            return default;
        }
        return Deserialize<T>(Serialize(obj, false));
    }

    /// <summary>
    /// Deserialize to object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="strJson"></param>
    /// <returns></returns>
    public static T? Deserialize<T>(string? strJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(strJson))
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(strJson, _defaultDeserializeOptions);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// parse
    /// </summary>
    /// <param name="strJson"></param>
    /// <returns></returns>
    public static JsonNode? ParseJson(string? strJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(strJson))
            {
                return null;
            }
            return JsonNode.Parse(strJson, nodeOptions: null, _defaultDocumentOptions);
        }
        catch
        {
            //SaveLog(ex.Message, ex);
            return null;
        }
    }

    /// <summary>
    /// Serialize Object to Json string
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="indented"></param>
    /// <param name="nullValue"></param>
    /// <returns></returns>
    public static string Serialize(object? obj, bool indented = true, bool nullValue = false)
    {
        var result = string.Empty;
        try
        {
            if (obj == null)
            {
                return result;
            }
            var options = (nullValue, indented) switch
            {
                (true, true) => _nullValueSerializeOptions,
                (true, false) => _nullValueSerializeNoIndentedOptions,
                (false, true) => _defaultSerializeOptions,
                _ => _defaultSerializeNoIndentedOptions
            };
            result = JsonSerializer.Serialize(obj, options);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return result;
    }

    /// <summary>
    /// Serialize Object to Json string
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string Serialize(object? obj, JsonSerializerOptions? options)
    {
        var result = string.Empty;
        try
        {
            if (obj == null)
            {
                return result;
            }
            result = JsonSerializer.Serialize(obj, options ?? _defaultSerializeOptions);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return result;
    }

    /// <summary>
    /// SerializeToNode
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="nullValue"></param>
    /// <returns></returns>
    public static JsonNode? SerializeToNode<T>(T? obj, bool nullValue = false)
    {
        var options = nullValue ? _nullValueSerializeOptions : _defaultSerializeOptions;
        return JsonSerializer.SerializeToNode(obj, options);
    }

    public static JsonArray? SerializeToArray(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }
        var array = new JsonArray();
        foreach (var value in values)
        {
            var valueNode = JsonSerializer.SerializeToNode(value, _defaultSerializeOptions);
            array.Add(valueNode);
        }
        return array;
    }
}

[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(RulesItem))]
[JsonSerializable(typeof(ProfileItem))]
[JsonSerializable(typeof(List<RulesItem>))]
[JsonSerializable(typeof(List<ProfileItem>))]
[JsonSerializable(typeof(RoutingTemplate))]
[JsonSerializable(typeof(DNSItem))]
[JsonSerializable(typeof(SimpleDNSItem))]
[JsonSerializable(typeof(IPAPIInfo))]
[JsonSerializable(typeof(SsSIP008))]
[JsonSerializable(typeof(List<SsServer>))]
[JsonSerializable(typeof(SsServer))]
[JsonSerializable(typeof(VmessQRCode))]
[JsonSerializable(typeof(ClashProxies))]
[JsonSerializable(typeof(ClashProviders))]
[JsonSerializable(typeof(ClashConnections))]
[JsonSerializable(typeof(ProtocolExtraItem))]
[JsonSerializable(typeof(TransportExtraItem))]
[JsonSerializable(typeof(TrafficItem))]
[JsonSerializable(typeof(V2rayMetricsVars))]
[JsonSerializable(typeof(V2rayMetricsVarsLink))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(List<GitHubRelease>))]
[JsonSerializable(typeof(ServerStatItem))]
[JsonSerializable(typeof(WebDavItem))]
[JsonSerializable(typeof(KeyEventItem))]
[JsonSerializable(typeof(SubItem))]
[JsonSerializable(typeof(SingboxConfig))]
[JsonSerializable(typeof(Dns4Sbox))]
[JsonSerializable(typeof(Inbound4Sbox))]
[JsonSerializable(typeof(List<Inbound4Sbox>))]
[JsonSerializable(typeof(Endpoints4Sbox))]
[JsonSerializable(typeof(List<Endpoints4Sbox>))]
[JsonSerializable(typeof(Outbound4Sbox))]
[JsonSerializable(typeof(List<Outbound4Sbox>))]
[JsonSerializable(typeof(Rule4Sbox))]
[JsonSerializable(typeof(Ruleset4Sbox))]
[JsonSerializable(typeof(List<Rule4Sbox>))]
[JsonSerializable(typeof(List<Ruleset4Sbox>))]
[JsonSerializable(typeof(V2rayConfig))]
[JsonSerializable(typeof(Dns4Ray))]
[JsonSerializable(typeof(DnsServer4Ray))]
[JsonSerializable(typeof(Inbounds4Ray))]
[JsonSerializable(typeof(List<Inbounds4Ray>))]
[JsonSerializable(typeof(Outbounds4Ray))]
[JsonSerializable(typeof(List<Outbounds4Ray>))]
[JsonSerializable(typeof(RulesItem4Ray))]
[JsonSerializable(typeof(List<RulesItem4Ray>))]
[JsonSerializable(typeof(Finalmask4Ray))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(Guid?))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
internal partial class AppJsonContext : JsonSerializerContext;
