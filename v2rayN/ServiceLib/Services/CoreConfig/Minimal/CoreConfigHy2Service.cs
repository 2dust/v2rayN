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
}
