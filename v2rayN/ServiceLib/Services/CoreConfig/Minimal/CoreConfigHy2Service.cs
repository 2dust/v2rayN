using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigHy2Service(Config config) : CoreConfigServiceMinimalBase(config)
{
    protected override async Task<RetResult> GeneratePassthroughConfig(ProfileItem node, int port)
    {
        var ret = new RetResult();
        try
        {
            if (node == null
                || node.Port <= 0)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (node.ConfigType != EConfigType.Hysteria2)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var configJsonNode = new JsonObject();

            // inbound
            configJsonNode["socks5"] = new JsonObject
            {
                ["listen"] = Global.Loopback + ":" + port.ToString()
            };

            // outbound
            var outboundPort = string.Empty;
            if (node.Ports.IsNotEmpty())
            {
                outboundPort = node.Ports.Replace(':', '-');
                if (_config.HysteriaItem.HopInterval > 0)
                {
                    configJsonNode["transport"] = new JsonObject
                    {
                        ["udp"] = new JsonObject
                        {
                            ["hopInterval"] = $"{_config.HysteriaItem.HopInterval}s"
                        }
                    };
                }
            }
            else
            {
                outboundPort = node.Port.ToString();
            }
            configJsonNode["server"] = node.Address + ":" + outboundPort;
            configJsonNode["auth"] = node.Id;

            if (node.Sni.IsNotEmpty())
            {
                configJsonNode["tls"] = new JsonObject
                {
                    ["sni"] = node.Sni,
                    ["insecure"] = node.AllowInsecure.ToLower() == "true"
                };
            }

            if (node.Path.IsNotEmpty())
            {
                configJsonNode["obfs"] = new JsonObject
                {
                    ["type"] = "salamander ",
                    ["salamander"] = new JsonObject
                    {
                        ["password"] = node.Path
                    }
                };
            }

            var bandwidthObject = new JsonObject();
            if (_config.HysteriaItem.UpMbps > 0)
            {
                bandwidthObject["up"] = $"{_config.HysteriaItem.UpMbps} mbps";
            }
            if (_config.HysteriaItem.DownMbps > 0)
            {
                bandwidthObject["down"] = $"{_config.HysteriaItem.DownMbps} mbps";
            }
            if (bandwidthObject.Count > 0)
            {
                configJsonNode["bandwidth"] = bandwidthObject;
            }

            ret.Success = true;
            ret.Data = JsonUtils.Serialize(configJsonNode, true);

            return await Task.FromResult(ret);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return await Task.FromResult(ret);
        }
    }

    public override async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
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

            string addressFileName = node.Address;
            if (!File.Exists(addressFileName))
            {
                addressFileName = Utils.GetConfigPath(addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            // Try deserializing the file to check if it is a valid JSON or YAML file
            var fileContent = File.ReadAllText(addressFileName);
            var jsonContent = JsonUtils.Deserialize<JsonObject>(fileContent);
            if (jsonContent != null)
            {
                File.Copy(addressFileName, fileName);
            }
            else
            {
                // If it's YAML, convert to JSON and write it
                var yamlContent = YamlUtils.FromYaml<Dictionary<string, object>>(fileContent);
                if (yamlContent != null)
                {
                    File.WriteAllText(fileName, JsonUtils.Serialize(yamlContent, true));
                }
                else
                {
                    ret.Msg = ResUI.FailedReadConfiguration + "2";
                    return ret;
                }
            }
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
}
