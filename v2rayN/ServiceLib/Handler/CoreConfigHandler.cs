namespace ServiceLib.Handler;

/// <summary>
/// Core configuration file processing class
/// </summary>
public static class CoreConfigHandler
{
    private static readonly string _tag = "CoreConfigHandler";

    public static async Task<RetResult> GenerateClientConfig(CoreConfigContext context, string? fileName)
    {
        var config = AppManager.Instance.Config;
        var result = new RetResult();
        var node = context.Node;

        if (node.ConfigType == EConfigType.Custom)
        {
            result = node.CoreType switch
            {
                ECoreType.mihomo => await new CoreConfigClashService(config).GenerateClientCustomConfig(node, fileName),
                _ => await GenerateClientCustomConfig(node, fileName)
            };
        }
        else if (AppManager.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box)
        {
            result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        }
        else
        {
            result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        }
        if (result.Success != true)
        {
            return result;
        }
        if (fileName.IsNotEmpty() && result.Data != null)
        {
            await File.WriteAllTextAsync(fileName, result.Data.ToString());
        }

        return result;
    }

    private static async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        try
        {
            if (node == null || fileName is null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.SetAttributes(fileName, FileAttributes.Normal); //If the file has a read-only attribute, direct deletion will fail
                File.Delete(fileName);
            }

            var addressFileName = node.Address;
            if (!File.Exists(addressFileName))
            {
                addressFileName = Utils.GetConfigPath(addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            File.Copy(addressFileName, fileName);
            File.SetAttributes(fileName, FileAttributes.Normal); //Copy will keep the attributes of addressFileName, so we need to add write permissions to fileName just in case of addressFileName is a read-only file.

            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            return await Task.FromResult(ret);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, string fileName, List<ServerTestItem> selecteds, ECoreType coreType)
    {
        var result = new RetResult();
        var context = await BuildCoreConfigContext(config, new());
        var ids = selecteds.Where(serverTestItem => !serverTestItem.IndexId.IsNullOrEmpty())
            .Select(serverTestItem => serverTestItem.IndexId);
        var nodes = await AppManager.Instance.GetProfileItemsByIndexIds(ids);
        foreach (var node in nodes)
        {
            await FillNodeContext(context, node, false);
        }
        if (coreType == ECoreType.sing_box)
        {
            result = new CoreConfigSingboxService(context).GenerateClientSpeedtestConfig(selecteds);
        }
        else if (coreType == ECoreType.Xray)
        {
            result = new CoreConfigV2rayService(context).GenerateClientSpeedtestConfig(selecteds);
        }
        if (result.Success != true)
        {
            return result;
        }
        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, CoreConfigContext context, ServerTestItem testItem, string fileName)
    {
        var result = new RetResult();
        var node = context.Node;
        var initPort = AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest);
        var port = Utils.GetFreePort(initPort + testItem.QueueNum);
        testItem.Port = port;

        if (AppManager.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box)
        {
            result = new CoreConfigSingboxService(context).GenerateClientSpeedtestConfig(port);
        }
        else
        {
            result = new CoreConfigV2rayService(context).GenerateClientSpeedtestConfig(port);
        }
        if (result.Success != true)
        {
            return result;
        }

        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    public static async Task<CoreConfigContext> BuildCoreConfigContext(Config config, ProfileItem node)
    {
        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType) == ECoreType.sing_box ? ECoreType.sing_box : ECoreType.Xray;
        var context = new CoreConfigContext()
        {
            Node = node,
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
        context = context with
        {
            Node = await FillNodeContext(context, node)
        };
        if (!(context.RoutingItem?.RuleSet.IsNullOrEmpty() ?? true))
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(context.RoutingItem?.RuleSet);
            foreach (var ruleItem in rules.Where(ruleItem => !Global.OutboundTags.Contains(ruleItem.OutboundTag)))
            {
                var ruleOutboundNode = await AppManager.Instance.GetProfileItemViaRemarks(ruleItem.OutboundTag);
                if (ruleOutboundNode != null)
                {
                    var ruleOutboundNodeAct = await FillNodeContext(context, ruleOutboundNode, false);
                    context.AllProxiesMap[$"remark:{ruleItem.OutboundTag}"] = ruleOutboundNodeAct;
                }
            }
        }
        return context;
    }

    private static async Task<ProfileItem> FillNodeContext(CoreConfigContext context, ProfileItem node, bool includeSubChain = true)
    {
        if (node.IndexId.IsNullOrEmpty())
        {
            return node;
        }
        var newItems = new List<ProfileItem> { node };
        if (node.ConfigType.IsGroupType())
        {
            var (groupChildList, _) = await GroupProfileManager.GetChildProfileItems(node);
            foreach (var childItem in groupChildList.Where(childItem => !context.AllProxiesMap.ContainsKey(childItem.IndexId)))
            {
                await FillNodeContext(context, childItem, false);
            }
            node.SetProtocolExtra(node.GetProtocolExtra() with
            {
                ChildItems = Utils.List2String(groupChildList.Select(n => n.IndexId).ToList()),
            });
            newItems.AddRange(groupChildList);
        }
        context.AllProxiesMap[node.IndexId] = node;

        foreach (var item in newItems)
        {
            var address = item.Address;
            if (Utils.IsDomain(address))
            {
                context.ProtectDomainList.Add(address);
            }

            if (item.EchConfigList.IsNullOrEmpty())
            {
                continue;
            }

            var echQuerySni = item.Sni;
            if (item.StreamSecurity == Global.StreamSecurity
                && item.EchConfigList?.Contains("://") == true)
            {
                var idx = item.EchConfigList.IndexOf('+');
                echQuerySni = idx > 0 ? item.EchConfigList[..idx] : item.Sni;
            }
            if (!Utils.IsDomain(echQuerySni))
            {
                continue;
            }
            context.ProtectDomainList.Add(echQuerySni);
        }

        if (!includeSubChain || node.Subid.IsNullOrEmpty())
        {
            return node;
        }

        var subItem = await AppManager.Instance.GetSubItem(node.Subid);
        if (subItem == null)
        {
            return node;
        }
        var prevNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.PrevProfile);
        var nextNode = await AppManager.Instance.GetProfileItemViaRemarks(subItem.NextProfile);
        if (prevNode is null && nextNode is null)
        {
            return node;
        }

        var prevNodeAct = prevNode is null ? null : await FillNodeContext(context, prevNode, false);
        var nextNodeAct = nextNode is null ? null : await FillNodeContext(context, nextNode, false);

        // Build new proxy chain node
        var chainNode = new ProfileItem()
        {
            IndexId = $"inner-{Utils.GetGuid(false)}",
            ConfigType = EConfigType.ProxyChain,
            CoreType = node.CoreType ?? ECoreType.Xray,
        };
        List<string?> childItems = [prevNodeAct?.IndexId, node.IndexId, nextNodeAct?.IndexId];
        var chainExtraItem = chainNode.GetProtocolExtra() with
        {
            GroupType = chainNode.ConfigType.ToString(),
            ChildItems = string.Join(",", childItems.Where(x => !x.IsNullOrEmpty())),
        };
        chainNode.SetProtocolExtra(chainExtraItem);
        context.AllProxiesMap[chainNode.IndexId] = chainNode;
        return chainNode;
    }
}
