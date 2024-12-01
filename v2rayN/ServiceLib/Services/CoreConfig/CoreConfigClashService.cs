namespace ServiceLib.Services.CoreConfig
{
    /// <summary>
    /// Core configuration file processing class
    /// </summary>
    public class CoreConfigClashService
    {
        private Config _config;

        public CoreConfigClashService(Config config)
        {
            _config = config;
        }

        /// <summary>
        /// 生成配置文件
        /// </summary>
        /// <param name="node"></param>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
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

                string addressFileName = node.Address;
                if (Utils.IsNullOrEmpty(addressFileName))
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

                string tagYamlStr1 = "!<str>";
                string tagYamlStr2 = "__strn__";
                string tagYamlStr3 = "!!str";
                var txtFile = File.ReadAllText(addressFileName);
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

                //port
                fileContent["port"] = AppHandler.Instance.GetLocalPort(EInboundProtocol.http);
                //socks-port
                fileContent["socks-port"] = AppHandler.Instance.GetLocalPort(EInboundProtocol.socks);
                //log-level
                fileContent["log-level"] = GetLogLevel(_config.CoreBasicItem.Loglevel);

                //external-controller
                fileContent["external-controller"] = $"{Global.Loopback}:{AppHandler.Instance.StatePort2}";
                //allow-lan
                if (_config.Inbound.First().AllowLANConn)
                {
                    fileContent["allow-lan"] = "true";
                    fileContent["bind-address"] = "*";
                }
                else
                {
                    fileContent["allow-lan"] = "false";
                }

                //ipv6
                fileContent["ipv6"] = _config.ClashUIItem.EnableIPv6;

                //mode
                if (!fileContent.ContainsKey("mode"))
                {
                    fileContent["mode"] = ERuleMode.Rule.ToString().ToLower();
                }
                else
                {
                    if (_config.ClashUIItem.RuleMode != ERuleMode.Unchanged)
                    {
                        fileContent["mode"] = _config.ClashUIItem.RuleMode.ToString().ToLower();
                    }
                }

                //enable tun mode
                if (_config.TunModeItem.EnableTun)
                {
                    string tun = Utils.GetEmbedText(Global.ClashTunYaml);
                    if (Utils.IsNotEmpty(tun))
                    {
                        var tunContent = YamlUtils.FromYaml<Dictionary<string, object>>(tun);
                        if (tunContent != null)
                            fileContent["tun"] = tunContent["tun"];
                    }
                }

                //Mixin
                try
                {
                    MixinContent(fileContent, node);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("GenerateClientConfigClash-Mixin", ex);
                }

                var txtFileNew = YamlUtils.ToYaml(fileContent).Replace(tagYamlStr2, tagYamlStr3);
                await File.WriteAllTextAsync(fileName, txtFileNew);
                //check again
                if (!File.Exists(fileName))
                {
                    ret.Msg = ResUI.FailedReadConfiguration + "2";
                    return ret;
                }

                ClashApiHandler.Instance.ProfileContent = fileContent;

                ret.Msg = string.Format(ResUI.SuccessfulConfiguration, $"{node.GetSummary()}");
                ret.Success = true;
                return ret;
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GenerateClientConfigClash", ex);
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
        }

        private void MixinContent(Dictionary<string, object> fileContent, ProfileItem node)
        {
            //if (!_config.clashUIItem.enableMixinContent)
            //{
            //    return;
            //}

            var path = Utils.GetConfigPath(Global.ClashMixinConfigFileName);
            if (!File.Exists(path))
            {
                return;
            }

            var txtFile = File.ReadAllText(Utils.GetConfigPath(Global.ClashMixinConfigFileName));

            var mixinContent = YamlUtils.FromYaml<Dictionary<string, object>>(txtFile);
            if (mixinContent == null)
            {
                return;
            }
            foreach (var item in mixinContent)
            {
                if (!_config.TunModeItem.EnableTun && item.Key == "tun")
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
            bool blPrepend = false;
            bool blRemoved = false;
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

            if (!blRemoved && !fileContent.ContainsKey(key))
            {
                fileContent.Add(key, value);
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
                foreach (var item in lstValue)
                {
                    lstOri.Insert(0, item);
                }
            }
            else
            {
                foreach (var item in lstValue)
                {
                    lstOri.Add(item);
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
}