namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<int> GenRouting(SingboxConfig singboxConfig)
    {
        try
        {
            singboxConfig.route.final = Global.ProxyTag;
            var item = _config.SimpleDNSItem;

            var defaultDomainResolverTag = Global.SingboxOutboundResolverTag;
            var directDNSStrategy = item.SingboxStrategy4Direct.IsNullOrEmpty() ? Global.SingboxDomainStrategy4Out.FirstOrDefault() : item.SingboxStrategy4Direct;

            var rawDNSItem = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
            if (rawDNSItem != null && rawDNSItem.Enabled == true)
            {
                defaultDomainResolverTag = Global.SingboxFinalResolverTag;
                directDNSStrategy = rawDNSItem.DomainStrategy4Freedom.IsNullOrEmpty() ? Global.SingboxDomainStrategy4Out.FirstOrDefault() : rawDNSItem.DomainStrategy4Freedom;
            }
            singboxConfig.route.default_domain_resolver = new()
            {
                server = defaultDomainResolverTag,
                strategy = directDNSStrategy
            };

            if (_config.TunModeItem.EnableTun)
            {
                singboxConfig.route.auto_detect_interface = true;

                var tunRules = JsonUtils.Deserialize<List<Rule4Sbox>>(EmbedUtils.GetEmbedText(Global.TunSingboxRulesFileName));
                if (tunRules != null)
                {
                    singboxConfig.route.rules.AddRange(tunRules);
                }

                GenRoutingDirectExe(out var lstDnsExe, out var lstDirectExe);
                singboxConfig.route.rules.Add(new()
                {
                    port = new() { 53 },
                    action = "hijack-dns",
                    process_name = lstDnsExe
                });

                singboxConfig.route.rules.Add(new()
                {
                    outbound = Global.DirectTag,
                    process_name = lstDirectExe
                });
            }

            if (_config.Inbound.First().SniffingEnabled)
            {
                singboxConfig.route.rules.Add(new()
                {
                    action = "sniff"
                });
                singboxConfig.route.rules.Add(new()
                {
                    protocol = new() { "dns" },
                    action = "hijack-dns"
                });
            }
            else
            {
                singboxConfig.route.rules.Add(new()
                {
                    port = new() { 53 },
                    network = new() { "udp" },
                    action = "hijack-dns"
                });
            }

            var hostsDomains = new List<string>();
            var dnsItem = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
            if (dnsItem == null || dnsItem.Enabled == false)
            {
                var simpleDNSItem = _config.SimpleDNSItem;
                if (!simpleDNSItem.Hosts.IsNullOrEmpty())
                {
                    var userHostsMap = Utils.ParseHostsToDictionary(simpleDNSItem.Hosts);
                    foreach (var kvp in userHostsMap)
                    {
                        hostsDomains.Add(kvp.Key);
                    }
                }
                if (simpleDNSItem.UseSystemHosts == true)
                {
                    var systemHostsMap = Utils.GetSystemHosts();
                    foreach (var kvp in systemHostsMap)
                    {
                        hostsDomains.Add(kvp.Key);
                    }
                }
            }
            if (hostsDomains.Count > 0)
            {
                singboxConfig.route.rules.Add(new()
                {
                    action = "resolve",
                    domain = hostsDomains,
                });
            }

            singboxConfig.route.rules.Add(new()
            {
                outbound = Global.DirectTag,
                clash_mode = ERuleMode.Direct.ToString()
            });
            singboxConfig.route.rules.Add(new()
            {
                outbound = Global.ProxyTag,
                clash_mode = ERuleMode.Global.ToString()
            });

            var domainStrategy = _config.RoutingBasicItem.DomainStrategy4Singbox.IsNullOrEmpty() ? null : _config.RoutingBasicItem.DomainStrategy4Singbox;
            var defaultRouting = await ConfigHandler.GetDefaultRouting(_config);
            if (defaultRouting.DomainStrategy4Singbox.IsNotEmpty())
            {
                domainStrategy = defaultRouting.DomainStrategy4Singbox;
            }
            var resolveRule = new Rule4Sbox
            {
                action = "resolve",
                strategy = domainStrategy
            };
            if (_config.RoutingBasicItem.DomainStrategy == Global.IPOnDemand)
            {
                singboxConfig.route.rules.Add(resolveRule);
            }

            var routing = await ConfigHandler.GetDefaultRouting(_config);
            var ipRules = new List<RulesItem>();
            if (routing != null)
            {
                var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
                foreach (var item1 in rules ?? [])
                {
                    if (!item1.Enabled)
                    {
                        continue;
                    }

                    if (item1.RuleType == ERuleType.DNS)
                    {
                        continue;
                    }

                    await GenRoutingUserRule(item1, singboxConfig);

                    if (item1.Ip?.Count > 0)
                    {
                        ipRules.Add(item1);
                    }
                }
            }
            if (_config.RoutingBasicItem.DomainStrategy == Global.IPIfNonMatch)
            {
                singboxConfig.route.rules.Add(resolveRule);
                foreach (var item2 in ipRules)
                {
                    await GenRoutingUserRule(item2, singboxConfig);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private void GenRoutingDirectExe(out List<string> lstDnsExe, out List<string> lstDirectExe)
    {
        var dnsExeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directExeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var coreInfoResult = CoreInfoManager.Instance.GetCoreInfo();

        foreach (var coreConfig in coreInfoResult)
        {
            if (coreConfig.CoreType == ECoreType.v2rayN)
            {
                continue;
            }

            foreach (var baseExeName in coreConfig.CoreExes)
            {
                if (coreConfig.CoreType != ECoreType.sing_box)
                {
                    dnsExeSet.Add(Utils.GetExeName(baseExeName));
                }
                directExeSet.Add(Utils.GetExeName(baseExeName));
            }
        }

        lstDnsExe = new List<string>(dnsExeSet);
        lstDirectExe = new List<string>(directExeSet);
    }

    private async Task<int> GenRoutingUserRule(RulesItem item, SingboxConfig singboxConfig)
    {
        try
        {
            if (item == null)
            {
                return 0;
            }
            item.OutboundTag = await GenRoutingUserRuleOutbound(item.OutboundTag, singboxConfig);
            var rules = singboxConfig.route.rules;

            var rule = new Rule4Sbox();
            if (item.OutboundTag == "block")
            {
                rule.action = "reject";
            }
            else
            {
                rule.outbound = item.OutboundTag;
            }

            if (item.Port.IsNotEmpty())
            {
                var portRanges = item.Port.Split(',').Where(it => it.Contains('-')).Select(it => it.Replace("-", ":")).ToList();
                var ports = item.Port.Split(',').Where(it => !it.Contains('-')).Select(it => it.ToInt()).ToList();

                rule.port_range = portRanges.Count > 0 ? portRanges : null;
                rule.port = ports.Count > 0 ? ports : null;
            }
            if (item.Network.IsNotEmpty())
            {
                rule.network = Utils.String2List(item.Network);
            }
            if (item.Protocol?.Count > 0)
            {
                rule.protocol = item.Protocol;
            }
            if (item.InboundTag?.Count >= 0)
            {
                rule.inbound = item.InboundTag;
            }
            var rule1 = JsonUtils.DeepCopy(rule);
            var rule2 = JsonUtils.DeepCopy(rule);
            var rule3 = JsonUtils.DeepCopy(rule);

            var hasDomainIp = false;
            if (item.Domain?.Count > 0)
            {
                var countDomain = 0;
                foreach (var it in item.Domain)
                {
                    if (ParseV2Domain(it, rule1))
                        countDomain++;
                }
                if (countDomain > 0)
                {
                    rules.Add(rule1);
                    hasDomainIp = true;
                }
            }

            if (item.Ip?.Count > 0)
            {
                var countIp = 0;
                foreach (var it in item.Ip)
                {
                    if (ParseV2Address(it, rule2))
                        countIp++;
                }
                if (countIp > 0)
                {
                    rules.Add(rule2);
                    hasDomainIp = true;
                }
            }

            if (_config.TunModeItem.EnableTun && item.Process?.Count > 0)
            {
                rule3.process_name = item.Process;
                rules.Add(rule3);
                hasDomainIp = true;
            }

            if (!hasDomainIp
                && (rule.port != null || rule.port_range != null || rule.protocol != null || rule.inbound != null || rule.network != null))
            {
                rules.Add(rule);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private bool ParseV2Domain(string domain, Rule4Sbox rule)
    {
        if (domain.StartsWith("#") || domain.StartsWith("ext:") || domain.StartsWith("ext-domain:"))
        {
            return false;
        }
        else if (domain.StartsWith("geosite:"))
        {
            rule.geosite ??= [];
            rule.geosite?.Add(domain.Substring(8));
        }
        else if (domain.StartsWith("regexp:"))
        {
            rule.domain_regex ??= [];
            rule.domain_regex?.Add(domain.Replace(Global.RoutingRuleComma, ",").Substring(7));
        }
        else if (domain.StartsWith("domain:"))
        {
            rule.domain ??= [];
            rule.domain_suffix ??= [];
            rule.domain?.Add(domain.Substring(7));
            rule.domain_suffix?.Add("." + domain.Substring(7));
        }
        else if (domain.StartsWith("full:"))
        {
            rule.domain ??= [];
            rule.domain?.Add(domain.Substring(5));
        }
        else if (domain.StartsWith("keyword:"))
        {
            rule.domain_keyword ??= [];
            rule.domain_keyword?.Add(domain.Substring(8));
        }
        else
        {
            rule.domain_keyword ??= [];
            rule.domain_keyword?.Add(domain);
        }
        return true;
    }

    private bool ParseV2Address(string address, Rule4Sbox rule)
    {
        if (address.StartsWith("ext:") || address.StartsWith("ext-ip:"))
        {
            return false;
        }
        else if (address.Equals("geoip:private"))
        {
            rule.ip_is_private = true;
        }
        else if (address.StartsWith("geoip:"))
        {
            rule.geoip ??= new();
            rule.geoip?.Add(address.Substring(6));
        }
        else if (address.Equals("geoip:!private"))
        {
            rule.ip_is_private = false;
        }
        else if (address.StartsWith("geoip:!"))
        {
            rule.geoip ??= new();
            rule.geoip?.Add(address.Substring(6));
            rule.invert = true;
        }
        else
        {
            rule.ip_cidr ??= new();
            rule.ip_cidr?.Add(address);
        }
        return true;
    }

    private async Task<string?> GenRoutingUserRuleOutbound(string outboundTag, SingboxConfig singboxConfig)
    {
        if (Global.OutboundTags.Contains(outboundTag))
        {
            return outboundTag;
        }

        var node = await AppManager.Instance.GetProfileItemViaRemarks(outboundTag);

        if (node == null
            || (!Global.SingboxSupportConfigType.Contains(node.ConfigType)
            && node.ConfigType is not (EConfigType.PolicyGroup or EConfigType.ProxyChain)))
        {
            return Global.ProxyTag;
        }

        var tag = $"{node.IndexId}-{Global.ProxyTag}";
        if (singboxConfig.outbounds.Any(o => o.tag == tag)
            || (singboxConfig.endpoints != null && singboxConfig.endpoints.Any(e => e.tag == tag)))
        {
            return tag;
        }

        if (node.ConfigType is EConfigType.PolicyGroup or EConfigType.ProxyChain)
        {
            var ret = await GenGroupOutbound(node, singboxConfig, tag);
            if (ret == 0)
            {
                return tag;
            }
            return Global.ProxyTag;
        }

        var server = await GenServer(node);
        if (server is null)
        {
            return Global.ProxyTag;
        }

        server.tag = tag;
        if (server is Endpoints4Sbox endpoint)
        {
            singboxConfig.endpoints ??= new();
            singboxConfig.endpoints.Add(endpoint);
        }
        else if (server is Outbound4Sbox outbound)
        {
            singboxConfig.outbounds.Add(outbound);
        }

        return server.tag;
    }
}
