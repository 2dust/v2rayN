using System.Collections;
using System.Text;

namespace v2rayN.WebAPI.Configuration;

internal sealed class NativeEnvironmentConfigurationException : Exception
{
    public NativeEnvironmentConfigurationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

internal static class NativeEnvironmentFile
{
    internal const int MaximumFileBytes = 64 * 1024;
    internal const int MaximumValueBytes = 8 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a minimal, non-shell KEY=value format. Comments occupy whole lines; the first '='
    /// separates key and value. A value may be unquoted or wrapped in one matching pair of
    /// single/double quotes. Quotes cannot be escaped, multiline values and shell syntax are
    /// rejected, and the first occurrence of a duplicate key wins.
    /// </summary>
    internal static void LoadFromExecutableDirectory(string executableDirectory)
    {
        var path = Path.Combine(Path.GetFullPath(executableDirectory), ".env");
        if (Directory.Exists(path))
            throw new NativeEnvironmentConfigurationException($"Invalid environment configuration in '{path}': expected a regular file.");
        if (!File.Exists(path)) return;

        Dictionary<string, string> values;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
                throw new NativeEnvironmentConfigurationException($"The file exceeds the {MaximumFileBytes}-byte size limit.");

            using var contents = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                if (contents.Length + read > MaximumFileBytes)
                    throw new NativeEnvironmentConfigurationException($"The file exceeds the {MaximumFileBytes}-byte size limit.");
                contents.Write(buffer, 0, read);
            }

            values = Parse(StrictUtf8.GetString(contents.GetBuffer(), 0, checked((int)contents.Length)));
        }
        catch (NativeEnvironmentConfigurationException exception)
        {
            throw new NativeEnvironmentConfigurationException($"Invalid environment configuration in '{path}': {exception.Message}", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new NativeEnvironmentConfigurationException($"Could not read environment configuration from '{path}'.", exception);
        }

        var processEnvironment = ReadProcessEnvironment();
        var mergedValues = Merge(values, processEnvironment);
        foreach (var key in values.Keys)
        {
            // Never replace a variable inherited from the OS, systemd, or the container runtime.
            if (!processEnvironment.ContainsKey(key))
                Environment.SetEnvironmentVariable(key, mergedValues[key]);
        }
    }

    internal static Dictionary<string, string> Parse(string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (StrictUtf8.GetByteCount(contents) > MaximumFileBytes)
            throw new NativeEnvironmentConfigurationException($"The file exceeds the {MaximumFileBytes}-byte size limit.");
        if (contents.Contains('\0'))
            throw new NativeEnvironmentConfigurationException("NUL characters are not allowed.");

        if (contents.Length > 0 && contents[0] == '\uFEFF')
            contents = contents[1..];

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(contents);
        var lineNumber = 0;
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw InvalidLine(lineNumber, "expected KEY=value");

            var key = line[..separator];
            if (!IsValidKey(key))
                throw InvalidLine(lineNumber, "invalid key name");

            var value = ParseValue(line[(separator + 1)..], lineNumber);
            // Duplicate keys are valid but deterministic: the first occurrence wins.
            if (!parsed.ContainsKey(key)) parsed.Add(key, value);
        }

        return parsed;
    }

    internal static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> fileValues,
        IReadOnlyDictionary<string, string?> processEnvironment)
    {
        var merged = new Dictionary<string, string>(EnvironmentVariableComparer());
        foreach (var (key, fileValue) in fileValues)
        {
            // Presence wins even when the process supplied an empty value.
            merged[key] = processEnvironment.TryGetValue(key, out var processValue)
                ? processValue ?? string.Empty
                : fileValue;
        }
        return merged;
    }

    private static Dictionary<string, string?> ReadProcessEnvironment()
    {
        var values = new Dictionary<string, string?>(EnvironmentVariableComparer());
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            values[(string)entry.Key] = entry.Value?.ToString();
        return values;
    }

    private static string ParseValue(string value, int lineNumber)
    {
        if (value.Length > 0 && value[0] is '\'' or '"')
        {
            var quote = value[0];
            if (value.Length < 2 || value[^1] != quote)
                throw InvalidLine(lineNumber, "malformed quoted value");

            value = value[1..^1];
            if (value.Contains(quote))
                throw InvalidLine(lineNumber, "quoted values do not support escapes or embedded matching quotes");
        }
        else if (value.Contains('\'') || value.Contains('"'))
        {
            throw InvalidLine(lineNumber, "quotes must wrap the entire value");
        }

        if (StrictUtf8.GetByteCount(value) > MaximumValueBytes)
            throw InvalidLine(lineNumber, $"value exceeds the {MaximumValueBytes}-byte size limit");

        // This is a plain-text format, not a shell: reject shell expansion/escape syntax rather
        // than interpreting it. No command or variable expansion is ever performed.
        if (value.Contains('$') || value.Contains('`') || value.Contains('\\'))
            throw InvalidLine(lineNumber, "shell expansion and escape syntax are not supported");

        return value;
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length == 0 || !IsAsciiLetter(key[0]) && key[0] != '_') return false;
        for (var index = 1; index < key.Length; index++)
        {
            var character = key[index];
            if (!IsAsciiLetter(character) && !char.IsAsciiDigit(character) && character != '_') return false;
        }
        return true;
    }

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static StringComparer EnvironmentVariableComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static NativeEnvironmentConfigurationException InvalidLine(int lineNumber, string reason) =>
        new($"Line {lineNumber}: {reason}.");
}
