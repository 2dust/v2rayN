using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using v2rayN.Mode;
using v2rayN.Protos.Statistics;

namespace v2rayN.Handler
{
    class StatisticsHandler
    {
        private Mode.Config config_;
        private ServerStatistics serverStatistics_;
        private Channel channel_;
        private StatsService.StatsServiceClient client_;
        private bool exitFlag_;

        Action<ulong, ulong, List<ServerStatItem>> updateFunc_;

        public bool Enable
        {
            get; set;
        }

        public bool UpdateUI
        {
            get; set;
        }


        public List<ServerStatItem> Statistic => serverStatistics_.server;

        public StatisticsHandler(Mode.Config config, Action<ulong, ulong, List<ServerStatItem>> update)
        {
            //try
            //{
            //    if (Environment.Is64BitOperatingSystem)
            //    {
            //        FileManager.UncompressFile(Utils.GetPath("grpc_csharp_ext.x64.dll"), Resources.grpc_csharp_ext_x64_dll);
            //    }
            //    else
            //    {
            //        FileManager.UncompressFile(Utils.GetPath("grpc_csharp_ext.x86.dll"), Resources.grpc_csharp_ext_x86_dll);
            //    }
            //}
            //catch (IOException ex)
            //{
            //    Utils.SaveLog(ex.Message, ex);

            //}

            config_ = config;
            Enable = config.enableStatistics;
            UpdateUI = false;
            updateFunc_ = update;
            exitFlag_ = false;

            LoadFromFile();

            GrpcInit();

            Task.Run(Run);
        }

        private void GrpcInit()
        {
            if (channel_ == null)
            {
                Global.statePort = GetFreePort();

                channel_ = new Channel($"{Global.Loopback}:{Global.statePort}", ChannelCredentials.Insecure);
                channel_.ConnectAsync();
                client_ = new StatsService.StatsServiceClient(channel_);
            }
        }

        public void Close()
        {
            try
            {
                exitFlag_ = true;
                channel_.ShutdownAsync();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void Run()
        {
            while (!exitFlag_)
            {
                try
                {
                    if (Enable && channel_.State == ChannelState.Ready)
                    {
                        QueryStatsResponse res = null;
                        try
                        {
                            res = client_.QueryStats(new QueryStatsRequest() { Pattern = "", Reset = true });
                        }
                        catch (Exception ex)
                        {
                            //Utils.SaveLog(ex.Message, ex);
                        }

                        if (res != null)
                        {
                            string itemId = config_.indexId;
                            ServerStatItem serverStatItem = GetServerStatItem(itemId);

                            //TODO: parse output
                            ParseOutput(res.Stat, out ulong up, out ulong down);

                            serverStatItem.todayUp += up;
                            serverStatItem.todayDown += down;
                            serverStatItem.totalUp += up;
                            serverStatItem.totalDown += down;

                            if (UpdateUI)
                            {
                                updateFunc_(up, down, new List<ServerStatItem> { serverStatItem });
                            }
                        }
                    }
                    Thread.Sleep(config_.statisticsFreshRate);
                    channel_.ConnectAsync();
                }
                catch (Exception ex)
                {
                    //Utils.SaveLog(ex.Message, ex);
                }
            }
        }

        public void LoadFromFile()
        {
            try
            {
                string result = Utils.LoadResource(Utils.GetPath(Global.StatisticLogOverall));
                if (!Utils.IsNullOrEmpty(result))
                {
                    //转成Json
                    serverStatistics_ = Utils.FromJson<ServerStatistics>(result);
                }

                if (serverStatistics_ == null)
                {
                    serverStatistics_ = new ServerStatistics();
                }
                if (serverStatistics_.server == null)
                {
                    serverStatistics_.server = new List<ServerStatItem>();
                }

                long ticks = DateTime.Now.Date.Ticks;
                foreach (ServerStatItem item in serverStatistics_.server)
                {
                    if (item.dateNow != ticks)
                    {
                        item.todayUp = 0;
                        item.todayDown = 0;
                        item.dateNow = ticks;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void SaveToFile()
        {
            try
            {
                Utils.ToJsonFile(serverStatistics_, Utils.GetPath(Global.StatisticLogOverall));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void ClearAllServerStatistics()
        {
            if (serverStatistics_ != null)
            {
                foreach (var item in serverStatistics_.server)
                {
                    item.todayUp = 0;
                    item.todayDown = 0;
                    item.totalUp = 0;
                    item.totalDown = 0;
                    // update ui display to zero
                    updateFunc_(0, 0, new List<ServerStatItem> { item });
                }

                // update statistic json file
                SaveToFile();
            }
        }

        private ServerStatItem GetServerStatItem(string itemId)
        {
            long ticks = DateTime.Now.Date.Ticks;
            int cur = Statistic.FindIndex(item => item.itemId == itemId);
            if (cur < 0)
            {
                Statistic.Add(new ServerStatItem
                {
                    itemId = itemId,
                    totalUp = 0,
                    totalDown = 0,
                    todayUp = 0,
                    todayDown = 0,
                    dateNow = ticks
                });
                cur = Statistic.Count - 1;
            }
            if (Statistic[cur].dateNow != ticks)
            {
                Statistic[cur].todayUp = 0;
                Statistic[cur].todayDown = 0;
                Statistic[cur].dateNow = ticks;
            }
            return Statistic[cur];
        }

        private void ParseOutput(Google.Protobuf.Collections.RepeatedField<Stat> source, out ulong up, out ulong down)
        {

            up = 0; down = 0;
            try
            {

                foreach (Stat stat in source)
                {
                    string name = stat.Name;
                    long value = stat.Value;
                    string[] nStr = name.Split(">>>".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    string type = "";

                    name = name.Trim();

                    name = nStr[1];
                    type = nStr[3];

                    if (name == Global.agentTag)
                    {
                        if (type == "uplink")
                        {
                            up = (ulong)value;
                        }
                        else if (type == "downlink")
                        {
                            down = (ulong)value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Utils.SaveLog(ex.Message, ex);
            }
        }

        private int GetFreePort()
        {
            int defaultPort = 28123;
            try
            {
                // TCP stack please do me a favor
                TcpListener l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch (Exception ex)
            {
                // in case access denied
                Utils.SaveLog(ex.Message, ex);
                return defaultPort;
            }
        }
    }
}
