namespace ServiceLib.Services.CoreConfig.Minimal;
public class CoreConfigBrookService(Config config) : CoreConfigServiceMinimalBase(config)
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

            if (node.ConfigType != EConfigType.Brook)
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.ConfigType}";
                return ret;
            }

            var processArgs = "client";

            // inbound
            processArgs += " --socks5 " + Global.Loopback + ":" + port.ToString();

            // outbound
            processArgs += " --server " + node.Address + ":" + node.Port;
            processArgs += " --password " + node.Id;

            ret.Success = true;
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
