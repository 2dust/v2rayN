using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace v2rayN;

internal static class StringEx
{
  public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
  {
    return string.IsNullOrEmpty(value);
  }

  public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
  {
    return string.IsNullOrWhiteSpace(value);
  }

  public static bool BeginWithAny(this string s, IEnumerable<char> chars)
  {
    if (s.IsNullOrEmpty()) return false;
    return chars.Contains(s[0]);
  }

  public static bool IsWhiteSpace(this string value)
  {
    foreach (char c in value)
    {
      if (char.IsWhiteSpace(c)) continue;

      return false;
    }
    return true;
  }

  public static IEnumerable<string> NonWhiteSpaceLines(this TextReader reader)
  {
    string? line;
    while ((line = reader.ReadLine()) != null)
    {
      if (line.IsWhiteSpace()) continue;
      yield return line;
    }
  }

  public static string TrimEx(this string? value)
  {
    return value == null ? string.Empty : value.Trim();
  }

  public static string RemovePrefix(this string value, char prefix)
  {
    if (value.StartsWith(prefix))
    {
      return value.Substring(1);
    }
    else
    {
      return value;
    }
  }

  public static string RemovePrefix(this string value, string prefix)
  {
    if (value.StartsWith(prefix))
    {
      return value.Substring(prefix.Length);
    }
    else
    {
      return value;
    }
  }

  public static string UpperFirstChar(this string value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    return char.ToUpper(value[0]) + value.Substring(1);
  }

  public static string AppendQuotes(this string value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    return $"\"{value}\"";
  }
}