namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private void GenRouting()
    {
        try
        {
            _coreConfig.route.final = Global.ProxyTag;
            var simpleDnsItem = context.SimpleDnsItem;

            var defaultDomainResolverTag = Global.SingboxDirectDNSTag;
            var directDnsStrategy = Utils.DomainStrategy4Sbox(simpleDnsItem.Strategy4Freedom);

            var rawDNSItem = context.RawDnsItem;
            if (rawDNSItem is { Enabled: true })
            {
                defaultDomainResolverTag = Global.SingboxLocalDNSTag;
                directDnsStrategy = rawDNSItem.DomainStrategy4Freedom.IsNullOrEmpty() ? null : rawDNSItem.DomainStrategy4Freedom;
            }
            _coreConfig.route.default_domain_resolver = new()
            {
                server = defaultDomainResolverTag,
                strategy = directDnsStrategy
            };

            if (context.IsTunEnabled)
            {
                _coreConfig.route.auto_detect_interface = true;

                var tunRules = JsonUtils.Deserialize<List<Rule4Sbox>>(EmbedUtils.GetEmbedText(Global.TunSingboxRulesFileName));
                if (tunRules != null)
                {
                    _coreConfig.route.rules.AddRange(tunRules);
                }

                var (lstDnsExe, lstDirectExe) = BuildRoutingDirectExe();
                _coreConfig.route.rules.Add(new()
                {
                    port = [53],
                    action = "hijack-dns",
                    process_name = lstDnsExe
                });

                _coreConfig.route.rules.Add(new()
                {
                    outbound = Global.DirectTag,
                    process_name = lstDirectExe
                });

                // ICMP Routing
                var icmpRouting = _config.TunModeItem.IcmpRouting ?? "";
                if (!Global.TunIcmpRoutingPolicies.Contains(icmpRouting))
                {
                    icmpRouting = Global.TunIcmpRoutingPolicies.First();
                }
                if (icmpRouting == "direct")
                {
                    _coreConfig.route.rules.Add(new()
                    {
                        network = ["icmp"],
                        outbound = Global.DirectTag,
                    });
                }
                else if (icmpRouting != "rule")
                {
                    var rejectMethod = icmpRouting switch
                    {
                        "unreachable" => "default",
                        "drop" => "drop",
                        _ => "reply",
                    };
                    _coreConfig.route.rules.Add(new()
                    {
                        network = ["icmp"],
                        action = "reject",
                        method = rejectMethod,
                    });
                }
            }

            if (_config.Inbound.First().SniffingEnabled)
            {
                _coreConfig.route.rules.Add(new()
                {
                    action = "sniff"
                });
                _coreConfig.route.rules.Add(new()
                {
                    type = "logical",
                    mode = "or",
                    action = "hijack-dns",
                    rules =
                    [
                        new() { port = [53] },
                        new() { protocol = ["dns"] },
                    ],
                });
            }
            else
            {
                _coreConfig.route.rules.Add(new()
                {
                    port = [53],
                    action = "hijack-dns",
                });
            }

            var hostsDomains = new List<string>();
            if (rawDNSItem is not { Enabled: true })
            {
                var userHostsMap = Utils.ParseHostsToDictionary(simpleDnsItem.Hosts);
                hostsDomains.AddRange(userHostsMap.Select(kvp => kvp.Key));
                if (simpleDnsItem.UseSystemHosts == true)
                {
                    var systemHostsMap = Utils.GetSystemHosts();
                    hostsDomains.AddRange(systemHostsMap.Select(kvp => kvp.Key));
                }
            }
            if (hostsDomains.Count > 0)
            {
                var hostsResolveRule = new Rule4Sbox
                {
                    action = "resolve",
                };
                var hostsCounter = 0;
                foreach (var host in hostsDomains)
                {
                    var domainRule = new Rule4Sbox();
                    if (!ParseV2Domain(host, domainRule))
                    {
                        continue;
                    }
                    if (domainRule.domain_keyword?.Count > 0 && !host.Contains(':'))
                    {
                        domainRule.domain = domainRule.domain_keyword;
                        domainRule.domain_keyword = null;
                    }
                    if (domainRule.domain?.Count > 0)
                    {
                        hostsResolveRule.domain ??= [];
                        hostsResolveRule.domain.AddRange(domainRule.domain);
                        hostsCounter++;
                    }
                    else if (domainRule.domain_keyword?.Count > 0)
                    {
                        hostsResolveRule.domain_keyword ??= [];
                        hostsResolveRule.domain_keyword.AddRange(domainRule.domain_keyword);
                        hostsCounter++;
                    }
                    else if (domainRule.domain_suffix?.Count > 0)
                    {
                        hostsResolveRule.domain_suffix ??= [];
                        hostsResolveRule.domain_suffix.AddRange(domainRule.domain_suffix);
                        hostsCounter++;
                    }
                    else if (domainRule.domain_regex?.Count > 0)
                    {
                        hostsResolveRule.domain_regex ??= [];
                        hostsResolveRule.domain_regex.AddRange(domainRule.domain_regex);
                        hostsCounter++;
                    }
                    else if (domainRule.geosite?.Count > 0)
                    {
                        hostsResolveRule.geosite ??= [];
                        hostsResolveRule.geosite.AddRange(domainRule.geosite);
                        hostsCounter++;
                    }
                }
                if (hostsCounter > 0)
                {
                    _coreConfig.route.rules.Add(hostsResolveRule);
                }
            }

            _coreConfig.route.rules.Add(new()
            {
                outbound = Global.DirectTag,
                clash_mode = ERuleMode.Direct.ToString()
            });
            _coreConfig.route.rules.Add(new()
            {
                outbound = Global.ProxyTag,
                clash_mode = ERuleMode.Global.ToString()
            });

            var domainStrategy = _config.RoutingBasicItem.DomainStrategy4Singbox.NullIfEmpty();
            var routing = context.RoutingItem;
            if (routing.DomainStrategy4Singbox.IsNotEmpty())
            {
                domainStrategy = routing.DomainStrategy4Singbox;
            }
            var resolveRule = new Rule4Sbox
            {
                action = "resolve",
                strategy = domainStrategy
            };
            if (_config.RoutingBasicItem.DomainStrategy == Global.IPOnDemand)
            {
                _coreConfig.route.rules.Add(resolveRule);
            }

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

                    GenRoutingUserRule(item1);

                    if (item1.Ip?.Count > 0)
                    {
                        ipRules.Add(item1);
                    }
                }
            }
            if (_config.RoutingBasicItem.DomainStrategy == Global.IPIfNonMatch)
            {
                _coreConfig.route.rules.Add(resolveRule);
                foreach (var item2 in ipRules)
                {
                    GenRoutingUserRule(item2);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private static (List<string> lstDnsExe, List<string> lstDirectExe) BuildRoutingDirectExe()
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

        var lstDnsExe = new List<string>(dnsExeSet);
        var lstDirectExe = new List<string>(directExeSet);

        return (lstDnsExe, lstDirectExe);
    }

    private void GenRoutingUserRule(RulesItem? item)
    {
        try
        {
            if (item == null)
            {
                return;
            }
            item.OutboundTag = GenRoutingUserRuleOutbound(item.OutboundTag ?? Global.ProxyTag);
            var rules = _coreConfig.route.rules;

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
                    {
                        countDomain++;
                    }
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
                var negativeIpList = item.Ip.Where(it => it.StartsWith('!')).ToList();
                if (negativeIpList.Count > 0)
                {
                    var positiveIpList = item.Ip.Except(negativeIpList).ToList();
                    var positiveRule = rule2;
                    positiveRule = JsonUtils.DeepCopy(rule2);
                    positiveRule.outbound = null;
                    positiveRule.action = null;
                    foreach (var it in positiveIpList)
                    {
                        if (ParseV2Address(it, positiveRule))
                        {
                            countIp++;
                        }
                    }
                    var negativeRule = new Rule4Sbox();
                    foreach (var it in negativeIpList)
                    {
                        // Remove first '!' and trim spaces
                        var ip = it[1..].Trim();
                        if (ParseV2Address(ip, negativeRule))
                        {
                            countIp++;
                        }
                    }
                    negativeRule.invert = true;
                    rule2 = new Rule4Sbox()
                    {
                        outbound = rule2.outbound,
                        action = rule2.action,
                        type = "logical",
                        mode = "or",
                        rules = [
                            positiveRule,
                            negativeRule
                        ]
                    };
                }
                else
                {
                    foreach (var it in item.Ip)
                    {
                        if (ParseV2Address(it, rule2))
                        {
                            countIp++;
                        }
                    }
                }
                if (countIp > 0)
                {
                    rules.Add(rule2);
                    hasDomainIp = true;
                }
            }

            if (item.Process?.Count > 0)
            {
                var ruleProcName = JsonUtils.DeepCopy(rule3);
                ruleProcName.process_name ??= [];
                var ruleProcPath = JsonUtils.DeepCopy(rule3);
                ruleProcPath.process_path ??= [];
                foreach (var process in item.Process)
                {
                    // sing-box doesn't support this, fall back to process name match
                    if (process is "self/" or "xray/")
                    {
                        ruleProcName.process_name.Add(Utils.GetExeName("sing-box"));
                        continue;
                    }

                    if (process.Contains('/') || process.Contains('\\'))
                    {
                        var procPath = process;
                        if (Utils.IsWindows())
                        {
                            procPath = procPath.Replace('/', '\\');
                        }
                        ruleProcPath.process_path.Add(procPath);
                        continue;
                    }

                    // sing-box strictly matches the exe suffix on Windows
                    var procName = Utils.GetExeName(process);

                    ruleProcName.process_name.Add(procName);
                }

                if (ruleProcName.process_name.Count > 0)
                {
                    rules.Add(ruleProcName);
                    hasDomainIp = true;
                }

                if (ruleProcPath.process_path.Count > 0)
                {
                    rules.Add(ruleProcPath);
                    hasDomainIp = true;
                }
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
    }

    private static bool ParseV2Domain(string domain, Rule4Sbox rule)
    {
        if (domain.StartsWith('#') || domain.StartsWith("ext:") || domain.StartsWith("ext-domain:"))
        {
            return false;
        }
        else if (domain.StartsWith(Global.GeoSitePrefix))
        {
            rule.geosite ??= [];
            rule.geosite?.Add(domain[Global.GeoSitePrefix.Length..]);
        }
        else if (domain.StartsWith("regexp:"))
        {
            rule.domain_regex ??= [];
            rule.domain_regex?.Add(domain.Replace(Global.RoutingRuleComma, ",").Substring(7));
        }
        else if (domain.StartsWith("domain:"))
        {
            rule.domain_suffix ??= [];
            rule.domain_suffix?.Add(domain.Substring(7));
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
        else if (domain.StartsWith("dotless:"))
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

    private static bool ParseV2Address(string address, Rule4Sbox rule)
    {
        if (address.StartsWith("ext:") || address.StartsWith("ext-ip:"))
        {
            return false;
        }
        else if (address.Equals($"{Global.GeoIPPrefix}private"))
        {
            rule.ip_is_private = true;
        }
        else if (address.StartsWith(Global.GeoIPPrefix))
        {
            rule.geoip ??= [];
            rule.geoip?.Add(address[Global.GeoIPPrefix.Length..]);
        }
        else
        {
            rule.ip_cidr ??= [];
            rule.ip_cidr?.Add(address);
        }
        return true;
    }

    private string GenRoutingUserRuleOutbound(string outboundTag)
    {
        if (Global.OutboundTags.Contains(outboundTag))
        {
            return outboundTag;
        }

        var node = context.AllProxiesMap.GetValueOrDefault($"remark:{outboundTag}");

        if (node == null
            || (!Global.SingboxSupportConfigType.Contains(node.ConfigType)
            && !node.ConfigType.IsGroupType()))
        {
            return Global.ProxyTag;
        }

        var tag = $"{node.IndexId}-{Global.ProxyTag}-{node.Remarks}";
        if (_coreConfig.outbounds.Any(o => o.tag.StartsWith(tag))
            || (_coreConfig.endpoints != null && _coreConfig.endpoints.Any(e => e.tag.StartsWith(tag))))
        {
            return tag;
        }

        var proxyOutbounds = new CoreConfigSingboxService(context with { Node = node, }).BuildAllProxyOutbounds(tag);
        FillRangeProxy(proxyOutbounds, _coreConfig, false);

        return tag;
    }
}
