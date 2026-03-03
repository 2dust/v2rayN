namespace ServiceLib.Handler.Builder;

public record CoreConfigContextBuilderResult(CoreConfigContext Context, NodeValidatorResult ValidatorResult)
{
    public bool Success => ValidatorResult.Success;
}

/// <summary>
/// Holds the results of a full context build, including the main context and an optional
/// pre-socks context (e.g. for TUN protection or pre-socks chaining).
/// </summary>
public record CoreConfigContextBuilderAllResult(
    CoreConfigContextBuilderResult MainResult,
    CoreConfigContextBuilderResult? PreSocksResult)
{
    /// <summary>True only when both the main result and (if present) the pre-socks result succeeded.</summary>
    public bool Success => MainResult.Success && (PreSocksResult?.Success ?? true);

    /// <summary>
    /// Merges all errors and warnings from the main result and the optional pre-socks result
    /// into a single <see cref="NodeValidatorResult"/> for unified notification.
    /// </summary>
    public NodeValidatorResult CombinedValidatorResult => new(
        [.. MainResult.ValidatorResult.Errors, .. PreSocksResult?.ValidatorResult.Errors ?? []],
        [.. MainResult.ValidatorResult.Warnings, .. PreSocksResult?.ValidatorResult.Warnings ?? []]);

    /// <summary>
    /// The main context with TunProtectSsPort/ProxyRelaySsPort and ProtectDomainList merged in
    /// from the pre-socks result (if any). Pass this to the core runner.
    /// </summary>
    public CoreConfigContext ResolvedMainContext => PreSocksResult is not null
        ? MainResult.Context with
        {
            TunProtectSsPort = PreSocksResult.Context.TunProtectSsPort,
            ProxyRelaySsPort = PreSocksResult.Context.ProxyRelaySsPort,
            ProtectDomainList = [.. MainResult.Context.ProtectDomainList ?? [], .. PreSocksResult.Context.ProtectDomainList ?? []],
        }
        : MainResult.Context;
}

public class CoreConfigContextBuilder
{
    /// <summary>
    /// Builds a <see cref="CoreConfigContext"/> for the given node, resolves its proxy map,
    /// and processes outbound nodes referenced by routing rules.
    /// </summary>
    public static async Task<CoreConfigContextBuilderResult> Build(Config config, ProfileItem node)
    {
        var runCoreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
        var coreType = runCoreType == ECoreType.sing_box ? ECoreType.sing_box : ECoreType.Xray;
        var context = new CoreConfigContext()
        {
            Node = node,
            RunCoreType = runCoreType,
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
        var (actNode, nodeValidatorResult) = await ResolveNodeAsync(context, node);
        if (!nodeValidatorResult.Success)
        {
            return new CoreConfigContextBuilderResult(context, nodeValidatorResult);
        }
        context = context with { Node = actNode };
        validatorResult.Warnings.AddRange(nodeValidatorResult.Warnings);
        if (!(context.RoutingItem?.RuleSet.IsNullOrEmpty() ?? true))
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(context.RoutingItem?.RuleSet) ?? [];
            foreach (var ruleItem in rules.Where(ruleItem => ruleItem.Enabled && !Global.OutboundTags.Contains(ruleItem.OutboundTag)))
            {
                if (ruleItem.OutboundTag.IsNullOrEmpty())
                {
                    validatorResult.Warnings.Add(string.Format(ResUI.MsgRoutingRuleEmptyOutboundTag, ruleItem.Remarks));
                    ruleItem.OutboundTag = Global.ProxyTag;
                    continue;
                }
                var ruleOutboundNode = await AppManager.Instance.GetProfileItemViaRemarks(ruleItem.OutboundTag);
                if (ruleOutboundNode == null)
                {
                    validatorResult.Warnings.Add(string.Format(ResUI.MsgRoutingRuleOutboundNodeNotFound, ruleItem.Remarks, ruleItem.OutboundTag));
                    ruleItem.OutboundTag = Global.ProxyTag;
                    continue;
                }

                var (actRuleNode, ruleNodeValidatorResult) = await ResolveNodeAsync(context, ruleOutboundNode, false);
                validatorResult.Warnings.AddRange(ruleNodeValidatorResult.Warnings.Select(w =>
                    string.Format(ResUI.MsgRoutingRuleOutboundNodeWarning, ruleItem.Remarks, ruleItem.OutboundTag, w)));
                if (!ruleNodeValidatorResult.Success)
                {
                    validatorResult.Warnings.AddRange(ruleNodeValidatorResult.Errors.Select(e =>
                        string.Format(ResUI.MsgRoutingRuleOutboundNodeError, ruleItem.Remarks, ruleItem.OutboundTag, e)));
                    ruleItem.OutboundTag = Global.ProxyTag;
                    continue;
                }

                context.AllProxiesMap[$"remark:{ruleItem.OutboundTag}"] = actRuleNode;
            }
        }

        return new CoreConfigContextBuilderResult(context, validatorResult);
    }

