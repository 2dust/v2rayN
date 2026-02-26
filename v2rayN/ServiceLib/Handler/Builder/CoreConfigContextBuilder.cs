namespace ServiceLib.Handler.Builder;

public record CoreConfigContextBuilderResult(CoreConfigContext Context, NodeValidatorResult ValidatorResult)
{
    public bool Success => ValidatorResult.Success;
}

public class CoreConfigContextBuilder
{
    public static async Task<CoreConfigContextBuilderResult> Build(Config config, ProfileItem node)
    {
        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box
            ? ECoreType.sing_box
            : ECoreType.Xray;
        var context = new CoreConfigContext()
        {
            Node = node,
            RunCoreType = AppManager.Instance.GetCoreType(node, node.ConfigType),
            AllProxiesMap = [],
            AppConfig = config,
            FullConfigTemplate = await AppManager.Instance.GetFullConfigTemplateItem(coreType),
            IsTunEnabled = config.TunModeItem.EnableTun,
            SimpleDnsItem = config.SimpleDNSItem,
            ProtectDomainList = [],
            TunProtectSsPort = 0,
            ProxyRelaySsPort = 0,
            RawDnsItem = await AppManager.Instance.GetDNSItem(coreType),
            RoutingItem = await ConfigHandler.GetDefaultRouting(config),
        };
        var validatorResult = NodeValidatorResult.Empty();
        var (actNode, nodeValidatorResult) = await FillNodeContext(context, node);
        if (!nodeValidatorResult.Success)
        {
            return new CoreConfigContextBuilderResult(context, nodeValidatorResult);
        }
        context = context with { Node = actNode };
        validatorResult.Warnings.AddRange(nodeValidatorResult.Warnings);
        if (!(context.RoutingItem?.RuleSet.IsNullOrEmpty() ?? true))
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(context.RoutingItem?.RuleSet);
            foreach (var ruleItem in rules.Where(ruleItem => !Global.OutboundTags.Contains(ruleItem.OutboundTag)))
            {
                var ruleOutboundNode = await AppManager.Instance.GetProfileItemViaRemarks(ruleItem.OutboundTag);
                if (ruleOutboundNode == null)
                {
                    continue;
                }

                var (actRuleNode, ruleNodeValidatorResult) = await FillNodeContext(context, ruleOutboundNode, false);
                validatorResult.Warnings.AddRange(ruleNodeValidatorResult.Warnings.Select(w =>
                    $"Routing rule {ruleItem.Remarks} outbound node {ruleItem.OutboundTag} warning: {w}"));
                if (!ruleNodeValidatorResult.Success)
                {
                    validatorResult.Warnings.AddRange(ruleNodeValidatorResult.Errors.Select(e =>
                        $"Routing rule {ruleItem.Remarks} outbound node {ruleItem.OutboundTag} error: {e}. Fallback to proxy node only."));
                    ruleItem.OutboundTag = Global.ProxyTag;
                    continue;
                }

                context.AllProxiesMap[$"remark:{ruleItem.OutboundTag}"] = actRuleNode;
            }
        }

