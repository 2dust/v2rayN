namespace ServiceLib.Services;

/// <summary>
/// Prepares an isolated copy of a full custom client config. Local inbound
/// ports are remapped while the user's outbound and routing graph is never
/// reconstructed by v2rayN.
/// </summary>
public static class CustomSpeedtestConfig
{
    public static int GetInboundCount(string content)
    {
        return JsonUtils.ParseJson(content) is JsonObject config
            && config["inbounds"] is JsonArray inbounds
            ? inbounds.Count
            : 0;
    }

    public static bool TryChangePort(string content, ECoreType coreType, int port, bool requireUdp,
        out string? testConfig, out string? reason)
    {
        return TryChangePorts(content, coreType, [port], requireUdp, out testConfig, out _, out reason);
    }

    public static bool TryChangePorts(string content, ECoreType coreType, IReadOnlyList<int> ports, bool requireUdp,
        out string? testConfig, out int testPort, out string? reason)
    {
        testConfig = null;
        testPort = 0;
        reason = null;
        if (JsonUtils.ParseJson(content) is not JsonObject config
            || config["inbounds"] is not JsonArray { Count: > 0 } inbounds
            || config["outbounds"] is not JsonArray { Count: > 0 })
        {
            reason = "Custom test requires a full JSON config with at least one inbound and one outbound.";
            return false;
        }
        if (ports.Count != inbounds.Count || ports.Any(it => it is <= 0 or > 65535)
            || ports.Distinct().Count() != ports.Count)
        {
            reason = "Could not allocate isolated ports for all Custom configuration inbounds.";
            return false;
        }

        // Other cores and configuration formats do not share these inbound semantics.
        var isRay = coreType is ECoreType.Xray or ECoreType.v2fly;
        var isSingbox = coreType == ECoreType.sing_box;
        if (!isRay && !isSingbox)
        {
            reason = $"Custom test is not supported for {coreType}.";
            return false;
        }

        var portKey = isRay ? "port" : "listen_port";
        if (isRay)
        {
            if (config["metrics"] is not null)
            {
                reason = "Custom test does not support an additional Xray metrics listener.";
                return false;
            }
        }
        else if (config["experimental"] is JsonObject experimental
                 && (experimental["clash_api"] is not null || experimental["v2ray_api"] is not null))
        {
            reason = "Custom test does not support additional API listeners.";
            return false;
        }

        var proxyInbounds = new List<JsonObject>();
        var allInbounds = new List<JsonObject>(inbounds.Count);
        foreach (var item in inbounds)
        {
            if (item is not JsonObject inbound || GetString(inbound, "listen") != Global.Loopback
                || !TryGetPort(inbound, portKey, out _))
            {
                reason = "Custom test requires every inbound to use an explicit loopback address and numeric port.";
                return false;
            }
            allInbounds.Add(inbound);

            var protocol = GetString(inbound, isRay ? "protocol" : "type");
            var hasCompatibleProtocol = isRay ? protocol == "socks" : protocol is "socks" or "mixed";
            if (!hasCompatibleProtocol || !IsUnauthenticated(inbound, isRay)
                || (isRay && requireUdp && !HasRayUdp(inbound)))
            {
                continue;
            }
            proxyInbounds.Add(inbound);
        }
        if (proxyInbounds.Count != 1)
        {
            reason = requireUdp && isRay
                ? "Custom UDP test requires exactly one unauthenticated loopback SOCKS inbound with UDP explicitly enabled."
                : "Custom test requires exactly one unauthenticated loopback SOCKS-compatible inbound.";
            return false;
        }

        var proxyInbound = proxyInbounds[0];
        proxyInbound[portKey] = ports[0];
        var nextPort = 1;
        foreach (var inbound in allInbounds)
        {
            if (!ReferenceEquals(inbound, proxyInbound))
            {
                inbound[portKey] = ports[nextPort++];
            }
        }
        testPort = ports[0];
        testConfig = config.ToJsonString();
        return true;
    }

    private static bool IsUnauthenticated(JsonObject inbound, bool isRay)
    {
        if (isRay)
        {
            return inbound["settings"] is not JsonObject settings
                   || ((GetString(settings, "auth") is not { } auth || auth == "noauth")
                       && settings["accounts"] is not JsonArray { Count: > 0 });
        }
        return inbound["users"] is not JsonArray { Count: > 0 };
    }

    private static bool HasRayUdp(JsonObject inbound)
    {
        return inbound["settings"] is JsonObject settings
               && settings["udp"] is JsonValue udp
               && udp.TryGetValue<bool>(out var enabled)
               && enabled;
    }

    private static string? GetString(JsonObject obj, string key)
    {
        return obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    private static bool TryGetPort(JsonObject obj, string key, out int port)
    {
        port = 0;
        return obj[key] is JsonValue value && value.TryGetValue<int>(out port) && port is > 0 and <= 65535;
    }
}
