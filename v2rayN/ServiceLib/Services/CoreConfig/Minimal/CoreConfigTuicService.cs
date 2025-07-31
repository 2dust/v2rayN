using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigTuicService(Config config) : CoreConfigServiceMinimalBase(config)
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
                ["server"] = Global.Loopback + ":" + port.ToString()
            };

            // outbound
            var alpn = new JsonArray();
            foreach(var item in node.GetAlpn() ?? new List<string>())
            {
                alpn.Add(item);
            }
            if (alpn.Count == 0)
            {
                alpn.Add("h3");
            }

            configJsonNode["relay"] = new JsonObject
            {
                ["server"] = node.Address + ":" + node.Port,
                ["uuid"] = node.Id,
                ["password"] = node.Security,
                ["udp_relay_mode"] = "quic",
                ["congestion_control"] = node.HeaderType,
                ["alpn"] = alpn
            };

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