    /// <summary>
    /// Builds the main <see cref="CoreConfigContext"/> for <paramref name="node"/> and, when
    /// the main build succeeds, also builds the optional pre-socks context required for TUN
    /// protection or pre-socks proxy chaining.
    /// </summary>
    public static async Task<CoreConfigContextBuilderAllResult> BuildAll(Config config, ProfileItem node)
    {
        var mainResult = await Build(config, node);
        if (!mainResult.Success)
        {
            return new CoreConfigContextBuilderAllResult(mainResult, null);
        }

        var preResult = await BuildPreSocksIfNeeded(mainResult.Context);
        return new CoreConfigContextBuilderAllResult(mainResult, preResult);
    }

    /// <summary>
    /// Determines whether a pre-socks context is required for <paramref name="nodeContext"/>
    /// and, if so, builds and returns it. Returns <c>null</c> when no pre-socks core is needed.
    /// </summary>
    private static async Task<CoreConfigContextBuilderResult?> BuildPreSocksIfNeeded(CoreConfigContext nodeContext)
    {
        var config = nodeContext.AppConfig;
        var node = nodeContext.Node;
        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType);

        var preSocksItem = ConfigHandler.GetPreSocksItem(config, node, coreType);
        if (preSocksItem != null)
        {
            var preSocksResult = await Build(nodeContext.AppConfig, preSocksItem);
            return preSocksResult with
            {
                Context = preSocksResult.Context with
                {
                    ProtectDomainList = [.. nodeContext.ProtectDomainList ?? [], .. preSocksResult.Context.ProtectDomainList ?? []],
                }
            };
        }

        if (!nodeContext.IsTunEnabled
            || coreType != ECoreType.Xray
            || node.ConfigType == EConfigType.Custom)
        {
            return null;
        }

