namespace ServiceLib.Common;

public class JsonUtils
{
    private static readonly string _tag = "JsonUtils";

    private static readonly JsonSerializerOptions _defaultDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions _defaultSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions _nullValueSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonDocumentOptions _defaultDocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip
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
            if (obj is null)
            {
                return result;
            }
            var options = nullValue ? _nullValueSerializeOptions : _defaultSerializeOptions;
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
            if (obj is null)
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
    /// <returns></returns>
    public static JsonNode? SerializeToNode(object? obj, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.SerializeToNode(obj, options);
    }
}
