using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigNaiveService
{
    private Config _config;
    private static readonly string _tag = "CoreConfigNaiveService";

    public CoreConfigNaiveService(Config config)
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

            if (node.ConfigType != EConfigType.NaiveProxy)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var configJsonNode = new JsonObject();

            // inbound
            configJsonNode["listen"] = Global.SocksProtocol + Global.Loopback + ":" + AppHandler.Instance.GetLocalPort(EInboundProtocol.split).ToString();

            // outbound
            configJsonNode["proxy"] = (node.Network == "quic" ? "quic://" : Global.HttpsProtocol) + node.Id + "@" + node.Address + ":" + node.Port;

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