        var tunProtectSsPort = Utils.GetFreePort();
        var proxyRelaySsPort = Utils.GetFreePort();
        var preItem = new ProfileItem()
        {
            CoreType = ECoreType.sing_box,
            ConfigType = EConfigType.Shadowsocks,
            Address = Global.Loopback,
            Port = proxyRelaySsPort,
            Password = Global.None,
        };
        preItem.SetProtocolExtra(preItem.GetProtocolExtra() with
        {
            SsMethod = Global.None,
        });
        var preResult2 = await Build(nodeContext.AppConfig, preItem);
        return preResult2 with
        {
            Context = preResult2.Context with
            {
                ProtectDomainList = [.. nodeContext.ProtectDomainList ?? [], .. preResult2.Context.ProtectDomainList ?? []],
                TunProtectSsPort = tunProtectSsPort,
                ProxyRelaySsPort = proxyRelaySsPort,
            }
        };
    }

    /// <summary>
    /// Resolves a node into the context, optionally wrapping it in a subscription-level proxy chain.
    /// Returns the effective (possibly replaced) node and the validation result.
    /// </summary>
    public static async Task<(ProfileItem, NodeValidatorResult)> ResolveNodeAsync(CoreConfigContext context,
        ProfileItem node,
        bool includeSubChain = true)
    {
        if (node.IndexId.IsNullOrEmpty())
        {
            return (node, NodeValidatorResult.Empty());
        }

        if (includeSubChain)
        {
            var (virtualChainNode, chainValidatorResult) = await BuildSubscriptionChainNodeAsync(node);
            if (virtualChainNode != null)
            {
                context.AllProxiesMap[virtualChainNode.IndexId] = virtualChainNode;
                var (resolvedNode, resolvedResult) = await ResolveNodeAsync(context, virtualChainNode, false);
                resolvedResult.Warnings.InsertRange(0, chainValidatorResult.Warnings);
                return (resolvedNode, resolvedResult);
            }
            // Chain not built but warnings may still exist (e.g. missing profiles)
            if (chainValidatorResult.Warnings.Count > 0)
            {
                var fillResult = await RegisterNodeAsync(context, node);
                fillResult.Warnings.InsertRange(0, chainValidatorResult.Warnings);
                return (node, fillResult);
            }
        }

        var registerResult = await RegisterNodeAsync(context, node);
        return (node, registerResult);
    }

    /// <summary>
    /// If the node's subscription defines prev/next profiles, creates a virtual
    /// <see cref="EConfigType.ProxyChain"/> node that wraps them together.
    /// Returns <c>null</c> as the chain item when no chain is needed.
    /// Any warnings (e.g. missing prev/next profile) are returned in the validator result.
    /// </summary>
    private static async Task<(ProfileItem? ChainNode, NodeValidatorResult ValidatorResult)> BuildSubscriptionChainNodeAsync(ProfileItem node)
    {
        var result = NodeValidatorResult.Empty();

        if (node.Subid.IsNullOrEmpty())
        {
            return (null, result);
        }

        var subItem = await AppManager.Instance.GetSubItem(node.Subid);
        if (subItem == null)
        {
            return (null, result);
        }

        ProfileItem? prevNode = null;
        ProfileItem? nextNode = null;

        if (!subItem.PrevProfile.IsNullOrEmpty())
        {
            prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
            if (prevNode == null)
            {
                result.Warnings.Add(string.Format(ResUI.MsgSubscriptionPrevProfileNotFound, subItem.PrevProfile));
            }
        }
        if (!subItem.NextProfile.IsNullOrEmpty())
        {
            nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
            if (nextNode == null)
            {
                result.Warnings.Add(string.Format(ResUI.MsgSubscriptionNextProfileNotFound, subItem.NextProfile));
            }
        }

        if (prevNode is null && nextNode is null)
        {
            return (null, result);
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
        return (chainNode, result);
    }

    /// <summary>
    /// Dispatches registration to either <see cref="RegisterGroupNodeAsync"/> or
    /// <see cref="RegisterSingleNodeAsync"/> based on the node's config type.
    /// </summary>
    private static async Task<NodeValidatorResult> RegisterNodeAsync(CoreConfigContext context, ProfileItem node)
    {
        if (node.ConfigType.IsGroupType())
        {
            return await RegisterGroupNodeAsync(context, node);
        }
        else
        {
            return RegisterSingleNodeAsync(context, node);
        }
    }

    /// <summary>
    /// Validates a single (non-group) node and, on success, adds it to the proxy map
    /// and records any domain addresses that should bypass the proxy.
    /// </summary>
    private static NodeValidatorResult RegisterSingleNodeAsync(CoreConfigContext context, ProfileItem node)
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

    /// <summary>
    /// Entry point for registering a group node. Initialises the visited/ancestor sets
    /// and delegates to <see cref="TraverseGroupNodeAsync"/>.
    /// </summary>
    private static async Task<NodeValidatorResult> RegisterGroupNodeAsync(CoreConfigContext context,
        ProfileItem node)
    {
        if (!node.ConfigType.IsGroupType())
        {
            return NodeValidatorResult.Empty();
        }

        HashSet<string> ancestors = [node.IndexId];
        HashSet<string> globalVisited = [node.IndexId];
        return await TraverseGroupNodeAsync(context, node, globalVisited, ancestors);
    }

    /// <summary>
    /// Recursively walks the children of a group node, registering valid leaf nodes
    /// and nested groups. Detects cycles via <paramref name="ancestorsGroup"/> and
    /// deduplicates shared nodes via <paramref name="globalVisitedGroup"/>.
    /// </summary>
    private static async Task<NodeValidatorResult> TraverseGroupNodeAsync(
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
                    string.Format(ResUI.MsgGroupCycleDependency, node.Remarks, childNode.Remarks));
                continue;
            }

            if (globalVisitedGroup.Contains(childNode.IndexId))
            {
                childIndexIdList.Add(childNode.IndexId);
                continue;
            }

            if (!childNode.ConfigType.IsGroupType())
            {
                var childNodeResult = RegisterSingleNodeAsync(context, childNode);
                childNodeValidatorResult.Warnings.AddRange(childNodeResult.Warnings.Select(w =>
                    string.Format(ResUI.MsgGroupChildNodeWarning, node.Remarks, childNode.Remarks, w)));
                childNodeValidatorResult.Errors.AddRange(childNodeResult.Errors.Select(e =>
                    string.Format(ResUI.MsgGroupChildNodeError, node.Remarks, childNode.Remarks, e)));
                if (!childNodeResult.Success)
                {
                    continue;
                }

                globalVisitedGroup.Add(childNode.IndexId);
                childIndexIdList.Add(childNode.IndexId);
                continue;
            }

            var newAncestorsGroup = new HashSet<string>(ancestorsGroup) { childNode.IndexId };
            var childGroupResult =
                await TraverseGroupNodeAsync(context, childNode, globalVisitedGroup, newAncestorsGroup);
            childNodeValidatorResult.Warnings.AddRange(childGroupResult.Warnings.Select(w =>
                string.Format(ResUI.MsgGroupChildGroupNodeWarning, node.Remarks, childNode.Remarks, w)));
            childNodeValidatorResult.Errors.AddRange(childGroupResult.Errors.Select(e =>
                string.Format(ResUI.MsgGroupChildGroupNodeError, node.Remarks, childNode.Remarks, e)));
            if (!childGroupResult.Success)
            {
                continue;
            }

            globalVisitedGroup.Add(childNode.IndexId);
            childIndexIdList.Add(childNode.IndexId);
        }

        if (childIndexIdList.Count == 0)
        {
            childNodeValidatorResult.Errors.Add(string.Format(ResUI.MsgGroupNoValidChildNode, node.Remarks));
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
