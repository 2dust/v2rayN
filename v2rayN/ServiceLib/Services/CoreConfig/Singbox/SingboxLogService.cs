namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<int> GenLog(SingboxConfig singboxConfig)
    {
        try
        {
            switch (_config.CoreBasicItem.Loglevel)
            {
                case "debug":
                case "info":
                case "error":
                    singboxConfig.log.level = _config.CoreBasicItem.Loglevel;
                    break;

                case "warning":
                    singboxConfig.log.level = "warn";
                    break;

                default:
                    break;
            }
            if (_config.CoreBasicItem.Loglevel == Global.None)
            {
                singboxConfig.log.disabled = true;
            }
            if (_config.CoreBasicItem.LogEnabled)
            {
                var dtNow = DateTime.Now;
                singboxConfig.log.output = Utils.GetLogPath($"sbox_{dtNow:yyyy-MM-dd}.txt");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }
}
