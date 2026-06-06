namespace ServiceLib.Services.CoreConfig;

/// <summary>
/// Core configuration file processing class
/// </summary>
public class CoreConfigClashService(Config config)
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
                txtFile = YamlUtils.PreprocessYaml(txtFile);
            }

            var fileContent = YamlUtils.FromYaml<Dictionary<string, object>>(txtFile);
            if (fileContent == null)
            {
                ret.Msg = ResUI.FailedConversionConfiguration;
                return ret;
            }

            //mixed-port
            fileContent["mixed-port"] = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            //log-level
            fileContent["log-level"] = GetLogLevel(config.CoreBasicItem.Loglevel);

            //external-controller
            fileContent["external-controller"] = $"{Global.Loopback}:{AppManager.Instance.StatePort2}";
            fileContent.Remove("secret");
            //allow-lan
            if (config.Inbound.First().AllowLANConn)
            {
                fileContent["allow-lan"] = "true";
                fileContent["bind-address"] = "*";
            }
            else
            {
                fileContent["allow-lan"] = "false";
            }

            //ipv6
            fileContent["ipv6"] = config.ClashUIItem.EnableIPv6;

            //mode
            if (!fileContent.ContainsKey("mode"))
            {
                fileContent["mode"] = nameof(ERuleMode.Rule).ToLower();
            }
            else
            {
                if (config.ClashUIItem.RuleMode != ERuleMode.Unchanged)
                {
                    fileContent["mode"] = config.ClashUIItem.RuleMode.ToString().ToLower();
                }
            }

            //enable tun mode
            if (config.TunModeItem.EnableTun)
            {
                var tun = EmbedUtils.GetEmbedText(Global.ClashTunYaml);
                if (tun.IsNotEmpty())
                {
                    var tunContent = YamlUtils.FromYaml<Dictionary<string, object>>(tun);
                    if (tunContent != null)
                    {
                        fileContent["tun"] = tunContent["tun"];
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

            var txtFileNew = YamlUtils.ToYaml(fileContent).Replace(tagYamlStr2, tagYamlStr3);
            await File.WriteAllTextAsync(fileName, txtFileNew);
            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "2";
                return ret;
            }

            ClashApiManager.Instance.ProfileContent = fileContent;

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

    private async Task MixinContent(Dictionary<string, object> fileContent, ProfileItem node)
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

        var mixinContent = YamlUtils.FromYaml<Dictionary<string, object>>(txtFile);
        if (mixinContent == null)
        {
            return;
        }
        foreach (var item in mixinContent)
        {
            if (!config.TunModeItem.EnableTun && item.Key == "tun")
            {
                continue;
            }

            if (item.Key.StartsWith("prepend-")
                || item.Key.StartsWith("append-")
                || item.Key.StartsWith("removed-"))
            {
                ModifyContentMerge(fileContent, item.Key, item.Value);
            }
            else
            {
                fileContent[item.Key] = item.Value;
            }
        }
        return;
    }

    private void ModifyContentMerge(Dictionary<string, object> fileContent, string key, object value)
    {
        var blPrepend = false;
        var blRemoved = false;
        if (key.StartsWith("prepend-"))
        {
            blPrepend = true;
            key = key.Replace("prepend-", "");
        }
        else if (key.StartsWith("append-"))
        {
            blPrepend = false;
            key = key.Replace("append-", "");
        }
        else if (key.StartsWith("removed-"))
        {
            blRemoved = true;
            key = key.Replace("removed-", "");
        }
        else
        {
            return;
        }

        if (!blRemoved && fileContent.TryAdd(key, value))
        {
            return;
        }
        var lstOri = (List<object>)fileContent[key];
        var lstValue = (List<object>)value;

        if (blRemoved)
        {
            foreach (var item in lstValue)
            {
                lstOri.RemoveAll(t => t.ToString().StartsWith(item.ToString()));
            }
            return;
        }

        if (blPrepend)
        {
            lstValue.Reverse();
            lstValue.ForEach(item => lstOri.Insert(0, item));
        }
        else
        {
            lstValue.ForEach(lstOri.Add);
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
