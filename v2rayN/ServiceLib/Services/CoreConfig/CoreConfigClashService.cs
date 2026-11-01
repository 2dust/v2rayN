using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ServiceLib.Services.CoreConfig;

/// <summary>
/// Core configuration file processing class.
/// The TUN state is taken as a snapshot so the generated config always agrees
/// with the launch elevation decision (see CoreManager.ShouldRunAsSudo).
/// </summary>
public class CoreConfigClashService(Config config, bool isTunEnabled)
{
    private static readonly string _tag = "CoreConfigClashService";

    public async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        if (node == null || fileName is null)
        {
            ret.Msg = ResUI.CheckServerSettings;
            return ret;
        }

        ret.Msg = ResUI.InitialConfiguration;

        try
        {
            if (node == null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            var addressFileName = node.Address;
            if (addressFileName.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }
            if (!File.Exists(addressFileName))
            {
                addressFileName = Path.Combine(Utils.GetConfigPath(), addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "1";
                return ret;
            }

            var tagYamlStr1 = "!<str>";
            var tagYamlStr2 = "__strn__";
            var tagYamlStr3 = "!!str";
            var txtFile = await File.ReadAllTextAsync(addressFileName);
            txtFile = txtFile.Replace(tagYamlStr1, tagYamlStr2);

            //YAML anchors
            if (txtFile.Contains("<<:") && txtFile.Contains('*') && txtFile.Contains('&'))
            {
                txtFile = YamlUtils.PreprocessYaml(txtFile) ?? "";
            }

            var fileContent = YamlUtils.FromYaml(txtFile);
            if (fileContent == null)
            {
                ret.Msg = ResUI.FailedConversionConfiguration;
                return ret;
            }

            //mixed-port
            //fileContent["mixed-port"] = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            fileContent.Children[new YamlScalarNode("mixed-port")] = new YamlScalarNode(AppManager.Instance.GetLocalPort(EInboundProtocol.socks).ToString());
            //log-level
            //fileContent["log-level"] = GetLogLevel(config.CoreBasicItem.Loglevel);
            fileContent.Children[new YamlScalarNode("log-level")] = new YamlScalarNode(GetLogLevel(config.CoreBasicItem.Loglevel));

            //external-controller
            //fileContent["external-controller"] = $"{Global.Loopback}:{AppManager.Instance.StatePort2}";
            fileContent.Children[new YamlScalarNode("external-controller")] = new YamlScalarNode($"{Global.Loopback}:{AppManager.Instance.StatePort2}");
            //fileContent.Remove("secret");
            fileContent.Children.Remove(new YamlScalarNode("secret"));
            //allow-lan
            if (config.Inbound.First().AllowLANConn)
            {
                //fileContent["allow-lan"] = "true";
                fileContent.Children[new YamlScalarNode("allow-lan")] = new YamlScalarNode("true");
                //fileContent["bind-address"] = "*";
                fileContent.Children[new YamlScalarNode("bind-address")] = new YamlScalarNode("*");
            }
            else
            {
                //fileContent["allow-lan"] = "false";
                fileContent.Children[new YamlScalarNode("allow-lan")] = new YamlScalarNode("false");
            }

            //ipv6
            //fileContent["ipv6"] = config.ClashUIItem.EnableIPv6;
            fileContent.Children[new YamlScalarNode("ipv6")] = new YamlScalarNode(config.ClashUIItem.EnableIPv6.ToString().ToLowerInvariant());

            //mode
            //fileContent.TryAdd("mode", nameof(ERuleMode.Rule));
            fileContent.Children.TryAdd(new YamlScalarNode("mode"), new YamlScalarNode(nameof(ERuleMode.Rule)));

            //enable tun mode
            if (isTunEnabled)
            {
                var tun = EmbedUtils.GetEmbedText(Global.ClashTunYaml);
                if (tun.IsNotEmpty())
                {
                    var tunContent = YamlUtils.FromYaml(tun);
                    if (tunContent != null)
                    {
                        //fileContent["tun"] = tunContent["tun"];
                        fileContent.Children[new YamlScalarNode("tun")] = tunContent.Children[new YamlScalarNode("tun")];
                    }
                }
            }

            //Mixin
            try
            {
                await MixinContent(fileContent, node);
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"{_tag}-Mixin", ex);
            }

            //// Mihomo parses plain values such as 815458e4 as floats, so quote REALITY short IDs.
            //var originalRealityShortIds = new List<(Dictionary<object, object> RealityOptions, string ShortId)>();
            //if (fileContent.GetValueOrDefault("proxies") is List<object> proxies)
            //{
            //    foreach (var proxy in proxies.OfType<Dictionary<object, object>>())
            //    {
            //        if (proxy.GetValueOrDefault("reality-opts") is Dictionary<object, object> realityOptions
            //            && realityOptions.GetValueOrDefault("short-id") is string shortId
            //            && !shortId.StartsWith(tagYamlStr2, StringComparison.Ordinal))
            //        {
            //            originalRealityShortIds.Add((realityOptions, shortId));
            //            realityOptions["short-id"] = new YamlScalarNode(shortId) { Style = ScalarStyle.DoubleQuoted };
            //        }
            //    }
            //}

            var txtFileNew = YamlUtils.ToYaml(fileContent).Replace(tagYamlStr2, tagYamlStr3);
            //foreach (var (realityOptions, shortId) in originalRealityShortIds)
            //{
            //    realityOptions["short-id"] = shortId;
            //}
            await File.WriteAllTextAsync(fileName, txtFileNew);
            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "2";
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, $"{node.GetSummary()}");
            ret.Success = true;
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    private async Task MixinContent(YamlMappingNode fileContent, ProfileItem node)
    {
        if (!config.ClashUIItem.EnableMixinContent)
        {
            return;
        }

        var path = Utils.GetConfigPath(Global.ClashMixinConfigFileName);
        if (!File.Exists(path))
        {
            var mixin = EmbedUtils.GetEmbedText(Global.ClashMixinYaml);
            await File.AppendAllTextAsync(path, mixin);
        }

        var txtFile = await File.ReadAllTextAsync(Utils.GetConfigPath(Global.ClashMixinConfigFileName));

        var mixinContent = YamlUtils.FromYaml(txtFile);
        if (mixinContent == null)
        {
            return;
        }
        foreach (var (keyNode, valueNode) in mixinContent.Children)
        {
            if (keyNode is not YamlScalarNode scalarKey)
            {
                continue;
            }
            var key = scalarKey.Value ?? "";
            if (!isTunEnabled && key == "tun")
            {
                continue;
            }

            if (key.StartsWith("prepend-")
                || key.StartsWith("append-")
                || key.StartsWith("removed-"))
            {
                ModifyContentMerge(fileContent, key, valueNode);
            }
            else
            {
                fileContent.Children[keyNode] = valueNode;
            }
        }
        return;
    }

    private void ModifyContentMerge(YamlMappingNode fileContent, YamlNode keyNode, YamlNode valueNode)
    {
        if (keyNode is not YamlScalarNode { Value: string key })
        {
            return;
        }

        var blPrepend = false;
        var blRemoved = false;
        string realKey;

        if (key.StartsWith("prepend-"))
        {
            blPrepend = true;
            realKey = key["prepend-".Length..];
        }
        else if (key.StartsWith("append-"))
        {
            realKey = key["append-".Length..];
        }
        else if (key.StartsWith("removed-"))
        {
            blRemoved = true;
            realKey = key["removed-".Length..];
        }
        else
        {
            return;
        }

        var targetKeyNode = new YamlScalarNode(realKey);

        if (!blRemoved && fileContent.Children.TryAdd(targetKeyNode, valueNode))
        {
            return;
        }

        if (!fileContent.Children.TryGetValue(targetKeyNode, out var existingNode) ||
            existingNode is not YamlSequenceNode lstOri ||
            valueNode is not YamlSequenceNode lstValue)
        {
            return;
        }

        if (blRemoved)
        {
            var removePrefixes = lstValue.Children
                .OfType<YamlScalarNode>()
                .Select(s => s.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            for (var i = lstOri.Children.Count - 1; i >= 0; i--)
            {
                if (lstOri.Children[i] is YamlScalarNode { Value: not null } itemScalar)
                {
                    if (removePrefixes.Any(prefix => itemScalar.Value.StartsWith(prefix!)))
                    {
                        lstOri.Children.RemoveAt(i);
                    }
                }
            }
            return;
        }

        if (blPrepend)
        {
            // Insert in reverse order to maintain original sequence at index 0
            for (var i = lstValue.Children.Count - 1; i >= 0; i--)
            {
                lstOri.Children.Insert(0, lstValue.Children[i]);
            }
        }
        else
        {
            foreach (var item in lstValue.Children)
            {
                lstOri.Children.Add(item);
            }
        }
    }

    private string GetLogLevel(string level)
    {
        if (level == "none")
        {
            return "silent";
        }
        else
        {
            return level;
        }
    }
}
