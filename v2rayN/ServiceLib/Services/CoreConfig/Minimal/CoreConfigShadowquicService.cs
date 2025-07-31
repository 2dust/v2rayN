using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigShadowquicService(Config config) : CoreConfigServiceMinimalBase(config)
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

            if (node.ConfigType != EConfigType.Shadowquic)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var configYamlNode = new Dictionary<string, object>();

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
            configYamlNode["log-level"] = logLevel;

            // inbound
            var inboundNode = new Dictionary<string, object>
            {
                ["type"] = "socks5",
                ["listen"] = Global.Loopback + ":" + port.ToString()
            };
            configYamlNode["inbound"] = inboundNode;

            // outbound
            var alpn = new List<string>();
            foreach (var item in node.GetAlpn() ?? new List<string>())
            {
                alpn.Add(item);
            }
            if (alpn.Count == 0)
            {
                alpn.Add("h3");
            }

            var outboundNode = new Dictionary<string, object>
            {
                ["type"] = "shadowquic",
                ["addr"] = node.Address + ":" + node.Port,
                ["password"] = node.Id,
                ["username"] = node.Security,
                ["alpn"] = alpn,
                ["congestion-control"] = node.HeaderType,
                ["zero-rtt"] = true
            };
            if (node.Sni.IsNotEmpty())
            {
                outboundNode["server-name"] = node.Sni;
            }
            configYamlNode["outbound"] = outboundNode;

            ret.Success = true;
            ret.Data = YamlUtils.ToYaml(configYamlNode);

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
