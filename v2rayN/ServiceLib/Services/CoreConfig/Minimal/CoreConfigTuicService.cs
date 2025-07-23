using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigTuicService
{
    private Config _config;
    private static readonly string _tag = "CoreConfigTuicService";

    public CoreConfigTuicService(Config config)
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

            if (node.ConfigType != EConfigType.TUIC)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var configJsonNode = new JsonObject();

            // log
            var logLevel = string.Empty;
            switch (_config.CoreBasicItem.Loglevel)
            {
                case "warning":
                    logLevel = "warn";
                    break;
                default:
                    logLevel = _config.CoreBasicItem.Loglevel;
                    break;
            }
            configJsonNode["log_level"] = logLevel;

            // inbound
            configJsonNode["local"] = new JsonObject
            {
                ["server"] = Global.Loopback + ":" + AppHandler.Instance.GetLocalPort(EInboundProtocol.split).ToString()
            };

            // outbound
            configJsonNode["relay"] = new JsonObject
            {
                ["server"] = node.Address + ":" + node.Port,
                ["uuid"] = node.Id,
                ["password"] = node.Security,
                ["udp_relay_mode"] = "quic",
                ["congestion_control"] = "bbr",
                ["alpn"] = new JsonArray { "h3", "spdy/3.1" }
            };

            ret.Data = configJsonNode.ToJsonString(new() { WriteIndented = true });

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
