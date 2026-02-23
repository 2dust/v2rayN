namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenRouting()
    {
        try
        {
            if (_coreConfig.routing?.rules != null)
            {
                _coreConfig.routing.domainStrategy = _config.RoutingBasicItem.DomainStrategy;

                var routing = context.RoutingItem;
                if (routing != null)
                {
                    if (routing.DomainStrategy.IsNotEmpty())
                    {
                        _coreConfig.routing.domainStrategy = routing.DomainStrategy;
                    }
                    var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
                    foreach (var item in rules)
                    {
                        if (!item.Enabled)
                        {
                            continue;
                        }

                        if (item.RuleType == ERuleType.DNS)
                        {
                            continue;
                        }

                        var item2 = JsonUtils.Deserialize<RulesItem4Ray>(JsonUtils.Serialize(item));
                        GenRoutingUserRule(item2);
                    }
                }
                var balancerTagList = _coreConfig.routing.balancers
                    ?.Select(p => p.tag)
                    .ToList() ?? [];
                if (balancerTagList.Count > 0)
                {
                    foreach (var rulesItem in _coreConfig.routing.rules.Where(r => balancerTagList.Contains(r.outboundTag + Global.BalancerTagSuffix)))
                    {
                        rulesItem.balancerTag = rulesItem.outboundTag + Global.BalancerTagSuffix;
                        rulesItem.outboundTag = null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void GenRoutingUserRule(RulesItem4Ray? userRule)
    {
        try
        {
            if (userRule == null)
            {
                return;
            }
            userRule.outboundTag = GenRoutingUserRuleOutbound(userRule.outboundTag ?? Global.ProxyTag);

            if (userRule.port.IsNullOrEmpty())
            {
                userRule.port = null;
            }
            if (userRule.network.IsNullOrEmpty())
            {
                userRule.network = null;
            }
            if (userRule.domain?.Count == 0)
            {
                userRule.domain = null;
            }
            if (userRule.ip?.Count == 0)
            {
                userRule.ip = null;
            }
            if (userRule.protocol?.Count == 0)
            {
                userRule.protocol = null;
            }
            if (userRule.inboundTag?.Count == 0)
            {
                userRule.inboundTag = null;
            }
            if (userRule.process?.Count == 0)
            {
                userRule.process = null;
            }

            var hasDomainIp = false;
            if (userRule.domain?.Count > 0)
            {
                var it = JsonUtils.DeepCopy(userRule);
                it.ip = null;
                it.process = null;
                it.type = "field";
                for (var k = it.domain.Count - 1; k >= 0; k--)
                {
                    if (it.domain[k].StartsWith('#'))
                    {
                        it.domain.RemoveAt(k);
                    }
                    it.domain[k] = it.domain[k].Replace(Global.RoutingRuleComma, ",");
                }
                _coreConfig.routing.rules.Add(it);
                hasDomainIp = true;
            }
            if (userRule.ip?.Count > 0)
            {
                var it = JsonUtils.DeepCopy(userRule);
                it.domain = null;
                it.process = null;
                it.type = "field";
                _coreConfig.routing.rules.Add(it);
                hasDomainIp = true;
            }
            if (userRule.process?.Count > 0)
            {
                var it = JsonUtils.DeepCopy(userRule);
                it.domain = null;
                it.ip = null;
                it.type = "field";
                _coreConfig.routing.rules.Add(it);
                hasDomainIp = true;
            }
            if (!hasDomainIp)
            {
                if (userRule.port.IsNotEmpty()
                    || userRule.protocol?.Count > 0
                    || userRule.inboundTag?.Count > 0
                    || userRule.network != null
                    )
                {
                    var it = JsonUtils.DeepCopy(userRule);
                    it.type = "field";
                    _coreConfig.routing.rules.Add(it);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private string GenRoutingUserRuleOutbound(string outboundTag)
    {
        if (Global.OutboundTags.Contains(outboundTag))
        {
            return outboundTag;
        }

        var node = context.AllProxiesMap.GetValueOrDefault($"remark:{outboundTag}");

        if (node == null
            || (!Global.XraySupportConfigType.Contains(node.ConfigType)
            && !node.ConfigType.IsGroupType()))
        {
            return Global.ProxyTag;
        }

        var tag = $"{node.IndexId}-{Global.ProxyTag}";
        if (_coreConfig.outbounds.Any(p => p.tag.StartsWith(tag)))
        {
            return tag;
        }

        var proxyOutbounds = new CoreConfigV2rayService(context with { Node = node, }).BuildAllProxyOutbounds(tag);
        _coreConfig.outbounds.AddRange(proxyOutbounds);
        if (proxyOutbounds.Count(n => n.tag.StartsWith(tag)) > 1)
        {
            var multipleLoad = node.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
            GenObservatory(multipleLoad, tag);
            GenBalancer(multipleLoad, tag);
        }

        return tag;
    }
}
