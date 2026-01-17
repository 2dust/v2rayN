using NLog;
using NLog.Config;
using NLog.Targets;

namespace ServiceLib.Common;

public class Logging
{
    private static readonly Logger _logger1 = LogManager.GetLogger("Log1");
    private static readonly Logger _logger2 = LogManager.GetLogger("Log2");

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
        if (!LogManager.IsLoggingEnabled())
        {
            return;
        }

        _logger1.Info(strContent);
    }

    public static void SaveLog(string strTitle, Exception ex)
    {
        if (!LogManager.IsLoggingEnabled())
        {
            return;
        }

        _logger2.Debug($"{strTitle},{ex.Message}");
        _logger2.Debug(ex.StackTrace);
        if (ex?.InnerException is not null)
        {
            _logger2.Error(ex.InnerException);
        }
    }
}
