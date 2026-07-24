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
            _coreConfig.dns.optimistic = context.SimpleDnsItem.ServeStale == true ? true : null;

            // final dns
            var useDirectDns = UseDirectDns();
            _coreConfig.dns.final = useDirectDns ? Global.SingboxDirectDNSTag : Global.SingboxRemoteDNSTag;
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

        var directDnsList = ParseDnsAddresses(simpleDnsItem.DirectDNS ?? Global.DomainDirectDNSAddress.First());
        for (var i = 0; i < directDnsList.Count; i++)
        {
            var directDns = directDnsList[i];
            var tag = string.Format(Global.SingboxDirectDNSTagTemplate, i + 1);
            directDns.tag = tag;
            directDns.domain_resolver = Global.SingboxLocalDNSTag;
        }

        var remoteDnsList = ParseDnsAddresses(simpleDnsItem.RemoteDNS ?? Global.DomainRemoteDNSAddress.First());
        for (var i = 0; i < remoteDnsList.Count; i++)
        {
            var remoteDns = remoteDnsList[i];
            var tag = string.Format(Global.SingboxRemoteDNSTagTemplate, i + 1);
            remoteDns.tag = tag;
            remoteDns.detour = Global.ProxyTag;
            remoteDns.domain_resolver = Global.SingboxLocalDNSTag;
        }

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
            if (systemHosts is { Count: > 0 })
            {
                foreach (var host in systemHosts)
                {
                    hostsDns.predefined.TryAdd(host.Key, new List<string> { host.Value });
                }
            }
        }

        foreach (var kvp in Utils.ParseHostsToDictionary(simpleDnsItem.Hosts))
        {
            // only allow full match
            // like example.com and full:example.com,
            // but not domain:example.com, keyword:example.com or regex:example.com etc.
            var testRule = new Rule4Sbox();
            if (!ParseV2Domain(kvp.Key, testRule))
            {
                continue;
            }
            if (testRule.domain_keyword?.Count > 0 && !kvp.Key.Contains(':'))
            {
                testRule.domain = testRule.domain_keyword;
                testRule.domain_keyword = null;
            }
            if (testRule.domain?.Count == 1)
            {
                hostsDns.predefined[testRule.domain.First()] = kvp.Value.Where(Utils.IsIpAddress).ToList();
            }
        }

        foreach (var host in hostsDns.predefined)
        {
            if (finalDns.server == host.Key)
            {
                finalDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
            foreach (var directDns in directDnsList.Where(directDns => directDns.server == host.Key))
            {
                directDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
            foreach (var remoteDns in remoteDnsList.Where(remoteDns => remoteDns.server == host.Key))
            {
                remoteDns.domain_resolver = Global.SingboxHostsDNSTag;
            }
        }

        _coreConfig.dns ??= new Dns4Sbox();
        _coreConfig.dns.servers ??= [];
        _coreConfig.dns.servers.AddRange(remoteDnsList);
        _coreConfig.dns.servers.AddRange(directDnsList);
        _coreConfig.dns.servers.Add(hostsDns);

        // fake ip
        if (simpleDnsItem.FakeIP == true)
        {
            var fakeipRange = simpleDnsItem.FakeIPRange.IsNullOrEmpty()
                ? Global.FakeIPRanges.First()
                : simpleDnsItem.FakeIPRange;
            var fakeip = new Server4Sbox
            {
                tag = Global.SingboxFakeDNSTag,
                type = "fakeip",
                inet4_range = fakeipRange,
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

        _coreConfig.dns.rules.Add(new()
        {
            preferred_by = Global.SingboxHostsDNSTag,
            server = Global.SingboxHostsDNSTag,
        });

        if (context.ProtectDomainList.Count > 0)
        {
            _coreConfig.dns.rules.Add(new()
            {
                server = Global.SingboxDirectDNSTag,
                domain = context.ProtectDomainList.ToList(),
            });
        }

        _coreConfig.dns.rules.AddRange([
            new Rule4Sbox
            {
                server = Global.SingboxRemoteDNSTag,
                clash_mode = nameof(ERuleMode.Global),
            },
            new Rule4Sbox
            {
                server = Global.SingboxDirectDNSTag,
                clash_mode = nameof(ERuleMode.Direct),
            },
        ]);

        foreach (var kvp in Utils.ParseHostsToDictionary(simpleDnsItem.Hosts))
        {
            var predefined = kvp.Value.First();
            if (predefined.IsNullOrEmpty())
            {
                continue;
            }
            var rule = new Rule4Sbox
            {
                query_type = [1, 5, 28], // A, CNAME and AAAA
                action = "predefined",
                rcode = "NOERROR",
            };
            if (!ParseV2Domain(kvp.Key, rule))
            {
                continue;
            }
            // see: https://xtls.github.io/en/config/dns.html#dnsobject
            // The matching format (domain:, full:, etc.) is the same as the domain
            // in the commonly used Routing System. The difference is that without a prefix,
            // it defaults to using the full: prefix (similar to the common hosts file syntax).
            if (rule.domain_keyword?.Count > 0 && !kvp.Key.Contains(':'))
            {
                rule.domain = rule.domain_keyword;
                rule.domain_keyword = null;
            }
            // example.com #0 -> example.com with NOERROR
            if (predefined.StartsWith('#') && int.TryParse(predefined.AsSpan(1), out var rcode))
            {
                rule.rcode = rcode switch
                {
                    0 => "NOERROR",
                    1 => "FORMERR",
                    2 => "SERVFAIL",
                    3 => "NXDOMAIN",
                    4 => "NOTIMP",
                    5 => "REFUSED",
                    _ => "NOERROR",
                };
            }
            else if (Utils.IsDomain(predefined))
            {
                // example.com CNAME target.com -> example.com with CNAME target.com
                rule.answer = new List<string> { $"*. IN CNAME {predefined}." };
            }
            else if (Utils.IsIpAddress(predefined) && (rule.domain?.Count ?? 0) == 0)
            {
                // not full match, but an IP address, treat it as predefined answer
                if (Utils.IsIpv6(predefined))
                {
                    rule.answer = new List<string> { $"*. IN AAAA {predefined}" };
                }
                else
                {
                    rule.answer = new List<string> { $"*. IN A {predefined}" };
                }
            }
            else
            {
                continue;
            }
            _coreConfig.dns.rules.Add(rule);
        }

        if (simpleDnsItem.BlockBindingQuery == true)
        {
            _coreConfig.dns.rules.Add(new()
            {
                query_type = [64, 65],
                action = "predefined",
                rcode = "NOERROR",
            });
        }

        if (simpleDnsItem.FakeIP == true && simpleDnsItem.GlobalFakeIp != false)
        {
            var fakeipFilterRule =
                JsonUtils.Deserialize<Rule4Sbox>(EmbedUtils.GetEmbedText(Global.SingboxFakeIPFilterFileName));
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
                    fakeipFilterRule,
                ],
            };

            _coreConfig.dns.rules.Add(rule4Fake);
        }

        var routing = context.RoutingItem;
        if (routing == null)
        {
            return;
        }

        var directDnsList = _coreConfig.dns.servers
            .Where(s => s.tag?.StartsWith(Global.SingboxDirectDNSTagPrefix) == true).ToList();
        var remoteDnsList = _coreConfig.dns.servers
            .Where(s => s.tag?.StartsWith(Global.SingboxRemoteDNSTagPrefix) == true).ToList();

        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];
        var expectedIPCidr = new List<string>();
        var expectedIPsRegions = new List<string>();
        var regionName = string.Empty;

        if (!string.IsNullOrEmpty(simpleDnsItem?.DirectExpectedIPs))
        {
            var ipItems = (Utils.String2List(simpleDnsItem.DirectExpectedIPs) ?? [])
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var ip in ipItems)
            {
                if (ip.StartsWith(Global.GeoIPPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var region = ip[Global.GeoIPPrefix.Length..];
                    if (string.IsNullOrEmpty(region))
                    {
                        continue;
                    }

                    expectedIPsRegions.Add(region);
                    regionName = region;
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
                Rule4Sbox? rule4ExpectedIPs = null;

                if (expectedIPsRegions.Count > 0 && rule.geosite?.Count > 0 && !regionName.IsNullOrEmpty())
                {
                    var regionGeosite = rule.geosite.Where(g =>
                        g.EndsWith($"-{regionName}", StringComparison.OrdinalIgnoreCase)
                        || g.EndsWith($"@{regionName}", StringComparison.OrdinalIgnoreCase)
                        || g == regionName).ToList();
                    if (regionGeosite.Count > 0)
                    {
                        rule.geosite.RemoveAll(regionGeosite.Contains);
                        rule4ExpectedIPs = JsonUtils.DeepCopy(rule);
                        rule4ExpectedIPs.geosite = regionGeosite;
                    }
                }

                if (rule.geosite?.Count > 0 || rule.domain?.Count > 0)
                {
                    AddRules(rule, item, directDnsList);
                }

                if (rule4ExpectedIPs is not null)
                {
                    var expectedRuleList = BuildMultiRules(rule4ExpectedIPs, item, directDnsList);
                    foreach (var expectedRule in
                             expectedRuleList.Where(expectedRule => expectedRule.action == "respond"))
                    {
                        if (expectedIPsRegions.Count > 0)
                        {
                            expectedRule.geoip = expectedIPsRegions;
                        }
                        if (expectedIPCidr.Count > 0)
                        {
                            expectedRule.ip_cidr = expectedIPCidr;
                        }
                    }
                    _coreConfig.dns.rules.AddRange(expectedRuleList);
                    var fallbackRemoteRuleList = BuildMultiRules(rule4ExpectedIPs, item, remoteDnsList);
                    if (expectedRuleList.LastOrDefault()?.race == true)
                    {
                        foreach (var fallbackRemoteRule in fallbackRemoteRuleList.Where(fallbackRemoteRule =>
                                     fallbackRemoteRule.action == "evaluate"))
                        {
                            fallbackRemoteRule.speculative = true;
                        }
                    }
                    _coreConfig.dns.rules.AddRange(fallbackRemoteRuleList);
                    // Avoid further rollbacks
                    //_coreConfig.dns.rules.Add(new()
                    //{
                    //    action = "predefined",
                    //    match_response = $"{remoteDnsList.Last().tag}-{item.Id}",
                    //    invert = true,
                    //    rcode = "NOERROR",
                    //});
                }
            }
            else if (item.OutboundTag == Global.BlockTag)
            {
                rule.action = "predefined";
                rule.rcode = "NXDOMAIN";
                _coreConfig.dns.rules.Add(rule);
            }
            else
            {
                if (simpleDnsItem.FakeIP == true && simpleDnsItem.GlobalFakeIp == false)
                {
                    var rule4Fake = JsonUtils.DeepCopy(rule);
                    rule4Fake.server = Global.SingboxFakeDNSTag;
                    rule4Fake.query_type = new List<int>
                    {
                        1,
                        28,
                    }; // A and AAAA
                    rule4Fake.rewrite_ttl = 1;
                    _coreConfig.dns.rules.Add(rule4Fake);
                }
                AddRules(rule, item, remoteDnsList);
            }
        }
        var useDirectDns = UseDirectDns();
        if ((!useDirectDns) && simpleDnsItem.FakeIP == true && simpleDnsItem.GlobalFakeIp == false)
        {
            _coreConfig.dns.rules.Add(new()
            {
                server = Global.SingboxFakeDNSTag,
                query_type = new List<int>
                {
                    1,
                    28,
                }, // A and AAAA
                rewrite_ttl = 1,
            });
        }
        var tempRuleItem = new RulesItem
        {
            Id = $"final-{Utils.GetGuid(false)}",
            Enabled = true,
        };
        AddRules(new(), tempRuleItem, useDirectDns ? directDnsList : remoteDnsList);
        return;

        static List<Rule4Sbox> BuildParallelRules(Rule4Sbox rule, RulesItem item, List<Server4Sbox> dnsList)
        {
            var evaluateRuleList = new List<Rule4Sbox>();
            var racingMatchingRuleList = new List<Rule4Sbox>();
            foreach (var dnsServer in dnsList)
            {
                var dnsServerTag = dnsServer.tag;
                var evaluateRule = JsonUtils.DeepCopy(rule);
                evaluateRule.action = "evaluate";
                // Maybe rule index is better than id? Not sure, but id is unique, so use id for now.
                var evaluateTag = $"{dnsServerTag}-{item.Id}";
                evaluateRule.tag = evaluateTag;
                evaluateRule.server = dnsServerTag;
                evaluateRuleList.Add(evaluateRule);
                var racingMatchingRule = new Rule4Sbox
                {
                    match_response = evaluateTag,
                    race = true,
                    action = "respond",
                };
                racingMatchingRuleList.Add(racingMatchingRule);
            }
            return [.. evaluateRuleList, .. racingMatchingRuleList];
        }

        static List<Rule4Sbox> BuildSerialRules(Rule4Sbox rule, RulesItem item, List<Server4Sbox> dnsList)
        {
            var ruleList = new List<Rule4Sbox>();
            foreach (var dnsServer in dnsList)
            {
                var dnsServerTag = dnsServer.tag;
                var evaluateRule = JsonUtils.DeepCopy(rule);
                evaluateRule.action = "evaluate";
                // Maybe rule index is better than id? Not sure, but id is unique, so use id for now.
                var evaluateTag = $"{dnsServerTag}-{item.Id}";
                evaluateRule.tag = evaluateTag;
                evaluateRule.server = dnsServerTag;
                ruleList.Add(evaluateRule);
                var racingMatchingRule = new Rule4Sbox
                {
                    match_response = evaluateTag,
                    action = "respond",
                };
                ruleList.Add(racingMatchingRule);
            }
            return ruleList;
        }

        List<Rule4Sbox> BuildMultiRules(Rule4Sbox rule, RulesItem item, List<Server4Sbox> dnsList)
        {
            if (simpleDnsItem.ParallelQuery == true)
            {
                return BuildParallelRules(rule, item, dnsList);
            }
            else
            {
                return BuildSerialRules(rule, item, dnsList);
            }
        }

        void AddRules(Rule4Sbox rule, RulesItem item, List<Server4Sbox> dnsList)
        {
            if (dnsList.Count == 1)
            {
                rule.server = dnsList.First().tag;
                _coreConfig.dns.rules.Add(rule);
            }
            else
            {
                var ruleList = BuildMultiRules(rule, item, dnsList);
                // Avoid further rollbacks
                //var lastEvaluateTag = $"{dnsList.Last().tag}-{item.Id}";
                //ruleList.Add(new()
                //{
                //    action = "predefined",
                //    match_response = lastEvaluateTag,
                //    invert = true,
                //    rcode = "NOERROR",
                //});
                _coreConfig.dns.rules.AddRange(ruleList);
            }
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
                strDNS = string.IsNullOrEmpty(item?.TunDNS)
                    ? EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName)
                    : item?.TunDNS;
            }
            else
            {
                strDNS = string.IsNullOrEmpty(item?.NormalDNS)
                    ? EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName)
                    : item?.NormalDNS;
            }

            var dns4Sbox = JsonUtils.Deserialize<Dns4Sbox>(strDNS);
            if (dns4Sbox is null)
            {
                return;
            }
            _coreConfig.dns = dns4Sbox;
            GenDnsProtectCustom();
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
            clash_mode = nameof(ERuleMode.Direct),
        });
        dns4Sbox.rules.Insert(0, new()
        {
            server = dns4Sbox.servers.Where(t => t.detour == Global.ProxyTag).Select(t => t.tag).FirstOrDefault() ??
                     "remote",
            clash_mode = nameof(ERuleMode.Global),
        });

        var finalDnsAddress = string.IsNullOrEmpty(dnsItem?.DomainDNSAddress)
            ? Global.DomainPureIPDNSAddress.FirstOrDefault()
            : dnsItem?.DomainDNSAddress;

        var localDnsServer = ParseDnsAddress(finalDnsAddress);
        localDnsServer.tag = tag;

        dns4Sbox.servers.Add(localDnsServer);
        var protectDomainRule = BuildProtectDomainRule();
        if (protectDomainRule != null)
        {
            dns4Sbox.rules.Insert(0, protectDomainRule);
        }

        _coreConfig.dns = dns4Sbox;
    }

    private Rule4Sbox? BuildProtectDomainRule()
    {
        if (context.ProtectDomainList.Count == 0)
        {
            return null;
        }
        return new()
        {
            server = Global.SingboxLocalDNSTag,
            domain = context.ProtectDomainList.ToList(),
        };
    }

    private static List<Server4Sbox> ParseDnsAddresses(string addresses)
    {
        var servers = new List<Server4Sbox>();
        var addressList = Utils.String2List(addresses)?.ToList();
        if (addressList is not { Count: > 0 })
        {
            return servers;
        }
        servers.AddRange(addressList.Select(ParseDnsAddress).Where(s => s != null));
        return servers;
    }

    private static Server4Sbox? ParseDnsAddress(string address)
    {
        var addressFirst = Utils.String2List(address)?.FirstOrDefault()?.Trim();
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
        if (server.type is "https" or "h3" && !string.IsNullOrEmpty(path) && path != "/")
        {
            server.path = path;
        }
        return server;
    }

    private bool UseDirectDns()
    {
        var routing = context.RoutingItem;
        var useDirectDns = false;
        if (routing == null)
        {
            return useDirectDns;
        }
        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];

        if (rules?.LastOrDefault() is not { OutboundTag: Global.DirectTag } lastRule)
        {
            return useDirectDns;
        }
        var noDomain = lastRule.Domain == null || lastRule.Domain.Count == 0;
        var noProcess = lastRule.Process == null || lastRule.Process.Count == 0;
        var isAnyIp = lastRule.Ip == null || lastRule.Ip.Count == 0 || lastRule.Ip.Contains("0.0.0.0/0");
        var isAnyPort = string.IsNullOrEmpty(lastRule.Port) || lastRule.Port == "0-65535";
        var isAnyNetwork = string.IsNullOrEmpty(lastRule.Network) || lastRule.Network == "tcp,udp";
        useDirectDns = noDomain && noProcess && isAnyIp && isAnyPort && isAnyNetwork;
        return useDirectDns;
    }
}
