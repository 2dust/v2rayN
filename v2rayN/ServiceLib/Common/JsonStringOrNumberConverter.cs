namespace ServiceLib.Common;

/// <summary>
/// Accepts either a JSON string or number and stores the value as a string.
/// This keeps existing routing rule storage intact while allowing numeric clipboard input.
/// </summary>
public sealed class JsonStringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumber(reader),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for string-or-number value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    private static string ReadNumber(Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var longValue))
        {
            return longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (reader.TryGetDouble(out var doubleValue))
        {
            return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
