namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private void GenLog()
    {
        try
        {
            switch (context.AppConfig.CoreBasicItem.Loglevel)
            {
                case "debug":
                case "info":
                case "error":
                    _coreConfig.log.level = context.AppConfig.CoreBasicItem.Loglevel;
                    break;

                case "warning":
                    _coreConfig.log.level = "warn";
                    break;

                default:
                    break;
            }
            if (context.AppConfig.CoreBasicItem.Loglevel == Global.None)
            {
                _coreConfig.log.disabled = true;
            }
            if (context.AppConfig.CoreBasicItem.LogEnabled)
            {
                var dtNow = DateTime.Now;
                _coreConfig.log.output = Utils.GetLogPath($"sbox_{dtNow:yyyy-MM-dd}.txt");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
