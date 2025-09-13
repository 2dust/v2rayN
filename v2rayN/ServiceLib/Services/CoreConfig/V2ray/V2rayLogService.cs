namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenLog(V2rayConfig v2rayConfig)
    {
        try
        {
            if (_config.CoreBasicItem.LogEnabled)
            {
                var dtNow = DateTime.Now;
                v2rayConfig.log.loglevel = _config.CoreBasicItem.Loglevel;
                v2rayConfig.log.access = Utils.GetLogPath($"Vaccess_{dtNow:yyyy-MM-dd}.txt");
                v2rayConfig.log.error = Utils.GetLogPath($"Verror_{dtNow:yyyy-MM-dd}.txt");
            }
            else
            {
                v2rayConfig.log.loglevel = _config.CoreBasicItem.Loglevel;
                v2rayConfig.log.access = null;
                v2rayConfig.log.error = null;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return await Task.FromResult(0);
    }
}
