using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.IO;
using System.Threading.Tasks;

namespace v2rayN.Tool
{
    public class Logging
    {
        public static void Setup()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();

            var patternLayout = new PatternLayout
            {
                ConversionPattern = "%date [%thread] %-5level %logger - %message%newline"
            };
            patternLayout.ActivateOptions();

            var roller = new RollingFileAppender
            {
                AppendToFile = true,
                RollingStyle = RollingFileAppender.RollingMode.Date,
                DatePattern = "yyyy-MM-dd'.txt'",
                File = Utils.GetPath(@"guiLogs\"),
                Layout = patternLayout,
                StaticLogFileName = false
            };
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            var memory = new MemoryAppender();
            memory.ActivateOptions();
            hierarchy.Root.AddAppender(memory);

            hierarchy.Root.Level = Level.Debug;
            hierarchy.Configured = true;
        }

        public static void ClearLogs()
        {
            Task.Run(() =>
            {
                try
                {
                    var now = DateTime.Now.AddMonths(-1);
                    var dir = Utils.GetPath(@"guiLogs\");
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
    }
}
