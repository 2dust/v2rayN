namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigBrookService
{
    private Config _config;
    private static readonly string _tag = "CoreConfigBrookService";

    public CoreConfigBrookService(Config config)
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

            if (node.ConfigType != EConfigType.Brook)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var processArgs = "client";

            // inbound
            processArgs += " --socks5 " + Global.Loopback + ":" + AppHandler.Instance.GetLocalPort(EInboundProtocol.split).ToString();

            // outbound
            processArgs += " --server " + node.Address + ":" + node.Port;
            processArgs += " --password " + node.Id;

            ret.Data = processArgs;

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
