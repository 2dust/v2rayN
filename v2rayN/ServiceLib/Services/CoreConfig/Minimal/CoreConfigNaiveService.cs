using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigNaiveService(Config config) : CoreConfigServiceMinimalBase(config)
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

            if (node.ConfigType != EConfigType.NaiveProxy)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var configJsonNode = new JsonObject();

            // inbound
            configJsonNode["listen"] = Global.SocksProtocol + Global.Loopback + ":" + port.ToString();

            // outbound
            configJsonNode["proxy"] = (node.HeaderType == "quic" ? "quic://" : Global.HttpsProtocol) + node.Id + "@" + node.Address + ":" + node.Port;

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
