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

            if (_config.TunModeItem.EnableTun)
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
            }

            if (_config.Inbound.First().SniffingEnabled)
            {
                _coreConfig.route.rules.Add(new()
                {
                    action = "sniff"
                });
                _coreConfig.route.rules.Add(new()
                {
                    protocol = ["dns"],
                    action = "hijack-dns"
                });
            }
            else
            {
                _coreConfig.route.rules.Add(new()
                {
                    port = [53],
                    network = ["udp"],
                    action = "hijack-dns"
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
                _coreConfig.route.rules.Add(new()
                {
                    action = "resolve",
                    domain = hostsDomains,
                });
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
                foreach (var it in item.Ip)
                {
                    if (ParseV2Address(it, rule2))
                    {
                        countIp++;
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

        var tag = $"{node.IndexId}-{Global.ProxyTag}";
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
