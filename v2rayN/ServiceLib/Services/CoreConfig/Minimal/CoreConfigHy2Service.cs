using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigHy2Service
{
    private Config _config;
    private static readonly string _tag = "CoreConfigHy2Service";

    public CoreConfigHy2Service(Config config)
    {
        _config = config;
    }

    public async Task<RetResult> GeneratePureEndpointConfig(ProfileItem node)
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
                ["listen"] = Global.Loopback + ":" + AppHandler.Instance.GetLocalPort(EInboundProtocol.split).ToString()
            };

            // outbound
            var port = string.Empty;
            if (node.Ports.IsNotEmpty())
            {
                port = node.Ports.Replace(':', '-');
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
                port = node.Port.ToString();
            }
            configJsonNode["server"] = node.Address + ":" + port;
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
