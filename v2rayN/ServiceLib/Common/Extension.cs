using System.Diagnostics.CodeAnalysis;

namespace ServiceLib.Common;

public static class Extension
{
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(value);
    }

    public static bool IsNotEmpty([NotNullWhen(false)] this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string? NullIfEmpty(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool BeginWithAny(this string s, IEnumerable<char> chars)
    {
        if (s.IsNullOrEmpty())
        {
            return false;
        }
        return chars.Contains(s.First());
    }

    private static bool IsWhiteSpace(this string value)
    {
        return value.All(char.IsWhiteSpace);
    }

    public static IEnumerable<string> NonWhiteSpaceLines(this TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (line.IsWhiteSpace())
            {
                continue;
            }
            yield return line;
        }
    }

    public static string TrimSafe(this string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    public static string RemovePrefix(this string value, char prefix)
    {
        return value.StartsWith(prefix) ? value[1..] : value;
    }

    public static string RemovePrefix(this string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;
    }

    public static string UpperFirstChar(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (char.IsUpper(value[0]))
        {
            return value;
        }

        return string.Create(value.Length, value, (span, oldStr) =>
        {
            oldStr.AsSpan().CopyTo(span);
            span[0] = char.ToUpper(span[0], CultureInfo.InvariantCulture);
        });
    }

    public static string AppendQuotes(this string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : $"\"{value}\"";
    }

    public static int ToInt(this string? value, int defaultValue = 0)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public static List<string> AppendEmpty(this IEnumerable<string> source)
    {
        return source.Concat(new[] { string.Empty }).ToList();
    }

    public static bool IsGroupType(this EConfigType configType)
    {
        return configType is EConfigType.PolicyGroup or EConfigType.ProxyChain;
    }

    public static bool IsComplexType(this EConfigType configType)
    {
        return configType is EConfigType.Custom or EConfigType.PolicyGroup or EConfigType.ProxyChain;
    }
}
