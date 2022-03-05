using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace v2rayN.Tool
{
    public class Logging
    {
        public static void Setup()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
            patternLayout.ActivateOptions();
          
            RollingFileAppender roller = new RollingFileAppender();
            roller.AppendToFile = true;
            roller.RollingStyle = RollingFileAppender.RollingMode.Date;
            roller.DatePattern = "yyyy-MM-dd'.txt'";
            roller.File = Utils.GetPath(@"guiLogs\");
            roller.Layout = patternLayout;
            roller.StaticLogFileName = false;
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            MemoryAppender memory = new MemoryAppender();
            memory.ActivateOptions();
            hierarchy.Root.AddAppender(memory);

            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }
    }
}