        return new CoreConfigContextBuilderResult(context, validatorResult);
    }

    public static async Task<(ProfileItem, NodeValidatorResult)> FillNodeContext(CoreConfigContext context,
        ProfileItem node,
        bool includeSubChain = true)
    {
        if (node.IndexId.IsNullOrEmpty())
        {
            return (node, NodeValidatorResult.Empty());
        }

        if (includeSubChain)
        {
            var virtualChainNode = await BuildVirtualSubChainNode(node);
            if (virtualChainNode != null)
            {
                context.AllProxiesMap[virtualChainNode.IndexId] = virtualChainNode;
                return await FillNodeContext(context, virtualChainNode, false);
            }
        }

        var fillResult = await FillNodeContextPrivate(context, node);
        return (node, fillResult);
    }

    private static async Task<ProfileItem?> BuildVirtualSubChainNode(ProfileItem node)
    {
        if (node.Subid.IsNullOrEmpty())
        {
            return null;
        }

        var subItem = await AppManager.Instance.GetSubItem(node.Subid);
        if (subItem == null
            || (subItem.PrevProfile.IsNullOrEmpty()
            && subItem.NextProfile.IsNullOrEmpty()))
        {
            return null;
        }

        var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
        var nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
        if (prevNode is null && nextNode is null)
        {
            return null;
        }

        // Build new proxy chain node
        var chainNode = new ProfileItem()
        {
            IndexId = $"inner-{Utils.GetGuid(false)}",
            ConfigType = EConfigType.ProxyChain,
            CoreType = node.CoreType ?? ECoreType.Xray,
        };
        List<string?> childItems = [prevNode?.IndexId, node.IndexId, nextNode?.IndexId];
        var chainExtraItem = chainNode.GetProtocolExtra() with
        {
            GroupType = chainNode.ConfigType.ToString(),
            ChildItems = string.Join(",", childItems.Where(x => !x.IsNullOrEmpty())),
        };
        chainNode.SetProtocolExtra(chainExtraItem);
        return chainNode;
    }

    private static async Task<NodeValidatorResult> FillNodeContextPrivate(CoreConfigContext context, ProfileItem node)
    {
        if (node.ConfigType.IsGroupType())
        {
            return await FillGroupNodeContextPrivate(context, node);
        }
        else
        {
            return FillNormalNodeContextPrivate(context, node);
        }
    }

    private static NodeValidatorResult FillNormalNodeContextPrivate(CoreConfigContext context, ProfileItem node)
    {
        if (node.ConfigType.IsGroupType())
        {
            return NodeValidatorResult.Empty();
        }

        var nodeValidatorResult = NodeValidator.Validate(node, context.RunCoreType);
        if (!nodeValidatorResult.Success)
        {
            return nodeValidatorResult;
        }

        context.AllProxiesMap[node.IndexId] = node;

        var address = node.Address;
        if (Utils.IsDomain(address))
        {
            context.ProtectDomainList.Add(address);
        }

        if (!node.EchConfigList.IsNullOrEmpty())
        {
            var echQuerySni = node.Sni;
            if (node.StreamSecurity == Global.StreamSecurity
                && node.EchConfigList?.Contains("://") == true)
            {
                var idx = node.EchConfigList.IndexOf('+');
                echQuerySni = idx > 0 ? node.EchConfigList[..idx] : node.Sni;
            }

            if (Utils.IsDomain(echQuerySni))
            {
                context.ProtectDomainList.Add(echQuerySni);
            }
        }

        return nodeValidatorResult;
    }

    private static async Task<NodeValidatorResult> FillGroupNodeContextPrivate(CoreConfigContext context,
        ProfileItem node)
    {
        if (!node.ConfigType.IsGroupType())
        {
            return NodeValidatorResult.Empty();
        }

        HashSet<string> ancestors = [node.IndexId];
        HashSet<string> globalVisited = [node.IndexId];
        return await FillGroupNodeContextPrivate(context, node, globalVisited, ancestors);
    }

    private static async Task<NodeValidatorResult> FillGroupNodeContextPrivate(
        CoreConfigContext context,
        ProfileItem node,
        HashSet<string> globalVisitedGroup,
        HashSet<string> ancestorsGroup)
    {
        var (groupChildList, _) = await GroupProfileManager.GetChildProfileItems(node);
        List<string> childIndexIdList = [];
        var childNodeValidatorResult = NodeValidatorResult.Empty();
        foreach (var childNode in groupChildList)
        {
            if (ancestorsGroup.Contains(childNode.IndexId))
            {
                childNodeValidatorResult.Errors.Add(
                    $"Group {node.Remarks} has a cycle dependency on child node {childNode.Remarks}. Skipping this node.");
                continue;
            }

            if (globalVisitedGroup.Contains(childNode.IndexId))
            {
                childIndexIdList.Add(childNode.IndexId);
                continue;
            }

            if (!childNode.ConfigType.IsGroupType())
            {
                var childNodeResult = FillNormalNodeContextPrivate(context, childNode);
                childNodeValidatorResult.Warnings.AddRange(childNodeResult.Warnings.Select(w =>
                    $"Group {node.Remarks} child node {childNode.Remarks} warning: {w}"));
                childNodeValidatorResult.Errors.AddRange(childNodeResult.Errors.Select(e =>
                    $"Group {node.Remarks} child node {childNode.Remarks} error: {e}. Skipping this node."));
                if (!childNodeResult.Success)
                {
                    continue;
                }

                globalVisitedGroup.Add(childNode.IndexId);
                childIndexIdList.Add(childNode.IndexId);
                continue;
            }

            globalVisitedGroup.Add(childNode.IndexId);
            var newAncestorsGroup = new HashSet<string>(ancestorsGroup) { childNode.IndexId };
            var childGroupResult =
                await FillGroupNodeContextPrivate(context, childNode, globalVisitedGroup, newAncestorsGroup);
            childNodeValidatorResult.Warnings.AddRange(childGroupResult.Warnings.Select(w =>
                $"Group {node.Remarks} child group node {childNode.Remarks} warning: {w}"));
            childNodeValidatorResult.Errors.AddRange(childGroupResult.Errors.Select(e =>
                $"Group {node.Remarks} child group node {childNode.Remarks} error: {e}. Skipping this node."));
            if (!childGroupResult.Success)
            {
                continue;
            }

            childIndexIdList.Add(childNode.IndexId);
        }

        if (childIndexIdList.Count == 0)
        {
            childNodeValidatorResult.Errors.Add($"Group {node.Remarks} has no valid child node.");
            return childNodeValidatorResult;
        }
        else
        {
            childNodeValidatorResult.Warnings.AddRange(childNodeValidatorResult.Errors);
            childNodeValidatorResult.Errors.Clear();
        }

        node.SetProtocolExtra(node.GetProtocolExtra() with { ChildItems = Utils.List2String(childIndexIdList), });
        context.AllProxiesMap[node.IndexId] = node;
        return childNodeValidatorResult;
    }
}
