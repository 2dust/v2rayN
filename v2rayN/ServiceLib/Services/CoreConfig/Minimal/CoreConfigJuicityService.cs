using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigJuicityService
{
    private Config _config;
    private static readonly string _tag = "CoreConfigJuicityService";

    public CoreConfigJuicityService(Config config)
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

            //if (node.ConfigType != EConfigType.Juicity)
            //{
            //    ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
            //    return ret;
            //}

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
            configJsonNode["listen"] = ":" + AppHandler.Instance.GetLocalPort(EInboundProtocol.split).ToString();

            // outbound
            configJsonNode["server"] = node.Address + ":" + node.Port;
            configJsonNode["uuid"] = node.Id;
            configJsonNode["password"] = node.Security;
            if (node.Sni.IsNotEmpty())
            {
                configJsonNode["sni"] = node.Sni;
            }
            configJsonNode["allow_insecure"] = node.AllowInsecure == "true";
            configJsonNode["congestion_control"] = "bbr";

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
