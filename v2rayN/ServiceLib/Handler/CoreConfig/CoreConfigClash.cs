namespace ServiceLib.Handler.CoreConfig
{
    /// <summary>
    /// Core configuration file processing class
    /// </summary>
    public class CoreConfigClash
    {
        private Config _config;

        public CoreConfigClash(Config config)
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
        public int GenerateClientCustomConfig(ProfileItem node, string? fileName, out string msg)
        {
            if (node == null || fileName is null)
            {
                msg = ResUI.CheckServerSettings;
                return -1;
            }

            msg = ResUI.InitialConfiguration;

            try
            {
                if (node == null)
                {
                    msg = ResUI.CheckServerSettings;
                    return -1;
                }

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                string addressFileName = node.address;
                if (Utils.IsNullOrEmpty(addressFileName))
                {
                    msg = ResUI.FailedGetDefaultConfiguration;
                    return -1;
                }
                if (!File.Exists(addressFileName))
                {
                    addressFileName = Path.Combine(Utils.GetConfigPath(), addressFileName);
                }
                if (!File.Exists(addressFileName))
                {
                    msg = ResUI.FailedReadConfiguration + "1";
                    return -1;
                }

                string tagYamlStr1 = "!<str>";
                string tagYamlStr2 = "__strn__";
                string tagYamlStr3 = "!!str";
                var txtFile = File.ReadAllText(addressFileName);
                txtFile = txtFile.Replace(tagYamlStr1, tagYamlStr2);

                //YAML anchors
                if (txtFile.Contains("<<:") && txtFile.Contains("*") && txtFile.Contains("&"))
                {
                    txtFile = YamlUtils.PreprocessYaml(txtFile);
                }

                var fileContent = YamlUtils.FromYaml<Dictionary<string, object>>(txtFile);
                if (fileContent == null)
                {
                    msg = ResUI.FailedConversionConfiguration;
                    return -1;
                }

                //port
                fileContent["port"] = LazyConfig.Instance.GetLocalPort(EInboundProtocol.http);
                //socks-port
                fileContent["socks-port"] = LazyConfig.Instance.GetLocalPort(EInboundProtocol.socks);
                //log-level
                fileContent["log-level"] = GetLogLevel(_config.coreBasicItem.loglevel);

                //external-controller
                fileContent["external-controller"] = $"{Global.Loopback}:{LazyConfig.Instance.StatePort2}";
                //allow-lan
                if (_config.inbound[0].allowLANConn)
                {
                    fileContent["allow-lan"] = "true";
                    fileContent["bind-address"] = "*";
                }
                else
                {
                    fileContent["allow-lan"] = "false";
                }

                //ipv6
                fileContent["ipv6"] = _config.clashUIItem.enableIPv6;

                //mode
                if (!fileContent.ContainsKey("mode"))
                {
                    fileContent["mode"] = ERuleMode.Rule.ToString().ToLower();
                }
                else
                {
                    if (_config.clashUIItem.ruleMode != ERuleMode.Unchanged)
                    {
                        fileContent["mode"] = _config.clashUIItem.ruleMode.ToString().ToLower();
                    }
                }

                //enable tun mode
                if (_config.tunModeItem.enableTun)
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
                File.WriteAllText(fileName, txtFileNew);
                //check again
                if (!File.Exists(fileName))
                {
                    msg = ResUI.FailedReadConfiguration + "2";
                    return -1;
                }

                ClashApiHandler.Instance.ProfileContent = fileContent;

                msg = string.Format(ResUI.SuccessfulConfiguration, $"{node.GetSummary()}");
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GenerateClientConfigClash", ex);
                msg = ResUI.FailedGenDefaultConfiguration;
                return -1;
            }
            return 0;
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
                if (!_config.tunModeItem.enableTun && item.Key == "tun")
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