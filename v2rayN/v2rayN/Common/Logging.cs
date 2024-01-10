using NLog;
using NLog.Config;
using NLog.Targets;
using System.IO;

namespace v2rayN
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

        public static void ClearLogs()
        {
            Task.Run(() =>
            {
                try
                {
                    var now = DateTime.Now.AddMonths(-1);
                    var dir = Utils.GetLogPath();
                    var files = Directory.GetFiles(dir, "*.txt");
                    foreach (var filePath in files)
                    {
                        var file = new FileInfo(filePath);
                        if (file.CreationTime < now)
                        {
                            try
                            {
                                file.Delete();
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            });
        }

        public static void SaveLog(string strContent)
        {
            if (LogManager.IsLoggingEnabled())
            {
                var logger = LogManager.GetLogger("Log1");
                logger.Info(strContent);
            }
        }

        public static void SaveLog(string strTitle, Exception ex)
        {
            if (LogManager.IsLoggingEnabled())
            {
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
}