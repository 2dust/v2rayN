namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigShadowquicService
{
    private Config _config;
    private static readonly string _tag = "CoreConfigShadowquicService";

    public CoreConfigShadowquicService(Config config)
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
                ["listen"] = Global.Loopback + ":" + AppHandler.Instance.GetLocalPort(EInboundProtocol.split).ToString()
            };
            configYamlNode["inbound"] = inboundNode;

            // outbound
            var outboundNode = new Dictionary<string, object>
            {
                ["type"] = "shadowquic",
                ["addr"] = node.Address + ":" + node.Port,
                ["password"] = node.Id,
                ["username"] = node.Security,
                ["alpn"] = new List<string> { "h3" },
                ["congestion-control"] = "bbr",
                ["zero-rtt"] = true
            };
            if (node.Sni.IsNotEmpty())
            {
                outboundNode["server-name"] = node.Sni;
            }
            configYamlNode["outbound"] = outboundNode;

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
