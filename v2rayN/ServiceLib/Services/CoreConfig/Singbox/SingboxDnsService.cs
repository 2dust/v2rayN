namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<int> GenDns(ProfileItem? node, SingboxConfig singboxConfig)
    {
        try
        {
            var item = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
            if (item != null && item.Enabled == true)
            {
                return await GenDnsCompatible(node, singboxConfig);
            }

            var simpleDNSItem = _config.SimpleDNSItem;
            await GenDnsServers(singboxConfig, simpleDNSItem);
            await GenDnsRules(singboxConfig, simpleDNSItem);

            singboxConfig.dns ??= new Dns4Sbox();
            singboxConfig.dns.independent_cache = true;

            // final dns
            var routing = await ConfigHandler.GetDefaultRouting(_config);
            var useDirectDns = false;
            if (routing != null)
            {
                var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];

                useDirectDns = rules?.LastOrDefault() is { } lastRule &&
                                  lastRule.OutboundTag == Global.DirectTag &&
                                  (lastRule.Port == "0-65535" ||
                                   lastRule.Network == "tcp,udp" ||
                                   lastRule.Ip?.Contains("0.0.0.0/0") == true);
            }
            singboxConfig.dns.final = useDirectDns ? Global.SingboxDirectDNSTag : Global.SingboxRemoteDNSTag;
            if ((!useDirectDns) && simpleDNSItem.FakeIP == true && simpleDNSItem.GlobalFakeIp == false)
            {
                singboxConfig.dns.rules.Add(new()
                {
                    server = Global.SingboxFakeDNSTag,
                    query_type = new List<int> { 1, 28 }, // A and AAAA
                    rewrite_ttl = 1,
                });
            }

            //Outbound Freedom Resolver
            var freedomOutbound = singboxConfig.outbounds?.FirstOrDefault(t => t is { type: "direct", tag: Global.DirectTag });
            if (freedomOutbound != null)
            {
                freedomOutbound.domain_resolver = new()
                {
                    server = Global.SingboxDirectDNSTag,
                };
            }

            await GenOutboundDnsRule(node, singboxConfig, Global.SingboxOutboundResolverTag);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsServers(SingboxConfig singboxConfig, SimpleDNSItem simpleDNSItem)
    {
        var finalDns = await GenDnsDomains(singboxConfig, simpleDNSItem);

        var directDns = ParseDnsAddress(simpleDNSItem.DirectDNS);
        directDns.tag = Global.SingboxDirectDNSTag;
        directDns.domain_resolver = Global.SingboxFinalResolverTag;

        var remoteDns = ParseDnsAddress(simpleDNSItem.RemoteDNS);
        remoteDns.tag = Global.SingboxRemoteDNSTag;
        remoteDns.detour = Global.ProxyTag;
        remoteDns.domain_resolver = Global.SingboxFinalResolverTag;

        var resolverDns = ParseDnsAddress(simpleDNSItem.SingboxOutboundsResolveDNS);
        resolverDns.tag = Global.SingboxOutboundResolverTag;
        resolverDns.domain_resolver = Global.SingboxFinalResolverTag;

        var hostsDns = new Server4Sbox
        {
            tag = Global.SingboxHostsDNSTag,
            type = "hosts",
            predefined = new(),
        };
        if (simpleDNSItem.AddCommonHosts == true)
        {
            hostsDns.predefined = Global.PredefinedHosts;
        }

        if (simpleDNSItem.UseSystemHosts == true)
        {
            var systemHosts = Utils.GetSystemHosts();
            if (systemHosts != null && systemHosts.Count > 0)
            {
                foreach (var host in systemHosts)
                {
                    hostsDns.predefined.TryAdd(host.Key, new List<string> { host.Value });
                }
            }
        }

        if (!simpleDNSItem.Hosts.IsNullOrEmpty())
        {
            var userHostsMap = Utils.ParseHostsToDictionary(simpleDNSItem.Hosts);

            foreach (var kvp in userHostsMap)
            {
                hostsDns.predefined[kvp.Key] = kvp.Value;
            }
        }

        foreach (var host in hostsDns.predefined)
        {
            if (finalDns.server == host.Key)
            {
                finalDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
            if (remoteDns.server == host.Key)
            {
                remoteDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
            if (resolverDns.server == host.Key)
            {
                resolverDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
            if (directDns.server == host.Key)
            {
                directDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
        }

        singboxConfig.dns ??= new Dns4Sbox();
        singboxConfig.dns.servers ??= new List<Server4Sbox>();
        singboxConfig.dns.servers.Add(remoteDns);
        singboxConfig.dns.servers.Add(directDns);
        singboxConfig.dns.servers.Add(resolverDns);
        singboxConfig.dns.servers.Add(hostsDns);

        // fake ip
        if (simpleDNSItem.FakeIP == true)
        {
            var fakeip = new Server4Sbox
            {
                tag = Global.SingboxFakeDNSTag,
                type = "fakeip",
                inet4_range = "198.18.0.0/15",
                inet6_range = "fc00::/18",
            };
            singboxConfig.dns.servers.Add(fakeip);
        }

        return await Task.FromResult(0);
    }

    private async Task<Server4Sbox> GenDnsDomains(SingboxConfig singboxConfig, SimpleDNSItem? simpleDNSItem)
    {
        var finalDns = ParseDnsAddress(simpleDNSItem.SingboxFinalResolveDNS);
        finalDns.tag = Global.SingboxFinalResolverTag;
        singboxConfig.dns ??= new Dns4Sbox();
        singboxConfig.dns.servers ??= new List<Server4Sbox>();
        singboxConfig.dns.servers.Add(finalDns);
        return await Task.FromResult(finalDns);
    }

    private async Task<int> GenDnsRules(SingboxConfig singboxConfig, SimpleDNSItem simpleDNSItem)
    {
        singboxConfig.dns ??= new Dns4Sbox();
        singboxConfig.dns.rules ??= new List<Rule4Sbox>();

        singboxConfig.dns.rules.AddRange(new[]
            {
            new Rule4Sbox { ip_accept_any = true, server = Global.SingboxHostsDNSTag },
            new Rule4Sbox
            {
                server = Global.SingboxRemoteDNSTag,
                strategy = simpleDNSItem.SingboxStrategy4Proxy.IsNullOrEmpty() ? null : simpleDNSItem.SingboxStrategy4Proxy,
                clash_mode = ERuleMode.Global.ToString()
            },
            new Rule4Sbox
            {
                server = Global.SingboxDirectDNSTag,
                strategy = simpleDNSItem.SingboxStrategy4Direct.IsNullOrEmpty() ? null : simpleDNSItem.SingboxStrategy4Direct,
                clash_mode = ERuleMode.Direct.ToString()
            }
        });

        if (simpleDNSItem.BlockBindingQuery == true)
        {
            singboxConfig.dns.rules.Add(new()
            {
                query_type = new List<int> { 64, 65 },
                action = "predefined",
                rcode = "NOTIMP"
            });
        }

        if (simpleDNSItem.FakeIP == true && simpleDNSItem.GlobalFakeIp == true)
        {
            var fakeipFilterRule = JsonUtils.Deserialize<Rule4Sbox>(EmbedUtils.GetEmbedText(Global.SingboxFakeIPFilterFileName));
            fakeipFilterRule.invert = true;
            var rule4Fake = new Rule4Sbox
            {
                server = Global.SingboxFakeDNSTag,
                type = "logical",
                mode = "and",
                rewrite_ttl = 1,
                rules = new List<Rule4Sbox>
                {
                    new() {
                        query_type = new List<int> { 1, 28 }, // A and AAAA
                    },
                    fakeipFilterRule,
                }
            };

            singboxConfig.dns.rules.Add(rule4Fake);
        }

        var routing = await ConfigHandler.GetDefaultRouting(_config);
        if (routing == null)
            return 0;

        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];
        var expectedIPCidr = new List<string>();
        var expectedIPsRegions = new List<string>();
        var regionNames = new HashSet<string>();

        if (!string.IsNullOrEmpty(simpleDNSItem?.DirectExpectedIPs))
        {
            var ipItems = simpleDNSItem.DirectExpectedIPs
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var ip in ipItems)
            {
                if (ip.StartsWith("geoip:", StringComparison.OrdinalIgnoreCase))
                {
                    var region = ip["geoip:".Length..];
                    if (!string.IsNullOrEmpty(region))
                    {
                        expectedIPsRegions.Add(region);
                        regionNames.Add(region);
                        regionNames.Add($"geolocation-{region}");
                        regionNames.Add($"tld-{region}");
                    }
                }
                else
                {
                    expectedIPCidr.Add(ip);
                }
            }
        }

        foreach (var item in rules)
        {
            if (!item.Enabled || item.Domain is null || item.Domain.Count == 0)
            {
                continue;
            }

            if (item.RuleType == ERuleType.Routing)
            {
                continue;
            }

            var rule = new Rule4Sbox();
            var validDomains = item.Domain.Count(it => ParseV2Domain(it, rule));
            if (validDomains <= 0)
            {
                continue;
            }

            if (item.OutboundTag == Global.DirectTag)
            {
                rule.server = Global.SingboxDirectDNSTag;
                rule.strategy = string.IsNullOrEmpty(simpleDNSItem.SingboxStrategy4Direct) ? null : simpleDNSItem.SingboxStrategy4Direct;

                if (expectedIPsRegions.Count > 0 && rule.geosite?.Count > 0)
                {
                    var geositeSet = new HashSet<string>(rule.geosite);
                    if (regionNames.Intersect(geositeSet).Any())
                    {
                        if (expectedIPsRegions.Count > 0)
                        {
                            rule.geoip = expectedIPsRegions;
                        }
                        if (expectedIPCidr.Count > 0)
                        {
                            rule.ip_cidr = expectedIPCidr;
                        }
                    }
                }
            }
            else if (item.OutboundTag == Global.BlockTag)
            {
                rule.action = "predefined";
                rule.rcode = "NXDOMAIN";
            }
            else
            {
                if (simpleDNSItem.FakeIP == true && simpleDNSItem.GlobalFakeIp == false)
                {
                    var rule4Fake = JsonUtils.DeepCopy(rule);
                    rule4Fake.server = Global.SingboxFakeDNSTag;
                    rule4Fake.query_type = new List<int> { 1, 28 }; // A and AAAA
                    rule4Fake.rewrite_ttl = 1;
                    singboxConfig.dns.rules.Add(rule4Fake);
                }
                rule.server = Global.SingboxRemoteDNSTag;
                rule.strategy = string.IsNullOrEmpty(simpleDNSItem.SingboxStrategy4Proxy) ? null : simpleDNSItem.SingboxStrategy4Proxy;
            }

            singboxConfig.dns.rules.Add(rule);
        }

        return 0;
    }

    private async Task<int> GenDnsCompatible(ProfileItem? node, SingboxConfig singboxConfig)
    {
        try
        {
            var item = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
            var strDNS = string.Empty;
            if (_config.TunModeItem.EnableTun)
            {
                strDNS = string.IsNullOrEmpty(item?.TunDNS) ? EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName) : item?.TunDNS;
            }
            else
            {
                strDNS = string.IsNullOrEmpty(item?.NormalDNS) ? EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName) : item?.NormalDNS;
            }

            var dns4Sbox = JsonUtils.Deserialize<Dns4Sbox>(strDNS);
            if (dns4Sbox is null)
            {
                return 0;
            }
            singboxConfig.dns = dns4Sbox;

            if (dns4Sbox.servers != null && dns4Sbox.servers.Count > 0 && dns4Sbox.servers.First().address.IsNullOrEmpty())
            {
                await GenDnsDomainsCompatible(singboxConfig, item);
            }
            else
            {
                await GenDnsDomainsLegacyCompatible(singboxConfig, item);
            }

            await GenOutboundDnsRule(node, singboxConfig, Global.SingboxFinalResolverTag);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsDomainsCompatible(SingboxConfig singboxConfig, DNSItem? dNSItem)
    {
        var dns4Sbox = singboxConfig.dns ?? new();
        dns4Sbox.servers ??= [];
        dns4Sbox.rules ??= [];

        var tag = Global.SingboxFinalResolverTag;
        var localDnsAddress = string.IsNullOrEmpty(dNSItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dNSItem?.DomainDNSAddress;

        var localDnsServer = ParseDnsAddress(localDnsAddress);
        localDnsServer.tag = tag;

        dns4Sbox.servers.Add(localDnsServer);

        singboxConfig.dns = dns4Sbox;
        return await Task.FromResult(0);
    }

    private async Task<int> GenDnsDomainsLegacyCompatible(SingboxConfig singboxConfig, DNSItem? dNSItem)
    {
        var dns4Sbox = singboxConfig.dns ?? new();
        dns4Sbox.servers ??= [];
        dns4Sbox.rules ??= [];

        var tag = Global.SingboxFinalResolverTag;
        dns4Sbox.servers.Add(new()
        {
            tag = tag,
            address = string.IsNullOrEmpty(dNSItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dNSItem?.DomainDNSAddress,
            detour = Global.DirectTag,
            strategy = string.IsNullOrEmpty(dNSItem?.DomainStrategy4Freedom) ? null : dNSItem?.DomainStrategy4Freedom,
        });
        dns4Sbox.rules.Insert(0, new()
        {
            server = tag,
            clash_mode = ERuleMode.Direct.ToString()
        });
        dns4Sbox.rules.Insert(0, new()
        {
            server = dns4Sbox.servers.Where(t => t.detour == Global.ProxyTag).Select(t => t.tag).FirstOrDefault() ?? "remote",
            clash_mode = ERuleMode.Global.ToString()
        });

        var lstDomain = singboxConfig.outbounds
                       .Where(t => t.server.IsNotEmpty() && Utils.IsDomain(t.server))
                       .Select(t => t.server)
                       .Distinct()
                       .ToList();
        if (lstDomain != null && lstDomain.Count > 0)
        {
            dns4Sbox.rules.Insert(0, new()
            {
                server = tag,
                domain = lstDomain
            });
        }

        singboxConfig.dns = dns4Sbox;
        return await Task.FromResult(0);
    }

    private async Task<int> GenOutboundDnsRule(ProfileItem? node, SingboxConfig singboxConfig, string? server)
    {
        if (node == null)
        {
            return 0;
        }

        List<string> domain = new();
        if (Utils.IsDomain(node.Address)) // normal outbound
        {
            domain.Add(node.Address);
        }
        if (node.Address == Global.Loopback && node.SpiderX.IsNotEmpty()) // Tun2SocksAddress
        {
            domain.AddRange(Utils.String2List(node.SpiderX)
                .Where(Utils.IsDomain)
                .Distinct()
                .ToList());
        }
        if (domain.Count == 0)
        {
            return 0;
        }

        singboxConfig.dns.rules ??= new List<Rule4Sbox>();
        singboxConfig.dns.rules.Insert(0, new Rule4Sbox
        {
            server = server,
            domain = domain,
        });

        return await Task.FromResult(0);
    }

    private static Server4Sbox? ParseDnsAddress(string address)
    {
        var addressFirst = address?.Split(address.Contains(',') ? ',' : ';').FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(addressFirst))
        {
            return null;
        }

        var server = new Server4Sbox();

        if (addressFirst is "local" or "localhost")
        {
            server.type = "local";
            return server;
        }

        if (addressFirst.StartsWith("dhcp://", StringComparison.OrdinalIgnoreCase))
        {
            var interface_name = addressFirst.Substring(7);
            server.type = "dhcp";
            server.Interface = interface_name == "auto" ? null : interface_name;
            return server;
        }

        if (!addressFirst.Contains("://"))
        {
            // udp dns
            server.type = "udp";
            server.server = addressFirst;
            return server;
        }

        try
        {
            var protocolEndIndex = addressFirst.IndexOf("://", StringComparison.Ordinal);
            server.type = addressFirst.Substring(0, protocolEndIndex).ToLower();

            var uri = new Uri(addressFirst);
            server.server = uri.Host;

            if (!uri.IsDefaultPort)
            {
                server.server_port = uri.Port;
            }

            if ((server.type == "https" || server.type == "h3") && !string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            {
                server.path = uri.AbsolutePath;
            }
        }
        catch (UriFormatException)
        {
            var protocolEndIndex = addressFirst.IndexOf("://", StringComparison.Ordinal);
            if (protocolEndIndex > 0)
            {
                server.type = addressFirst.Substring(0, protocolEndIndex).ToLower();
                var remaining = addressFirst.Substring(protocolEndIndex + 3);

                var portIndex = remaining.IndexOf(':');
                var pathIndex = remaining.IndexOf('/');

                if (portIndex > 0)
                {
                    server.server = remaining.Substring(0, portIndex);
                    var portPart = pathIndex > portIndex
                        ? remaining.Substring(portIndex + 1, pathIndex - portIndex - 1)
                        : remaining.Substring(portIndex + 1);

                    if (int.TryParse(portPart, out var parsedPort))
                    {
                        server.server_port = parsedPort;
                    }
                }
                else if (pathIndex > 0)
                {
                    server.server = remaining.Substring(0, pathIndex);
                }
                else
                {
                    server.server = remaining;
                }

                if (pathIndex > 0 && (server.type == "https" || server.type == "h3"))
                {
                    server.path = remaining.Substring(pathIndex);
                }
            }
        }

        return server;
    }
}
