using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigJuicityService(Config config) : CoreConfigServiceMinimalBase(config)
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

            if (node.ConfigType != EConfigType.Juicity)
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
            configJsonNode["listen"] = ":" + port.ToString();

            // outbound
            configJsonNode["server"] = node.Address + ":" + node.Port;
            configJsonNode["uuid"] = node.Id;
            configJsonNode["password"] = node.Security;
            if (node.Sni.IsNotEmpty())
            {
                configJsonNode["sni"] = node.Sni;
            }
            configJsonNode["allow_insecure"] = node.AllowInsecure == "true";
            configJsonNode["congestion_control"] = node.HeaderType;

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
