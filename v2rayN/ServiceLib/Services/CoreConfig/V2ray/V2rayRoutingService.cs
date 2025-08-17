namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenRouting(V2rayConfig v2rayConfig)
    {
        try
        {
            if (v2rayConfig.routing?.rules != null)
            {
                v2rayConfig.routing.domainStrategy = _config.RoutingBasicItem.DomainStrategy;

                var routing = await ConfigHandler.GetDefaultRouting(_config);
                if (routing != null)
                {
                    if (routing.DomainStrategy.IsNotEmpty())
                    {
                        v2rayConfig.routing.domainStrategy = routing.DomainStrategy;
                    }
                    var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
                    foreach (var item in rules)
                    {
                        if (item.Enabled)
                        {
                            var item2 = JsonUtils.Deserialize<RulesItem4Ray>(JsonUtils.Serialize(item));
                            await GenRoutingUserRule(item2, v2rayConfig);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return 0;
    }

    private async Task<int> GenRoutingUserRule(RulesItem4Ray? rule, V2rayConfig v2rayConfig)
    {
        try
        {
            if (rule == null)
            {
                return 0;
            }
            rule.outboundTag = await GenRoutingUserRuleOutbound(rule.outboundTag, v2rayConfig);

            if (rule.port.IsNullOrEmpty())
            {
                rule.port = null;
            }
            if (rule.network.IsNullOrEmpty())
            {
                rule.network = null;
            }
            if (rule.domain?.Count == 0)
            {
                rule.domain = null;
            }
            if (rule.ip?.Count == 0)
            {
                rule.ip = null;
            }
            if (rule.protocol?.Count == 0)
            {
                rule.protocol = null;
            }
            if (rule.inboundTag?.Count == 0)
            {
                rule.inboundTag = null;
            }

            var hasDomainIp = false;
            if (rule.domain?.Count > 0)
            {
                var it = JsonUtils.DeepCopy(rule);
                it.ip = null;
                it.type = "field";
                for (var k = it.domain.Count - 1; k >= 0; k--)
                {
                    if (it.domain[k].StartsWith("#"))
                    {
                        it.domain.RemoveAt(k);
                    }
                    it.domain[k] = it.domain[k].Replace(Global.RoutingRuleComma, ",");
                }
                v2rayConfig.routing.rules.Add(it);
                hasDomainIp = true;
            }
            if (rule.ip?.Count > 0)
            {
                var it = JsonUtils.DeepCopy(rule);
                it.domain = null;
                it.type = "field";
                v2rayConfig.routing.rules.Add(it);
                hasDomainIp = true;
            }
            if (!hasDomainIp)
            {
                if (rule.port.IsNotEmpty()
                    || rule.protocol?.Count > 0
                    || rule.inboundTag?.Count > 0
                    || rule.network != null
                    )
                {
                    var it = JsonUtils.DeepCopy(rule);
                    it.type = "field";
                    v2rayConfig.routing.rules.Add(it);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }

    private async Task<string?> GenRoutingUserRuleOutbound(string outboundTag, V2rayConfig v2rayConfig)
    {
        if (Global.OutboundTags.Contains(outboundTag))
        {
            return outboundTag;
        }

        var node = await AppManager.Instance.GetProfileItemViaRemarks(outboundTag);
        if (node == null
            || node.ConfigType == EConfigType.Custom
            || node.ConfigType == EConfigType.Hysteria2
            || node.ConfigType == EConfigType.TUIC
            || node.ConfigType == EConfigType.Anytls)
        {
            return Global.ProxyTag;
        }

        var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
        var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
        await GenOutbound(node, outbound);
        outbound.tag = Global.ProxyTag + node.IndexId.ToString();
        v2rayConfig.outbounds.Add(outbound);

        return outbound.tag;
    }
}
