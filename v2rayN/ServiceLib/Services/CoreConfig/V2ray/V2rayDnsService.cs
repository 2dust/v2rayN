using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenDns(ProfileItem? node, V2rayConfig v2rayConfig)
    {
        try
        {
            var item = await AppManager.Instance.GetDNSItem(ECoreType.Xray);
            if (item != null && item.Enabled == true)
            {
                var result = await GenDnsCompatible(node, v2rayConfig);

                if (v2rayConfig.routing.domainStrategy == Global.IPIfNonMatch)
                {
                    // DNS routing
                    v2rayConfig.dns.tag = Global.DnsTag;
                    v2rayConfig.routing.rules.Add(new RulesItem4Ray
                    {
                        type = "field",
                        inboundTag = new List<string> { Global.DnsTag },
                        outboundTag = Global.ProxyTag,
                    });
                }

                return result;
            }
            var simpleDNSItem = _config.SimpleDNSItem;
            var domainStrategy4Freedom = simpleDNSItem?.RayStrategy4Freedom;

            //Outbound Freedom domainStrategy
            if (domainStrategy4Freedom.IsNotEmpty())
            {
                var outbound = v2rayConfig.outbounds.FirstOrDefault(t => t is { protocol: "freedom", tag: Global.DirectTag });
                if (outbound != null)
                {
                    outbound.settings = new()
                    {
                        domainStrategy = domainStrategy4Freedom,
                        userLevel = 0
                    };
                }
            }

            await GenDnsServers(node, v2rayConfig, simpleDNSItem);
            await GenDnsHosts(v2rayConfig, simpleDNSItem);

            if (v2rayConfig.routing.domainStrategy == Global.IPIfNonMatch)
            {
                // DNS routing
                v2rayConfig.dns.tag = Global.DnsTag;
                v2rayConfig.routing.rules.Add(new RulesItem4Ray
                {
                    type = "field",
                    inboundTag = new List<string> { Global.DnsTag },
                    outboundTag = Global.ProxyTag,
                });
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsServers(ProfileItem? node, V2rayConfig v2rayConfig, SimpleDNSItem simpleDNSItem)
    {
        static List<string> ParseDnsAddresses(string? dnsInput, string defaultAddress)
        {
            var addresses = dnsInput?.Split(dnsInput.Contains(',') ? ',' : ';')
                .Select(addr => addr.Trim())
                .Where(addr => !string.IsNullOrEmpty(addr))
                .Select(addr => addr.StartsWith("dhcp", StringComparison.OrdinalIgnoreCase) ? "localhost" : addr)
                .Distinct()
                .ToList() ?? new List<string> { defaultAddress };
            return addresses.Count > 0 ? addresses : new List<string> { defaultAddress };
        }

        static object CreateDnsServer(string dnsAddress, List<string> domains, List<string>? expectedIPs = null)
        {
            var dnsServer = new DnsServer4Ray
            {
                address = dnsAddress,
                skipFallback = true,
                domains = domains.Count > 0 ? domains : null,
                expectedIPs = expectedIPs?.Count > 0 ? expectedIPs : null
            };
            return JsonUtils.SerializeToNode(dnsServer, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        var directDNSAddress = ParseDnsAddresses(simpleDNSItem?.DirectDNS, Global.DomainDirectDNSAddress.FirstOrDefault());
        var remoteDNSAddress = ParseDnsAddresses(simpleDNSItem?.RemoteDNS, Global.DomainRemoteDNSAddress.FirstOrDefault());

        var directDomainList = new List<string>();
        var directGeositeList = new List<string>();
        var proxyDomainList = new List<string>();
        var proxyGeositeList = new List<string>();
        var expectedDomainList = new List<string>();
        var expectedIPs = new List<string>();
        var regionNames = new HashSet<string>();

        if (!string.IsNullOrEmpty(simpleDNSItem?.DirectExpectedIPs))
        {
            expectedIPs = simpleDNSItem.DirectExpectedIPs
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var ip in expectedIPs)
            {
                if (ip.StartsWith("geoip:", StringComparison.OrdinalIgnoreCase))
                {
                    var region = ip["geoip:".Length..];
                    if (!string.IsNullOrEmpty(region))
                    {
                        regionNames.Add($"geosite:{region}");
                        regionNames.Add($"geosite:geolocation-{region}");
                        regionNames.Add($"geosite:tld-{region}");
                    }
                }
            }
        }

        var routing = await ConfigHandler.GetDefaultRouting(_config);
        List<RulesItem>? rules = null;
        if (routing != null)
        {
            rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];
            foreach (var item in rules)
            {
                if (!item.Enabled || item.Domain is null || item.Domain.Count == 0)
                {
                    continue;
                }

                foreach (var domain in item.Domain)
                {
                    if (domain.StartsWith('#'))
                        continue;
                    var normalizedDomain = domain.Replace(Global.RoutingRuleComma, ",");

                    if (item.OutboundTag == Global.DirectTag)
                    {
                        if (normalizedDomain.StartsWith("geosite:"))
                        {
                            (regionNames.Contains(normalizedDomain) ? expectedDomainList : directGeositeList).Add(normalizedDomain);
                        }
                        else
                        {
                            directDomainList.Add(normalizedDomain);
                        }
                    }
                    else if (item.OutboundTag != Global.BlockTag)
                    {
                        if (normalizedDomain.StartsWith("geosite:"))
                        {
                            proxyGeositeList.Add(normalizedDomain);
                        }
                        else
                        {
                            proxyDomainList.Add(normalizedDomain);
                        }
                    }
                }
            }
        }

        if (Utils.IsDomain(node?.Address))
        {
            directDomainList.Add(node.Address);
        }

        if (node?.Subid is not null)
        {
            var subItem = await AppManager.Instance.GetSubItem(node.Subid);
            if (subItem is not null)
            {
                foreach (var profile in new[] { subItem.PrevProfile, subItem.NextProfile })
                {
                    var profileNode = await AppManager.Instance.GetProfileItemViaRemarks(profile);
                    if (profileNode is not null
                        && Global.XraySupportConfigType.Contains(profileNode.ConfigType)
                        && Utils.IsDomain(profileNode.Address))
                    {
                        directDomainList.Add(profileNode.Address);
                    }
                }
            }
        }

        v2rayConfig.dns ??= new Dns4Ray();
        v2rayConfig.dns.servers ??= new List<object>();

        void AddDnsServers(List<string> dnsAddresses, List<string> domains, List<string>? expectedIPs = null)
        {
            if (domains.Count > 0)
            {
                foreach (var dnsAddress in dnsAddresses)
                {
                    v2rayConfig.dns.servers.Add(CreateDnsServer(dnsAddress, domains, expectedIPs));
                }
            }
        }

        AddDnsServers(remoteDNSAddress, proxyDomainList);
        AddDnsServers(directDNSAddress, directDomainList);
        AddDnsServers(remoteDNSAddress, proxyGeositeList);
        AddDnsServers(directDNSAddress, directGeositeList);
        AddDnsServers(directDNSAddress, expectedDomainList, expectedIPs);

        var useDirectDns = rules?.LastOrDefault() is { } lastRule
            && lastRule.OutboundTag == Global.DirectTag
            && (lastRule.Port == "0-65535"
                || lastRule.Network == "tcp,udp"
                || lastRule.Ip?.Contains("0.0.0.0/0") == true);

        var defaultDnsServers = useDirectDns ? directDNSAddress : remoteDNSAddress;
        v2rayConfig.dns.servers.AddRange(defaultDnsServers);

        return 0;
    }

    private async Task<int> GenDnsHosts(V2rayConfig v2rayConfig, SimpleDNSItem simpleDNSItem)
    {
        if (simpleDNSItem.AddCommonHosts == false && simpleDNSItem.UseSystemHosts == false && simpleDNSItem.Hosts.IsNullOrEmpty())
        {
            return await Task.FromResult(0);
        }
        v2rayConfig.dns ??= new Dns4Ray();
        v2rayConfig.dns.hosts ??= new Dictionary<string, object>();
        if (simpleDNSItem.AddCommonHosts == true)
        {
            v2rayConfig.dns.hosts = Global.PredefinedHosts.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)kvp.Value
            );
        }

        if (simpleDNSItem.UseSystemHosts == true)
        {
            var systemHosts = Utils.GetSystemHosts();
            if (systemHosts.Count > 0)
            {
                var normalHost = v2rayConfig.dns.hosts;
                if (normalHost != null)
                {
                    foreach (var host in systemHosts)
                    {
                        if (normalHost[host.Key] != null)
                        {
                            continue;
                        }
                        normalHost[host.Key] = new List<string> { host.Value };
                    }
                }
            }
        }

        var userHostsMap = simpleDNSItem.Hosts?
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => line.Contains(' '))
            .ToDictionary(
                line =>
                {
                    var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    return parts[0];
                },
                line =>
                {
                    var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var values = parts.Skip(1).ToList();
                    return values;
                }
            );

        if (userHostsMap != null)
        {
            foreach (var kvp in userHostsMap)
            {
                v2rayConfig.dns.hosts[kvp.Key] = kvp.Value;
            }
        }
        return await Task.FromResult(0);
    }

    private async Task<int> GenDnsCompatible(ProfileItem? node, V2rayConfig v2rayConfig)
    {
        try
        {
            var item = await AppManager.Instance.GetDNSItem(ECoreType.Xray);
            var normalDNS = item?.NormalDNS;
            var domainStrategy4Freedom = item?.DomainStrategy4Freedom;
            if (normalDNS.IsNullOrEmpty())
            {
                normalDNS = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
            }

            //Outbound Freedom domainStrategy
            if (domainStrategy4Freedom.IsNotEmpty())
            {
                var outbound = v2rayConfig.outbounds.FirstOrDefault(t => t is { protocol: "freedom", tag: Global.DirectTag });
                if (outbound != null)
                {
                    outbound.settings = new();
                    outbound.settings.domainStrategy = domainStrategy4Freedom;
                    outbound.settings.userLevel = 0;
                }
            }

            var obj = JsonUtils.ParseJson(normalDNS);
            if (obj is null)
            {
                List<string> servers = [];
                string[] arrDNS = normalDNS.Split(',');
                foreach (string str in arrDNS)
                {
                    servers.Add(str);
                }
                obj = JsonUtils.ParseJson("{}");
                obj["servers"] = JsonUtils.SerializeToNode(servers);
            }

            // Append to dns settings
            if (item.UseSystemHosts)
            {
                var systemHosts = Utils.GetSystemHosts();
                if (systemHosts.Count > 0)
                {
                    var normalHost1 = obj["hosts"];
                    if (normalHost1 != null)
                    {
                        foreach (var host in systemHosts)
                        {
                            if (normalHost1[host.Key] != null)
                                continue;
                            normalHost1[host.Key] = host.Value;
                        }
                    }
                }
            }
            var normalHost = obj["hosts"];
            if (normalHost != null)
            {
                foreach (var hostProp in normalHost.AsObject().ToList())
                {
                    if (hostProp.Value is JsonValue value && value.TryGetValue<string>(out var ip))
                    {
                        normalHost[hostProp.Key] = new JsonArray(ip);
                    }
                }
            }

            await GenDnsDomainsCompatible(node, obj, item);

            v2rayConfig.dns = JsonUtils.Deserialize<Dns4Ray>(JsonUtils.Serialize(obj));
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsDomainsCompatible(ProfileItem? node, JsonNode dns, DNSItem? dNSItem)
    {
        if (node == null)
        {
            return 0;
        }
        var servers = dns["servers"];
        if (servers != null)
        {
            var domainList = new List<string>();
            if (Utils.IsDomain(node.Address))
            {
                domainList.Add(node.Address);
            }
            var subItem = await AppManager.Instance.GetSubItem(node.Subid);
            if (subItem is not null)
            {
                // Previous proxy
                var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
                if (prevNode is not null
                    && Global.SingboxSupportConfigType.Contains(prevNode.ConfigType)
                    && Utils.IsDomain(prevNode.Address))
                {
                    domainList.Add(prevNode.Address);
                }

                // Next proxy
                var nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
                if (nextNode is not null
                    && Global.SingboxSupportConfigType.Contains(nextNode.ConfigType)
                    && Utils.IsDomain(nextNode.Address))
                {
                    domainList.Add(nextNode.Address);
                }
            }
            if (domainList.Count > 0)
            {
                var dnsServer = new DnsServer4Ray()
                {
                    address = string.IsNullOrEmpty(dNSItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dNSItem?.DomainDNSAddress,
                    skipFallback = true,
                    domains = domainList
                };
                servers.AsArray().Add(JsonUtils.SerializeToNode(dnsServer));
            }
        }
        return await Task.FromResult(0);
    }
}
