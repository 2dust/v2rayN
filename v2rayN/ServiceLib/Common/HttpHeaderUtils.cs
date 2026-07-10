namespace ServiceLib.Common;

public static class HttpHeaderUtils
{
    public static bool TryParse(string? httpHeaders, out JsonObject? headers)
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
                    || !IsHeaderName(item.Name)
                    || !IsHeaderValue(item.Value))
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

    private static bool IsHeaderName(string name)
    {
        return name.Length > 0 && name.All(IsHeaderNameChar);
    }

    private static bool IsHeaderNameChar(char c)
    {
        return char.IsAsciiLetterOrDigit(c)
               || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`'
                   or '|' or '~';
    }

    private static bool IsHeaderValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return IsHeaderValueString(element.GetString());
        }

        return element.ValueKind == JsonValueKind.Array
               && element.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String
                                                      && IsHeaderValueString(item.GetString()));
    }

    private static bool IsHeaderValueString(string? value)
    {
        if (value is null)
        {
            return false;
        }

        return value.Length == 0
               || IsFieldVChar(value[0])
               && IsFieldVChar(value[^1])
               && value.All(c => IsFieldVChar(c) || c is ' ' or '\t');
    }

    private static bool IsFieldVChar(char c)
    {
        return c is >= '\u0021' and <= '\u007E' or >= '\u0080';
    }
}
