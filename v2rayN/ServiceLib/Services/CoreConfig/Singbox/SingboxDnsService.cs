namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private void GenDns()
    {
        try
        {
            var item = context.RawDnsItem;
            if (item is { Enabled: true })
            {
                GenDnsCustom();
                return;
            }

            GenDnsServers();
            GenDnsRules();

            _coreConfig.dns ??= new Dns4Sbox();
            _coreConfig.dns.independent_cache = true;

            // final dns
            var routing = context.RoutingItem;
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
            _coreConfig.dns.final = useDirectDns ? Global.SingboxDirectDNSTag : Global.SingboxRemoteDNSTag;
            var simpleDnsItem = context.SimpleDnsItem;
            if ((!useDirectDns) && simpleDnsItem.FakeIP == true && simpleDnsItem.GlobalFakeIp == false)
            {
                _coreConfig.dns.rules.Add(new()
                {
                    server = Global.SingboxFakeDNSTag,
                    query_type = new List<int> { 1, 28 }, // A and AAAA
                    rewrite_ttl = 1,
                });
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void GenDnsServers()
    {
        var simpleDnsItem = context.SimpleDnsItem;
        var finalDns = GenBootstrapDns();

        var directDns = ParseDnsAddress(simpleDnsItem.DirectDNS ?? Global.DomainDirectDNSAddress.First());
        directDns.tag = Global.SingboxDirectDNSTag;
        directDns.domain_resolver = Global.SingboxLocalDNSTag;

        var remoteDns = ParseDnsAddress(simpleDnsItem.RemoteDNS ?? Global.DomainRemoteDNSAddress.First());
        remoteDns.tag = Global.SingboxRemoteDNSTag;
        remoteDns.detour = Global.ProxyTag;
        remoteDns.domain_resolver = Global.SingboxLocalDNSTag;

        var hostsDns = new Server4Sbox
        {
            tag = Global.SingboxHostsDNSTag,
            type = "hosts",
            predefined = new(),
        };
        if (simpleDnsItem.AddCommonHosts == true)
        {
            hostsDns.predefined = Global.PredefinedHosts;
        }

        if (simpleDnsItem.UseSystemHosts == true)
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

        foreach (var kvp in Utils.ParseHostsToDictionary(simpleDnsItem.Hosts))
        {
            hostsDns.predefined[kvp.Key] = kvp.Value.Where(Utils.IsIpAddress).ToList();
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

        _coreConfig.dns ??= new Dns4Sbox();
        _coreConfig.dns.servers ??= [];
        _coreConfig.dns.servers.Add(remoteDns);
        _coreConfig.dns.servers.Add(directDns);
        _coreConfig.dns.servers.Add(hostsDns);

        // fake ip
        if (simpleDnsItem.FakeIP == true)
        {
            var fakeip = new Server4Sbox
            {
                tag = Global.SingboxFakeDNSTag,
                type = "fakeip",
                inet4_range = "198.18.0.0/15",
                inet6_range = "fc00::/18",
            };
            _coreConfig.dns.servers.Add(fakeip);
        }
    }

    private Server4Sbox GenBootstrapDns()
    {
        var finalDns = ParseDnsAddress(context.SimpleDnsItem?.BootstrapDNS ?? Global.DomainPureIPDNSAddress.First());
        finalDns.tag = Global.SingboxLocalDNSTag;
        _coreConfig.dns ??= new Dns4Sbox();
        _coreConfig.dns.servers ??= [];
        _coreConfig.dns.servers.Add(finalDns);
        return finalDns;
    }

    private void GenDnsRules()
    {
        var simpleDnsItem = context.SimpleDnsItem;
        _coreConfig.dns ??= new Dns4Sbox();
        _coreConfig.dns.rules ??= [];

        _coreConfig.dns.rules.AddRange(new[]
        {
            new Rule4Sbox { ip_accept_any = true, server = Global.SingboxHostsDNSTag },
            new Rule4Sbox
            {
                server = Global.SingboxDirectDNSTag,
                strategy = Utils.DomainStrategy4Sbox(simpleDnsItem.Strategy4Freedom),
                domain = context.ProtectDomainList.ToList(),
            },
            new Rule4Sbox
            {
                server = Global.SingboxRemoteDNSTag,
                strategy = Utils.DomainStrategy4Sbox(simpleDnsItem.Strategy4Proxy),
                clash_mode = ERuleMode.Global.ToString()
            },
            new Rule4Sbox
            {
                server = Global.SingboxDirectDNSTag,
                strategy = Utils.DomainStrategy4Sbox(simpleDnsItem.Strategy4Freedom),
                clash_mode = ERuleMode.Direct.ToString()
            }
        });

        foreach (var kvp in Utils.ParseHostsToDictionary(simpleDnsItem.Hosts))
        {
            var predefined = kvp.Value.First();
            if (predefined.IsNullOrEmpty() || Utils.IsIpAddress(predefined))
            {
                continue;
            }
            if (predefined.StartsWith('#') && int.TryParse(predefined.AsSpan(1), out var rcode))
            {
                // xray syntactic sugar for predefined
                // etc. #0 -> NOERROR
                _coreConfig.dns.rules.Add(new()
                {
                    query_type = [1, 28],
                    domain = [kvp.Key],
                    action = "predefined",
                    rcode = rcode switch
                    {
                        0 => "NOERROR",
                        1 => "FORMERR",
                        2 => "SERVFAIL",
                        3 => "NXDOMAIN",
                        4 => "NOTIMP",
                        5 => "REFUSED",
                        _ => "NOERROR",
                    },
                });
                continue;
            }
            // CNAME record
            Rule4Sbox rule = new()
            {
                query_type = [1, 28],
                action = "predefined",
                rcode = "NOERROR",
                answer = [$"*. IN CNAME {predefined}."],
            };
            if (ParseV2Domain(kvp.Key, rule))
            {
                _coreConfig.dns.rules.Add(rule);
            }
        }

        if (simpleDnsItem.BlockBindingQuery == true)
        {
            _coreConfig.dns.rules.Add(new()
            {
                query_type = [64, 65],
                action = "predefined",
                rcode = "NOERROR"
            });
        }

        if (simpleDnsItem.FakeIP == true && simpleDnsItem.GlobalFakeIp == true)
        {
            var fakeipFilterRule = JsonUtils.Deserialize<Rule4Sbox>(EmbedUtils.GetEmbedText(Global.SingboxFakeIPFilterFileName));
            fakeipFilterRule.invert = true;
            var rule4Fake = new Rule4Sbox
            {
                server = Global.SingboxFakeDNSTag,
                type = "logical",
                mode = "and",
                rewrite_ttl = 1,
                rules =
                [
                    new()
                    {
                        query_type = [1, 28], // A and AAAA
                    },
                    fakeipFilterRule
                ]
            };

            _coreConfig.dns.rules.Add(rule4Fake);
        }

        var routing = context.RoutingItem;
        if (routing == null)
        {
            return;
        }

        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];
        var expectedIPCidr = new List<string>();
        var expectedIPsRegions = new List<string>();
        var regionNames = new HashSet<string>();

        if (!string.IsNullOrEmpty(simpleDnsItem?.DirectExpectedIPs))
        {
            var ipItems = simpleDnsItem.DirectExpectedIPs
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
                rule.strategy = Utils.DomainStrategy4Sbox(simpleDnsItem.Strategy4Freedom);

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
                if (simpleDnsItem.FakeIP == true && simpleDnsItem.GlobalFakeIp == false)
                {
                    var rule4Fake = JsonUtils.DeepCopy(rule);
                    rule4Fake.server = Global.SingboxFakeDNSTag;
                    rule4Fake.query_type = new List<int> { 1, 28 }; // A and AAAA
                    rule4Fake.rewrite_ttl = 1;
                    _coreConfig.dns.rules.Add(rule4Fake);
                }
                rule.server = Global.SingboxRemoteDNSTag;
                rule.strategy = Utils.DomainStrategy4Sbox(simpleDnsItem.Strategy4Proxy);
            }

            _coreConfig.dns.rules.Add(rule);
        }
    }

    private void GenMinimizedDns()
    {
        GenDnsServers();
        foreach (var server in _coreConfig.dns!.servers.Where(s => !string.IsNullOrEmpty(s.detour)).ToList())
        {
            _coreConfig.dns.servers.Remove(server);
        }
        _coreConfig.dns ??= new();
        _coreConfig.dns.rules ??= [];
        _coreConfig.dns.rules.Clear();
        _coreConfig.dns.final = Global.SingboxDirectDNSTag;
        _coreConfig.route.default_domain_resolver = new()
        {
            server = Global.SingboxDirectDNSTag,
        };
    }

    private void GenDnsCustom()
    {
        try
        {
            var item = context.RawDnsItem;
            var strDNS = string.Empty;
            if (context.IsTunEnabled)
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
                return;
            }
            _coreConfig.dns = dns4Sbox;
            if (dns4Sbox.servers?.Count > 0 &&
                dns4Sbox.servers.First().address.IsNullOrEmpty())
            {
                GenDnsProtectCustom();
            }
            else
            {
                GenDnsProtectCustomLegacy();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void GenDnsProtectCustom()
    {
        var dnsItem = context.RawDnsItem;
        var dns4Sbox = _coreConfig.dns ?? new();
        dns4Sbox.servers ??= [];
        dns4Sbox.rules ??= [];

        var tag = Global.SingboxLocalDNSTag;
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

        var finalDnsAddress = string.IsNullOrEmpty(dnsItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dnsItem?.DomainDNSAddress;

        var localDnsServer = ParseDnsAddress(finalDnsAddress);
        localDnsServer.tag = tag;

        dns4Sbox.servers.Add(localDnsServer);
        dns4Sbox.rules.Insert(0, BuildProtectDomainRule());

        _coreConfig.dns = dns4Sbox;
    }

    private void GenDnsProtectCustomLegacy()
    {
        GenDnsProtectCustom();

        var localDnsServer = _coreConfig.dns?.servers?.FirstOrDefault(s => s.tag == Global.SingboxLocalDNSTag);
        if (localDnsServer == null)
        {
            return;
        }
        localDnsServer.type = null;
        localDnsServer.server = null;
        var dnsItem = context.RawDnsItem;
        localDnsServer.address = string.IsNullOrEmpty(dnsItem?.DomainDNSAddress) ? Global.DomainPureIPDNSAddress.FirstOrDefault() : dnsItem?.DomainDNSAddress;
    }

    private Rule4Sbox BuildProtectDomainRule()
    {
        return new()
        {
            server = Global.SingboxLocalDNSTag,
            domain = context.ProtectDomainList.ToList(),
        };
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
