namespace ServiceLib.Common;

public static class EmbedUtils
{
    private static readonly string _tag = "EmbedUtils";
    private static readonly ConcurrentDictionary<string, string> _dicEmbedCache = new();

    /// <summary>
    /// Get embedded text resources
    /// </summary>
    /// <param name="res"></param>
    /// <returns></returns>
    public static string GetEmbedText(string res)
    {
        if (_dicEmbedCache.TryGetValue(res, out var value))
        {
            return value;
        }
        var result = string.Empty;

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(res);
            ArgumentNullException.ThrowIfNull(stream);
            using StreamReader reader = new(stream);
            result = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        _dicEmbedCache.TryAdd(res, result);
        return result;
    }

    /// <summary>
    /// Get local storage resources
    /// </summary>
    /// <returns></returns>
    public static string? LoadResource(string? res)
    {
        try
        {
            // Only read files inside the configuration directory. This method is reachable from
            // user-supplied routing items (RoutingItem.CustomRulesetPath4Singbox), so without this
            // confinement it doubles as an arbitrary-file-read primitive for anything the process
            // can access (private keys, browser profiles, ...).
            var fullRes = Path.GetFullPath(res);
            var configDir = Path.GetFullPath(Utils.GetConfigPath());
            if (fullRes.StartsWith(configDir, StringComparison.OrdinalIgnoreCase) && File.Exists(fullRes))
            {
                return File.ReadAllText(fullRes);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return null;
    }
}
