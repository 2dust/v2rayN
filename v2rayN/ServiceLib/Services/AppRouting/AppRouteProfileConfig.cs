namespace ServiceLib.Services.AppRouting;

internal static class AppRouteProfileConfig
{
    internal static RoutingItem GetBlockingRouting(Config config, RoutingItem? routing)
    {
        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing?.RuleSet) ?? [];
        return new RoutingItem
        {
            DomainStrategy = routing?.DomainStrategy.NullIfEmpty() ?? config.RoutingBasicItem.DomainStrategy,
            RuleSet = JsonUtils.Serialize(rules.Where(r => r.Enabled && r.RuleType != ERuleType.DNS && r.OutboundTag == Global.BlockTag))
        };
    }

    internal static string Generate(CoreConfigContext context, AppRouteRule endpoint)
    {
        var config = JsonUtils.DeepCopy(context.AppConfig);
        config.TunModeItem.EnableTun = false;
        // Application routing carries QUIC too, regardless of the main client's default XUDP policy.
        config.Mux4RayItem.XudpProxyUDP443 = "allow";
        var isolated = context with
        {
            AppConfig = config,
            IsTunEnabled = false,
            RoutingItem = endpoint.ApplyBlockingRules ? GetBlockingRouting(config, context.RoutingItem) : null
        };
        var generated = new CoreConfigV2rayService(isolated).GenerateClientSocksConfig(endpoint.SocksPort, endpoint.ApplyBlockingRules);
        if (!generated.Success || generated.Data is not string json)
        {
            throw new InvalidOperationException(generated.Msg);
        }

        var root = JsonNode.Parse(json)!;
        var inbound = root["inbounds"]![0]!;
        inbound["listen"] = "127.0.0.1";
        inbound["protocol"] = "socks";
        if (endpoint.ApplyBlockingRules)
        {
            // Match rules scoped to the main SOCKS inbound. Transparent traffic
            // supplies IPs, so recover HTTP/TLS/QUIC names for routing only.
            inbound["tag"] = nameof(EInboundProtocol.socks);
            inbound["sniffing"] = new JsonObject
            {
                ["enabled"] = true,
                ["destOverride"] = new JsonArray("http", "tls", "quic"),
                ["routeOnly"] = true
            };
        }
        inbound["settings"] = new JsonObject
        {
            ["auth"] = "password",
            ["udp"] = true,
            ["ip"] = "127.0.0.1",
            ["accounts"] = new JsonArray(new JsonObject { ["user"] = endpoint.SocksUsername, ["pass"] = endpoint.SocksPassword })
        };
        return root.ToJsonString();
    }
}
