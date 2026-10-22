namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenLog(bool isSpeedtest = false)
    {
        try
        {
            // Speedtest cores run without elevation and cannot share log files created by a root TUN core.
            if (_config.CoreBasicItem.LogEnabled && !isSpeedtest)
            {
                var dtNow = DateTime.Now;
                _coreConfig.log.loglevel = _config.CoreBasicItem.Loglevel;
                _coreConfig.log.access = Utils.GetLogPath($"Vaccess_{dtNow:yyyy-MM-dd}.txt");
                _coreConfig.log.error = Utils.GetLogPath($"Verror_{dtNow:yyyy-MM-dd}.txt");
            }
            else
            {
                _coreConfig.log.loglevel = _config.CoreBasicItem.Loglevel;
                _coreConfig.log.access = null;
                _coreConfig.log.error = null;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
