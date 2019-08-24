using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

using v2rayN.Mode;

namespace v2rayN.Handler
{
    class StatisticsHandler
    {
        private Config config_;
        private const string cliName_ = "v2ctl.exe";
        private string args_ = "";

        private Process connector_;

        private Thread workThread_;

        Action<ulong, ulong, ulong, ulong> updateFunc_;

        private bool enabled_;
        public bool Enable 
        {
            get { return enabled_; }
            set { enabled_ = value; }
        }

        public bool UpdateUI;

        private StringBuilder outputBuilder_;

        public UInt64 TotalUp { get; private set; }
       
        public UInt64 TotalDown { get; private set; }

        public UInt64 Up { get; private set; }

        public UInt64 Down { get; private set; }

        public StatisticsHandler(Config config, Action<ulong, ulong, ulong, ulong> update)
        {
            config_ = config;
            enabled_ = config.enableStatistics;
            UpdateUI = false;
            updateFunc_ = update;

            outputBuilder_ = new StringBuilder();

            var fullPath = Utils.GetPath(cliName_);

            if (!File.Exists(fullPath))
            {
                connector_ = null;
                return;
            }

            // .\v2ctl.exe api --server="127.0.0.1:port" StatsService.QueryStats "reset:true"
            args_ = string.Format("api --server=\"127.0.0.1:{0}\" StatsService.QueryStats \"reset:true\"", Global.InboundAPIPort);

            connector_ = new Process();
            connector_.StartInfo.FileName = fullPath;
            connector_.StartInfo.Arguments = args_;
            connector_.StartInfo.RedirectStandardOutput = true;
            connector_.StartInfo.UseShellExecute = false;
            connector_.StartInfo.CreateNoWindow = true;
            

            workThread_ = new Thread(new ThreadStart(run));
            workThread_.Start();
        }

        public void run()
        {
            try
            {
                while (true)
                {
                    if (enabled_)
                    {
                        connector_.Start();
                        string output = connector_.StandardOutput.ReadToEnd();
                        UInt64 up = 0;
                        UInt64 down = 0;

                        //TODO: parse output
                        parseOutput(output, out up, out down);

                        Up = up;
                        Down = down;

                        TotalUp += up;
                        TotalDown += down;

                        if (UpdateUI)
                            updateFunc_(TotalUp, TotalDown, Up, Down);
                        Thread.Sleep(config_.statisticsFreshRate);
                    }
                }
            }
            catch (Exception e)
            {  }
        }

        public void parseOutput(string source, out UInt64 up, out UInt64 down)
        {
            // (?<=name: ")(.*?)(?=")|(?<=value: )(.*?)
            var datas = Regex.Matches(source, "(?<=name: \")(?<name>.*?)(?=\").*?(?<=value: )(?<value>.*?)(?=>)", RegexOptions.Singleline);

            up = 0; down = 0;

            foreach(Match match in datas)
            {
                var g = match.Groups;
                var name = g["name"].Value;
                var value = g["value"].Value;
                var nStr = name.Split(">>>".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                var type = "";

                name = name.Trim();
                value = value.Trim();

                name = nStr[1];
                type = nStr[3];

                try
                {
                    if (name == Global.InboundProxyTagName)
                    {
                        if (type == "uplink")
                        {
                            up = UInt64.Parse(value);
                        }
                        else if (type == "downlink")
                        {
                            down = UInt64.Parse(value);
                        }
                    }
                }
                catch { }
            }
        }

        public void saveToFile()
        {

        }

        public void loadFromFile()
        {

        }
    }
}
