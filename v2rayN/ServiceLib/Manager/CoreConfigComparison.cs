using System.Text.Json.Nodes;
using System.Globalization;

namespace ServiceLib.Manager;

internal static class CoreConfigComparison
{
    public static bool IsSupported(CoreConfigContext context, string? content)
    {
        if (content.IsNullOrEmpty()
            || context.RunCoreType is not (ECoreType.Xray or ECoreType.sing_box)
            || context.Node.ConfigType is EConfigType.Custom or EConfigType.Outbound
            || context.Node.ConfigType.IsGroupType()
            || context.FullConfigTemplate is { Enabled: true }
            || context.RawDnsItem is { Enabled: true }
            || context.CustomOutboundContent.Count != 0
            || context.AllProxiesMap.Values.Any(node => node.ConfigType is EConfigType.Custom or EConfigType.Outbound || node.ConfigType.IsGroupType())
            || context.RoutingItem?.CustomRulesetPath4Singbox.IsNotEmpty() == true)
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(content) is JsonObject root && !HasExternalInput(root);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static bool AreEquivalent(CoreConfigContext context, string previous, string current)
    {
        if (!IsSupported(context, current)) return false;
        try
        {
            if (JsonNode.Parse(previous) is not JsonObject before || JsonNode.Parse(current) is not JsonObject after)
                return false;

            // These copies are for comparison only. Launch the original prepared content.
            if (context.AppConfig.CoreBasicItem.LogEnabled
                && before["log"] is JsonObject oldLog && after["log"] is JsonObject newLog)
            {
                if (context.RunCoreType == ECoreType.Xray)
                {
                    ReuseGeneratedLogDate(oldLog, newLog, "access", "Vaccess_");
                    ReuseGeneratedLogDate(oldLog, newLog, "error", "Verror_");
                }
                else
                {
                    ReuseGeneratedLogDate(oldLog, newLog, "output", "sbox_");
                }
            }
            if (context.RunCoreType == ECoreType.Xray && context.IsMacOS && context.IsTunEnabled
                && before["inbounds"] is JsonArray oldInbounds && after["inbounds"] is JsonArray newInbounds
                && oldInbounds.Count == newInbounds.Count)
            {
                for (var i = 0; i < oldInbounds.Count; i++)
                {
                    if (oldInbounds[i] is JsonObject oldInbound && newInbounds[i] is JsonObject newInbound
                        && ReadString(oldInbound["protocol"]) == "tun" && ReadString(newInbound["protocol"]) == "tun"
                        && oldInbound["settings"] is JsonObject oldSettings && newInbound["settings"] is JsonObject newSettings
                        && IsGeneratedTunName(ReadString(oldSettings["name"])) && IsGeneratedTunName(ReadString(newSettings["name"])))
                    {
                        newSettings["name"] = ReadString(oldSettings["name"]);
                    }
                }
            }
            return JsonNode.DeepEquals(before, after);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool IsGeneratedTunName(string? name) => name is not null && name.StartsWith("utun", StringComparison.Ordinal)
        && int.TryParse(name.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        && number is >= 0 and < 99 && name == "utun" + number.ToString(CultureInfo.InvariantCulture);

    private static void ReuseGeneratedLogDate(JsonObject before, JsonObject after, string property, string prefix)
    {
        var oldPath = ReadString(before[property]);
        var newPath = ReadString(after[property]);
        if (oldPath is not null && newPath is not null
            && Path.GetDirectoryName(oldPath) == Path.GetDirectoryName(newPath)
            && IsGeneratedLogPath(oldPath, prefix) && IsGeneratedLogPath(newPath, prefix))
        {
            after[property] = oldPath;
        }
    }

    private static bool IsGeneratedLogPath(string path, string prefix)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return Path.GetExtension(path) == ".txt" && stem.StartsWith(prefix, StringComparison.Ordinal)
            && DateOnly.TryParseExact(stem.AsSpan(prefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool HasExternalInput(JsonNode? node) => node switch
    {
        JsonObject item => (item["type"] is JsonValue type && type.TryGetValue<string>(out var value)
                && value == "local" && item["path"] is not null)
            || item.Any(property => HasExternalInput(property.Value)),
        JsonArray items => items.Any(HasExternalInput),
        JsonValue item => item.TryGetValue<string>(out var value)
            && (value.StartsWith("geoip:", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("geosite:", StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };
}
