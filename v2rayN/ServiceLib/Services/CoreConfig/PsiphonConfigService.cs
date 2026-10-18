namespace ServiceLib.Services.CoreConfig;

/// <summary>Adapts an imported Psiphon network configuration to a managed local proxy.</summary>
public static class PsiphonConfigService
{
    public static bool DisableFailedMode(Config config)
    {
        if (config.PsiphonMode is not ("only" or "after")) return false;
        config.PsiphonMode = "off";
        // A failed overlay must not change the user's separate TUN choice.
        return true;
    }

    public static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally { listener.Stop(); }
    }
    public record RegionOption(string Code, string Name);

    private static readonly string[] FallbackRegionCodes =
        ["AT", "BE", "CA", "CH", "CZ", "DE", "DK", "ES", "FR", "GB", "IE", "IT", "JP", "LT", "NL", "NO", "PL", "RO", "RS", "SE", "US"];

    private static string RegionCachePath => Utils.GetConfigPath("psiphon_regions.json");

    // Psiphon reports this list from the currently downloaded server entries. The fallback is the
    // list reported by the bundled bootstrap during integration testing, not the full ISO list.
    public static IReadOnlyList<RegionOption> Regions
    {
        get
        {
            var codes = FallbackRegionCodes.AsEnumerable();
            try
            {
                if (File.Exists(RegionCachePath))
                {
                    var cached = JsonUtils.Deserialize<List<string>>(File.ReadAllText(RegionCachePath));
                    if (cached is { Count: > 0 }) codes = cached;
                }
            }
            catch { }
            return new[] { new RegionOption("", "Automatic (best available)") }
                .Concat(codes.Distinct().Select(code =>
                {
                    try
                    {
                        var region = new System.Globalization.RegionInfo(code);
                        return new RegionOption(code, $"{region.EnglishName} ({code})");
                    }
                    catch { return new RegionOption(code, code); }
                }).OrderBy(region => region.Name))
                .ToList();
        }
    }

    public static void CaptureAvailableRegions(string notice)
    {
        try
        {
            var json = JsonNode.Parse(notice);
            if (json?["noticeType"]?.GetValue<string>() != "AvailableEgressRegions") return;
            var codes = json["data"]?["regions"]?.AsArray()
                .Select(item => item?.GetValue<string>()?.Trim().ToUpperInvariant())
                .Where(code => code is { Length: 2 } && code.All(char.IsAsciiLetter))
                .Cast<string>()
                .Distinct()
                .Order()
                .ToList();
            if (codes is not { Count: > 0 }) return;
            File.WriteAllText(RegionCachePath, JsonUtils.Serialize(codes));
        }
        catch { }
    }

    public const string PinnedHostsTag = "upstream-hosts";

    public static string SimplifyTunFrontend(string source, IEnumerable<IPAddress>? upstreamAddresses = null, bool preserveRuleSets = false,
        IReadOnlyDictionary<string, IPAddress[]>? pinnedHosts = null)
    {
        var config = JsonNode.Parse(source)?.AsObject()
            ?? throw new ArgumentException("The TUN frontend configuration is invalid.");
        static bool ContainsRuleSet(JsonNode? node)
        {
            if (node is JsonObject obj)
                return obj.Any(item => item.Key == "rule_set" || ContainsRuleSet(item.Value));
            if (node is JsonArray array) return array.Any(ContainsRuleSet);
            return false;
        }
        static void RemoveRuleSetRules(JsonNode? section)
        {
            if (section?["rules"] is not JsonArray rules) return;
            for (var index = rules.Count - 1; index >= 0; index--)
            {
                if (ContainsRuleSet(rules[index])) rules.RemoveAt(index);
            }
        }
        if (!preserveRuleSets)
        {
            RemoveRuleSetRules(config["dns"]);
            RemoveRuleSetRules(config["route"]);
        }
        if (config["route"] is JsonObject route)
        {
            if (!preserveRuleSets) route["rule_set"] = new JsonArray();
            if (route["rules"] is JsonArray rules)
            {
                var coreBypass = rules.FirstOrDefault(rule =>
                    rule?["outbound"]?.GetValue<string>() == "direct"
                    && rule["process_path"] is JsonArray);
                if (coreBypass != null)
                {
                    // Different rule fields are ANDed. A process-name fallback must be
                    // independent of the exact path (including its case on Windows).
                    var names = coreBypass["process_name"]?.DeepClone() ?? new JsonArray(
                        ((JsonArray)coreBypass["process_path"]!).Select(path =>
                            JsonValue.Create(Path.GetFileName(path?.GetValue<string>()))).ToArray());
                    coreBypass.AsObject().Remove("process_name");
                    rules.Remove(coreBypass);
                    rules.Insert(0, coreBypass);
                    rules.Insert(0, new JsonObject { ["outbound"] = "direct", ["process_name"] = names });
                }
            }
        }
        var excluded = upstreamAddresses?.Distinct().ToList();
        if (excluded is { Count: > 0 } && config["inbounds"] is JsonArray inbounds)
        {
            foreach (var inbound in inbounds.Where(item => item?["type"]?.GetValue<string>() == "tun"))
            {
                var addresses = inbound!["route_exclude_address"] as JsonArray ?? new JsonArray();
                if (inbound["route_exclude_address"] == null) inbound["route_exclude_address"] = addresses;
                foreach (var address in excluded)
                {
                    var cidr = $"{address}/{(address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128)}";
                    if (!addresses.Any(value => value?.GetValue<string>() == cidr)) addresses.Add(cidr);
                }
            }
        }
        PinUpstreamHosts(config, pinnedHosts);
        if (!preserveRuleSets) config.Remove("http_clients");
        return config.ToJsonString(new() { WriteIndented = true });
    }

    // Windows resolves names for xray.exe through its DNS client service, so those queries enter
    // the TUN and would otherwise be sent to remote DNS through the proxy chain itself. When the
    // tunnel drops and the cached record expires, the upstream can no longer find its own server
    // and the chain cannot recover. Answer the upstream's names locally with the addresses that
    // were resolved before TUN started (the same addresses excluded from the TUN route).
    private static void PinUpstreamHosts(JsonObject config, IReadOnlyDictionary<string, IPAddress[]>? pinnedHosts)
    {
        if (pinnedHosts is not { Count: > 0 } || config["dns"] is not JsonObject dns) return;
        var predefined = new JsonObject();
        foreach (var (host, addresses) in pinnedHosts)
        {
            var name = host?.Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(name) || IPAddress.TryParse(name, out _) || addresses is not { Length: > 0 }) continue;
            predefined[name] = new JsonArray(addresses.Distinct().Select(address => (JsonNode?)JsonValue.Create(address.ToString())).ToArray());
        }
        if (predefined.Count == 0) return;
        var names = new JsonArray(predefined.Select(item => (JsonNode?)JsonValue.Create(item.Key)).ToArray());
        if (dns["servers"] is not JsonArray servers) dns["servers"] = servers = new JsonArray();
        servers.Add(new JsonObject { ["type"] = "hosts", ["tag"] = PinnedHostsTag, ["predefined"] = predefined });
        if (dns["rules"] is not JsonArray rules) dns["rules"] = rules = new JsonArray();
        rules.Insert(0, new JsonObject { ["domain"] = names, ["server"] = PinnedHostsTag });
    }

    public static JsonObject CreateConfig(string source, string? region, int port, string dataDirectory, int? upstreamPort = null)
    {
        var config = JsonNode.Parse(source) as JsonObject
            ?? throw new ArgumentException("The Psiphon configuration must be a JSON object.");
        if (port is < 1 or > 65535)
        {
            throw new ArgumentException("Set a Psiphon SOCKS port between 1 and 65535.");
        }
        if (region != null && region != "" && !Regions.Any(r => r.Code == region))
        {
            throw new ArgumentException("Choose a valid Psiphon country or Automatic.");
        }
        foreach (var key in new[] { "PropagationChannelId", "SponsorId" })
        {
            if (string.IsNullOrWhiteSpace(config[key]?.GetValue<string>()))
            {
                throw new ArgumentException($"The imported Psiphon network configuration is missing {key}.");
            }
        }
        if (region != null)
        {
            config["EgressRegion"] = region;
        }
        config["LocalSocksProxyPort"] = port;
        config["DisableLocalSocksProxy"] = false;
        config["DisableLocalHTTPProxy"] = true;
        config["ListenInterface"] = ""; // Psiphon's documented loopback default.
        config["DataRootDirectory"] = dataDirectory;
        config.Remove("UpstreamProxyURL"); // Normalize the spelling accepted by older clients.
        config.Remove("UpstreamProxyUrl");
        if (upstreamPort != null)
        {
            if (upstreamPort is < 1 or > 65535 || upstreamPort == port)
            {
                throw new ArgumentException("Psiphon and upstream SOCKS ports must be valid and different.");
            }
            config["UpstreamProxyUrl"] = $"socks5://127.0.0.1:{upstreamPort}";
        }
        // Light fallback does not honor EgressRegion. Never silently change the requested exit.
        config["EnableLightProxyFallback"] = false;
        config["SplitTunnelOwnRegion"] = false;
        config["SplitTunnelRegions"] = new JsonArray();
        // Same fallback resolvers the MahsaNG/psiphon-android clients configure, used by Psiphon
        // when the system resolver is poisoned or unreachable.
        if (config["DNSResolverAlternateServers"] is not JsonArray { Count: > 0 })
        {
            config["DNSResolverAlternateServers"] = new JsonArray("1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4");
        }
        config["EmitDiagnosticNotices"] = false;
        config["EmitDiagnosticNetworkParameters"] = false;
        return config;
    }

    public static async Task<RetResult> Generate(CoreConfigContext context)
    {
        try
        {
            var node = context.Node;
            var sourcePath = File.Exists(node.Address) ? node.Address : Utils.GetConfigPath(node.Address);
            var dataPath = Utils.GetBinConfigPath("psiphon-data");
            Directory.CreateDirectory(dataPath);
            var config = CreateConfig(await File.ReadAllTextAsync(sourcePath),
                node.GetProtocolExtra().PsiphonRegion, node.PreSocksPort ?? 0, dataPath,
                node.GetProtocolExtra().PsiphonUseUpstream == true ? node.GetProtocolExtra().PsiphonUpstreamPort ?? 1089 : null);
            if (node.PreSocksPort == AppManager.Instance.GetLocalPort(EInboundProtocol.socks))
            {
                return new RetResult { Msg = "The Psiphon SOCKS port must differ from v2rayN's local listening port." };
            }
            return new RetResult { Success = true, Data = config.ToJsonString(new() { WriteIndented = true }) };
        }
        catch (Exception ex)
        {
            return new RetResult { Msg = $"Psiphon configuration: {ex.Message}" };
        }
    }

    public static bool IsSupportedUpstream(ProfileItem node) =>
        node.ConfigType is EConfigType.VMess or EConfigType.VLESS or EConfigType.Trojan
            or EConfigType.Shadowsocks or EConfigType.SOCKS or EConfigType.HTTP;

    public static async Task<RetResult> GenerateUpstream(Config appConfig, ProfileItem psiphonNode)
    {
        var extra = psiphonNode.GetProtocolExtra();
        var upstream = await AppManager.Instance.GetProfileItem(extra.PsiphonUpstreamProfileId ?? "");
        if (upstream == null || !IsSupportedUpstream(upstream))
        {
            return new RetResult { Msg = "Select an existing VMess, VLESS, Trojan, Shadowsocks, SOCKS or HTTP upstream profile for Psiphon." };
        }
        var port = extra.PsiphonUpstreamPort ?? 1089;
        if (port == AppManager.Instance.GetLocalPort(EInboundProtocol.socks))
        {
            return new RetResult { Msg = "The upstream port conflicts with v2rayN's listening port." };
        }
        var config = JsonUtils.DeepCopy(appConfig);
        config.TunModeItem.EnableTun = false;
        config.GuiItem.EnableStatistics = false;
        var node = JsonUtils.DeepCopy(upstream);
        node.CoreType = ECoreType.Xray;
        node.Subid = ""; // The explicitly chosen profile is the first hop.
        var build = await CoreConfigContextBuilder.Build(config, node);
        if (!build.Success)
        {
            return new RetResult { Msg = "The chosen Psiphon upstream profile is invalid." };
        }
        var result = new CoreConfigV2rayService(build.Context with { FullConfigTemplate = null }).GenerateClientConfigContent();
        if (!result.Success)
        {
            return result;
        }
        var json = JsonNode.Parse(result.Data!.ToString()!)!.AsObject();
        json["inbounds"] = JsonNode.Parse($$$"""
            [{"tag":"psiphon-upstream","listen":"127.0.0.1","port":{{{port}}},"protocol":"socks","settings":{"auth":"noauth","udp":false}}]
            """);
        json.Remove("api");
        json.Remove("stats");
        json.Remove("metrics");
        json["routing"] = JsonNode.Parse("""
            {"domainStrategy":"AsIs","rules":[{"type":"field","inboundTag":["psiphon-upstream"],"outboundTag":"proxy"}]}
            """);
        result.Data = json.ToJsonString(new() { WriteIndented = true });
        return result;
    }
}
