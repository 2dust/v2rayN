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
            await GenDnsServers(node, singboxConfig, simpleDNSItem);
            await GenDnsRules(node, singboxConfig, simpleDNSItem);

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

            await GenOutboundDnsRule(node, singboxConfig);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsServers(ProfileItem? node, SingboxConfig singboxConfig, SimpleDNSItem simpleDNSItem)
    {
        var finalDns = await GenDnsDomains(singboxConfig, simpleDNSItem);

        var directDns = ParseDnsAddress(simpleDNSItem.DirectDNS);
        directDns.tag = Global.SingboxDirectDNSTag;
        directDns.domain_resolver = Global.SingboxLocalDNSTag;

        var remoteDns = ParseDnsAddress(simpleDNSItem.RemoteDNS);
        remoteDns.tag = Global.SingboxRemoteDNSTag;
        remoteDns.detour = Global.ProxyTag;
        remoteDns.domain_resolver = Global.SingboxLocalDNSTag;

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
            if (directDns.server == host.Key)
            {
                directDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
        }

        singboxConfig.dns ??= new Dns4Sbox();
        singboxConfig.dns.servers ??= new List<Server4Sbox>();
        singboxConfig.dns.servers.Add(remoteDns);
        singboxConfig.dns.servers.Add(directDns);
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

        // ech
        var (_, dnsServer) = ParseEchParam(node?.EchConfigList);
        if (dnsServer is not null)
        {
            dnsServer.tag = Global.SingboxEchDNSTag;
            if (dnsServer.server is not null
                && hostsDns.predefined.ContainsKey(dnsServer.server))
            {
                dnsServer.domain_resolver = Global.SingboxHostsDNSTag;
            }
            else
            {
                dnsServer.domain_resolver = Global.SingboxLocalDNSTag;
            }
            singboxConfig.dns.servers.Add(dnsServer);
        }
        else if (node?.ConfigType.IsGroupType() == true)
        {
            var echDnsObject = JsonUtils.DeepCopy(directDns);
            echDnsObject.tag = Global.SingboxEchDNSTag;
            singboxConfig.dns.servers.Add(echDnsObject);
        }

        return await Task.FromResult(0);
    }

    private async Task<Server4Sbox> GenDnsDomains(SingboxConfig singboxConfig, SimpleDNSItem? simpleDNSItem)
    {
        var finalDns = ParseDnsAddress(simpleDNSItem.BootstrapDNS);
        finalDns.tag = Global.SingboxLocalDNSTag;
        singboxConfig.dns ??= new Dns4Sbox();
        singboxConfig.dns.servers ??= new List<Server4Sbox>();
        singboxConfig.dns.servers.Add(finalDns);
        return await Task.FromResult(finalDns);
    }

    private async Task<int> GenDnsRules(ProfileItem? node, SingboxConfig singboxConfig, SimpleDNSItem simpleDNSItem)
    {
        singboxConfig.dns ??= new Dns4Sbox();
        singboxConfig.dns.rules ??= new List<Rule4Sbox>();

        singboxConfig.dns.rules.AddRange(new[]
            {
            new Rule4Sbox { ip_accept_any = true, server = Global.SingboxHostsDNSTag },
            new Rule4Sbox
            {
                server = Global.SingboxRemoteDNSTag,
                strategy = simpleDNSItem.SingboxStrategy4Proxy.NullIfEmpty(),
                clash_mode = ERuleMode.Global.ToString()
            },
            new Rule4Sbox
            {
                server = Global.SingboxDirectDNSTag,
                strategy = simpleDNSItem.SingboxStrategy4Direct.NullIfEmpty(),
                clash_mode = ERuleMode.Direct.ToString()
            }
        });

        var (ech, _) = ParseEchParam(node?.EchConfigList);
        if (ech is not null)
        {
            var echDomain = ech.query_server_name ?? node?.Sni;
            singboxConfig.dns.rules.Add(new()
            {
                query_type = new List<int> { 64, 65 },
                server = Global.SingboxEchDNSTag,
                domain = echDomain is not null ? new List<string> { echDomain } : null,
            });
        }
        else if (node?.ConfigType.IsGroupType() == true)
        {
            var queryServerNames = (await ProfileGroupItemManager.GetAllChildEchQuerySni(node.IndexId)).ToList();
            if (queryServerNames.Count > 0)
            {
                singboxConfig.dns.rules.Add(new()
                {
                    query_type = new List<int> { 64, 65 },
                    server = Global.SingboxEchDNSTag,
                    domain = queryServerNames,
                });
            }
        }

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
        {
            return 0;
        }

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

            await GenOutboundDnsRule(node, singboxConfig);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenDnsDomainsCompatible(SingboxConfig singboxConfig, DNSItem? dnsItem)
    {
        var dns4Sbox = singboxConfig.dns ?? new();
        dns4Sbox.servers ??= [];
        dns4Sbox.rules ??= [];

        var tag = Global.SingboxLocalDNSTag;

        var finalDnsAddress = string.IsNullOrEmpty(dnsItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dnsItem?.DomainDNSAddress;

        var localDnsServer = ParseDnsAddress(finalDnsAddress);
        localDnsServer.tag = tag;

        dns4Sbox.servers.Add(localDnsServer);

        singboxConfig.dns = dns4Sbox;
        return await Task.FromResult(0);
    }

    private async Task<int> GenDnsDomainsLegacyCompatible(SingboxConfig singboxConfig, DNSItem? dnsItem)
    {
        var dns4Sbox = singboxConfig.dns ?? new();
        dns4Sbox.servers ??= [];
        dns4Sbox.rules ??= [];

        var tag = Global.SingboxLocalDNSTag;
        dns4Sbox.servers.Add(new()
        {
            tag = tag,
            address = string.IsNullOrEmpty(dnsItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dnsItem?.DomainDNSAddress,
            detour = Global.DirectTag,
            strategy = string.IsNullOrEmpty(dnsItem?.DomainStrategy4Freedom) ? null : dnsItem?.DomainStrategy4Freedom,
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

    private async Task<int> GenOutboundDnsRule(ProfileItem? node, SingboxConfig singboxConfig)
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
            server = Global.SingboxLocalDNSTag,
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

        var (domain, scheme, port, path) = Utils.ParseUrl(addressFirst);

        if (scheme.Equals("dhcp", StringComparison.OrdinalIgnoreCase))
        {
            server.type = "dhcp";
            if ((!domain.IsNullOrEmpty()) && domain != "auto")
            {
                server.server = domain;
            }
            return server;
        }

        if (scheme.IsNullOrEmpty())
        {
            // udp dns
            server.type = "udp";
        }
        else
        {
            // server.type = scheme.ToLower();

            // remove "+local" suffix
            // TODO: "+local" suffix decide server.detour = "direct" ?
            server.type = scheme.Replace("+local", "", StringComparison.OrdinalIgnoreCase).ToLower();
        }

        server.server = domain;
        if (port != 0)
        {
            server.server_port = port;
        }
        if ((server.type == "https" || server.type == "h3") && !string.IsNullOrEmpty(path) && path != "/")
        {
            server.path = path;
        }
        return server;
    }
}
