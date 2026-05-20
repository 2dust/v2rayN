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

    private static readonly JsonSerializerOptions _defaultSerializeNoIndentedOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions _nullValueSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions _nullValueSerializeNoIndentedOptions = new()
    {
        WriteIndented = false,
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

            return JsonNode.Parse(NormalizeBrokenJson(strJson), nodeOptions: null, _defaultDocumentOptions);
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
    /// <returns></returns>
    public static JsonNode? SerializeToNode(object? obj, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.SerializeToNode(obj, options);
    }

    /// <summary>
    /// Normalizes a broken JSON-like string by removing plus signs (+)
    /// that are located outside quoted string values.
    /// </summary>
    /// <param name="json">
    /// The JSON-like input string that may contain invalid plus signs.
    /// </param>
    /// <returns>
    /// A normalized JSON string that can be parsed by <see cref="JsonNode.Parse(string?, JsonNodeOptions?, JsonDocumentOptions)"/>.
    /// </returns>
    /// <remarks>
    /// This method is designed for cases where the input looks similar to JSON
    /// but contains invalid plus signs, such as:
    ///
    /// <code>
    /// {"scMaxEachPostBytes":+1000000,+"scMaxConcurrentPosts":+100,+"xPaddingBytes":+"100-1000"}
    /// </code>
    ///
    /// The method removes only plus signs that are outside string values.
    ///
    /// It does not remove plus signs inside quoted strings:
    ///
    /// <code>
    /// {"phone":"+49123456789"}
    /// </code>
    ///
    /// This behavior prevents accidental modification of valid string data.
    /// </remarks>
    private static string NormalizeBrokenJson(string json)
    {
        var result = new StringBuilder(json.Length);

        bool insideString = false;
        bool escaped = false;

        foreach (char ch in json)
        {
            if (escaped)
            {
                result.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\' && insideString)
            {
                result.Append(ch);
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                insideString = !insideString;
                result.Append(ch);
                continue;
            }

            // Remove plus signs only when they are outside string values.
            if (!insideString && ch == '+')
            {
                continue;
            }

            result.Append(ch);
        }

        return result.ToString();
    }
}
