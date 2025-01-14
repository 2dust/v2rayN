using NLog;
using NLog.Config;
using NLog.Targets;

namespace ServiceLib.Common
{
    public class Logging
    {
        public static void Setup()
        {
            LoggingConfiguration config = new();
            FileTarget fileTarget = new();
            config.AddTarget("file", fileTarget);
            fileTarget.Layout = "${longdate}-${level:uppercase=true} ${message}";
            fileTarget.FileName = Utils.GetLogPath("${shortdate}.txt");
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));
            LogManager.Configuration = config;
        }

        public static void LoggingEnabled(bool enable)
        {
            if (!enable)
            {
                LogManager.SuspendLogging();
            }
        }

        public static void SaveLog(string strContent)
        {
            if (!LogManager.IsLoggingEnabled()) return;

            LogManager.GetLogger("Log1").Info(strContent);
        }

        public static void SaveLog(string strTitle, Exception ex)
        {
            if (!LogManager.IsLoggingEnabled()) return;

            var logger = LogManager.GetLogger("Log2");
            logger.Debug($"{strTitle},{ex.Message}");
            logger.Debug(ex.StackTrace);
            if (ex?.InnerException != null)
            {
                logger.Error(ex.InnerException);
            }
        }
    }
}