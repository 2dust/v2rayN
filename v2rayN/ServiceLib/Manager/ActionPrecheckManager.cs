namespace ServiceLib.Manager;

/// <summary>
/// Centralized pre-checks before sensitive actions (set active profile, generate config, etc.).
/// </summary>
public class ActionPrecheckManager
{
    private static readonly Lazy<ActionPrecheckManager> _instance = new();
    public static ActionPrecheckManager Instance => _instance.Value;

    // sing-box supported transports for different protocol types
    private static readonly HashSet<string> SingboxUnsupportedTransports = [nameof(ETransport.kcp), nameof(ETransport.xhttp)];

    private static readonly HashSet<EConfigType> SingboxTransportSupportedProtocols =
        [EConfigType.VMess, EConfigType.VLESS, EConfigType.Trojan, EConfigType.Shadowsocks];

    private static readonly HashSet<string> SingboxShadowsocksAllowedTransports =
        [nameof(ETransport.tcp), nameof(ETransport.ws), nameof(ETransport.quic)];

    public async Task<List<string>> Check(string? indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return [ResUI.PleaseSelectServer];
        }

        var item = await AppManager.Instance.GetProfileItem(indexId);
        if (item is null)
        {
            return [ResUI.PleaseSelectServer];
        }

        return await Check(item);
    }

    public async Task<List<string>> Check(ProfileItem? item)
    {
        if (item is null)
        {
            return [ResUI.PleaseSelectServer];
        }

        var errors = new List<string>();

        errors.AddRange(await ValidateCurrentNodeAndCoreSupport(item));
        errors.AddRange(await ValidateRelatedNodesExistAndValid(item));

        return errors;
    }

    private async Task<List<string>> ValidateCurrentNodeAndCoreSupport(ProfileItem item)
    {
        if (item.ConfigType == EConfigType.Custom)
        {
            return [];
        }

        var coreType = AppManager.Instance.GetCoreType(item, item.ConfigType);
        return await ValidateNodeAndCoreSupport(item, coreType);
    }

    private async Task<List<string>> ValidateNodeAndCoreSupport(ProfileItem item, ECoreType? coreType = null)
    {
        var errors = new List<string>();

        coreType ??= AppManager.Instance.GetCoreType(item, item.ConfigType);

        if (item.ConfigType is EConfigType.Custom)
        {
            errors.Add(string.Format(ResUI.NotSupportProtocol, item.ConfigType.ToString()));
            return errors;
        }
        else if (item.ConfigType.IsGroupType())
        {
            var groupErrors = await ValidateGroupNode(item, coreType);
            errors.AddRange(groupErrors);
            return errors;
        }
        else if (!item.IsComplex())
        {
            var normalErrors = await ValidateNormalNode(item, coreType);
            errors.AddRange(normalErrors);
            return errors;
        }

        return errors;
    }

    private async Task<List<string>> ValidateNormalNode(ProfileItem item, ECoreType? coreType = null)
    {
        var errors = new List<string>();

        if (item.Address.IsNullOrEmpty())
        {
            errors.Add(string.Format(ResUI.InvalidProperty, "Address"));
            return errors;
        }

        if (item.Port is <= 0 or > 65535)
        {
            errors.Add(string.Format(ResUI.InvalidProperty, "Port"));
            return errors;
        }

        var net = item.GetNetwork();

        if (coreType == ECoreType.sing_box)
        {
            var transportError = ValidateSingboxTransport(item.ConfigType, net);
            if (transportError != null)
            {
                errors.Add(transportError);
            }

            if (!Global.SingboxSupportConfigType.Contains(item.ConfigType))
            {
                errors.Add(string.Format(ResUI.CoreNotSupportProtocol,
                    nameof(ECoreType.sing_box), item.ConfigType.ToString()));
            }
        }
        else if (coreType is ECoreType.Xray)
        {
            // Xray core does not support these protocols
            if (!Global.XraySupportConfigType.Contains(item.ConfigType))
            {
                errors.Add(string.Format(ResUI.CoreNotSupportProtocol,
                    nameof(ECoreType.Xray), item.ConfigType.ToString()));
            }
        }

        switch (item.ConfigType)
        {
            case EConfigType.VMess:
                if (item.Id.IsNullOrEmpty() || !Utils.IsGuidByParse(item.Id))
                {
                    errors.Add(string.Format(ResUI.InvalidProperty, "Id"));
                }

                break;

            case EConfigType.VLESS:
                if (item.Id.IsNullOrEmpty() || (!Utils.IsGuidByParse(item.Id) && item.Id.Length > 30))
                {
                    errors.Add(string.Format(ResUI.InvalidProperty, "Id"));
                }

                if (!Global.Flows.Contains(item.Flow))
                {
                    errors.Add(string.Format(ResUI.InvalidProperty, "Flow"));
                }

                break;

            case EConfigType.Shadowsocks:
                if (item.Id.IsNullOrEmpty())
                {
                    errors.Add(string.Format(ResUI.InvalidProperty, "Id"));
                }

                if (string.IsNullOrEmpty(item.Security) || !Global.SsSecuritiesInSingbox.Contains(item.Security))
                {
                    errors.Add(string.Format(ResUI.InvalidProperty, "Security"));
                }

                break;
        }

        if (item.StreamSecurity == Global.StreamSecurity)
        {
            // check certificate validity
            if ((!item.Cert.IsNullOrEmpty()) && (CertPemManager.ParsePemChain(item.Cert).Count == 0))
            {
                errors.Add(string.Format(ResUI.InvalidProperty, "TLS Certificate"));
            }
        }

        if (item.StreamSecurity == Global.StreamSecurityReality)
        {
            if (item.PublicKey.IsNullOrEmpty())
            {
                errors.Add(string.Format(ResUI.InvalidProperty, "PublicKey"));
            }
        }

        if (item.Network == nameof(ETransport.xhttp)
            && !item.Extra.IsNullOrEmpty())
        {
            // check xhttp extra json validity
            var xhttpExtra = JsonUtils.ParseJson(item.Extra);
            if (xhttpExtra is null)
            {
                errors.Add(string.Format(ResUI.InvalidProperty, "XHTTP Extra"));
            }
        }

        return errors;
    }

    private async Task<List<string>> ValidateGroupNode(ProfileItem item, ECoreType? coreType = null)
    {
        var errors = new List<string>();

        ProfileGroupItemManager.Instance.TryGet(item.IndexId, out var group);
        if (group is null || group.NotHasChild())
        {
            errors.Add(string.Format(ResUI.GroupEmpty, item.Remarks));
            return errors;
        }

        var hasCycle = ProfileGroupItemManager.HasCycle(item.IndexId);
        if (hasCycle)
        {
            errors.Add(string.Format(ResUI.GroupSelfReference, item.Remarks));
            return errors;
        }

        var childIds = Utils.String2List(group.ChildItems) ?? [];
        var subItems = await ProfileGroupItemManager.GetSubChildProfileItems(group);
        childIds.AddRange(subItems.Select(p => p.IndexId));

        foreach (var child in childIds)
        {
            var childErrors = new List<string>();
            if (child.IsNullOrEmpty())
            {
                continue;
            }

            var childItem = await AppManager.Instance.GetProfileItem(child);
            if (childItem is null)
            {
                childErrors.Add(string.Format(ResUI.NodeTagNotExist, child));
                continue;
            }

            if (childItem.ConfigType is EConfigType.Custom or EConfigType.ProxyChain)
            {
                childErrors.Add(string.Format(ResUI.InvalidProperty, childItem.Remarks));
                continue;
            }

            childErrors.AddRange(await ValidateNodeAndCoreSupport(childItem, coreType));
            errors.AddRange(childErrors.Select(s => s.Insert(0, $"{childItem.Remarks}: ")));
        }
        return errors;
    }

    private static string? ValidateSingboxTransport(EConfigType configType, string net)
    {
        // sing-box does not support xhttp / kcp transports
        if (SingboxUnsupportedTransports.Contains(net))
        {
            return string.Format(ResUI.CoreNotSupportNetwork, nameof(ECoreType.sing_box), net);
        }

        // sing-box does not support non-tcp transports for protocols other than vmess/trojan/vless/shadowsocks
        if (!SingboxTransportSupportedProtocols.Contains(configType) && net != nameof(ETransport.tcp))
        {
            return string.Format(ResUI.CoreNotSupportProtocolTransport,
                nameof(ECoreType.sing_box), configType.ToString(), net);
        }

        // sing-box shadowsocks only supports tcp/ws/quic transports
        if (configType == EConfigType.Shadowsocks && !SingboxShadowsocksAllowedTransports.Contains(net))
        {
            return string.Format(ResUI.CoreNotSupportProtocolTransport,
                nameof(ECoreType.sing_box), configType.ToString(), net);
        }

        return null;
    }

    private async Task<List<string>> ValidateRelatedNodesExistAndValid(ProfileItem? item)
    {
        var errors = new List<string>();
        errors.AddRange(await ValidateProxyChainedNodeExistAndValid(item));
        errors.AddRange(await ValidateRoutingNodeExistAndValid(item));
        return errors;
    }

    private async Task<List<string>> ValidateProxyChainedNodeExistAndValid(ProfileItem? item)
    {
        var errors = new List<string>();
        if (item is null)
        {
            return errors;
        }

        // prev node and next node
        var subItem = await AppManager.Instance.GetSubItem(item.Subid);
        if (subItem is null)
        {
            return errors;
        }

        var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
        var nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
        var coreType = AppManager.Instance.GetCoreType(item, item.ConfigType);

        await CollectProxyChainedNodeValidation(prevNode, subItem.PrevProfile, coreType, errors);
        await CollectProxyChainedNodeValidation(nextNode, subItem.NextProfile, coreType, errors);

        return errors;
    }

    private async Task CollectProxyChainedNodeValidation(ProfileItem? node, string tag, ECoreType coreType, List<string> errors)
    {
        if (node is not null)
        {
            var nodeErrors = await ValidateNodeAndCoreSupport(node, coreType);
            errors.AddRange(nodeErrors.Select(s => ResUI.ProxyChainedPrefix + $"{node.Remarks}: " + s));
        }
        else if (tag.IsNotEmpty())
        {
            errors.Add(ResUI.ProxyChainedPrefix + string.Format(ResUI.NodeTagNotExist, tag));
        }
    }

    private async Task<List<string>> ValidateRoutingNodeExistAndValid(ProfileItem? item)
    {
        var errors = new List<string>();

        if (item is null)
        {
            return errors;
        }

        var coreType = AppManager.Instance.GetCoreType(item, item.ConfigType);
        var routing = await ConfigHandler.GetDefaultRouting(AppManager.Instance.Config);
        if (routing == null)
        {
            return errors;
        }

        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
        foreach (var ruleItem in rules ?? [])
        {
            if (!ruleItem.Enabled)
            {
                continue;
            }

            var outboundTag = ruleItem.OutboundTag;
            if (outboundTag.IsNullOrEmpty() || Global.OutboundTags.Contains(outboundTag))
            {
                continue;
            }

            var tagItem = await AppManager.Instance.GetProfileItemViaRemarks(outboundTag);
            if (tagItem is null)
            {
                errors.Add(ResUI.RoutingRuleOutboundPrefix + string.Format(ResUI.NodeTagNotExist, outboundTag));
                continue;
            }

            var tagErrors = await ValidateNodeAndCoreSupport(tagItem, coreType);
            errors.AddRange(tagErrors.Select(s => ResUI.RoutingRuleOutboundPrefix + $"{tagItem.Remarks}: " + s));
        }

        return errors;
    }
}
