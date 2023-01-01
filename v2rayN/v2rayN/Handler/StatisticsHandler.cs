using Grpc.Core;
using ProtosLib.Statistics;
using System.Net;
using System.Net.Sockets;
using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class StatisticsHandler
    {
        private Mode.Config config_;
        private Channel channel_;
        private StatsService.StatsServiceClient client_;
        private bool exitFlag_;
        private ServerStatItem _serverStatItem;

        Action<ServerSpeedItem> updateFunc_;

        public bool Enable
        {
            get; set;
        }

        public StatisticsHandler(Mode.Config config, Action<ServerSpeedItem> update)
        {
            config_ = config;
            Enable = config.enableStatistics;
            updateFunc_ = update;
            exitFlag_ = false;

            Init();
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
                            GetServerStatItem(config_.indexId);
                            ParseOutput(res.Stat, out ServerSpeedItem server);

                            _serverStatItem.todayUp += server.proxyUp;
                            _serverStatItem.todayDown += server.proxyDown;
                            _serverStatItem.totalUp += server.proxyUp;
                            _serverStatItem.totalDown += server.proxyDown;

                            if (Global.ShowInTaskbar)
                            {
                                server.indexId = config_.indexId;
                                updateFunc_(server);
                            }
                            if (server.proxyUp != 0 || server.proxyDown != 0)
                            {
                                _ = SqliteHelper.Instance.UpdateAsync(_serverStatItem);
                            }

                        }
                    }
                    var sleep = config_.statisticsFreshRate < 1 ? 1 : config_.statisticsFreshRate;
                    Thread.Sleep(1000 * sleep);
                    channel_.ConnectAsync();
                }
                catch
                {
                }
            }
        }

        public void ClearAllServerStatistics()
        {
            SqliteHelper.Instance.Execute($"delete from ServerStatItem ");
            _serverStatItem = null;
        }

        private void Init()
        {
            long ticks = DateTime.Now.Date.Ticks;
            SqliteHelper.Instance.Execute($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");
        }

        private void GetServerStatItem(string indexId)
        {
            long ticks = DateTime.Now.Date.Ticks;
            if (_serverStatItem != null && _serverStatItem.indexId != indexId)
            {
                _serverStatItem = null;
            }

            if (_serverStatItem == null)
            {
                _serverStatItem = SqliteHelper.Instance.Table<ServerStatItem>().FirstOrDefault(t => t.indexId == indexId);
                if (_serverStatItem == null)
                {
                    _serverStatItem = new ServerStatItem
                    {
                        indexId = indexId,
                        totalUp = 0,
                        totalDown = 0,
                        todayUp = 0,
                        todayDown = 0,
                        dateNow = ticks
                    };
                    _ = SqliteHelper.Instance.Replacesync(_serverStatItem);
                }
            }

            if (_serverStatItem.dateNow != ticks)
            {
                _serverStatItem.todayUp = 0;
                _serverStatItem.todayDown = 0;
                _serverStatItem.dateNow = ticks;
            }
        }

        private void ParseOutput(Google.Protobuf.Collections.RepeatedField<Stat> source, out ServerSpeedItem server)
        {
            server = new();
            try
            {

                foreach (Stat stat in source)
                {
                    string name = stat.Name;
                    long value = stat.Value / 1024;    //KByte
                    string[] nStr = name.Split(">>>".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    string type = "";

                    name = name.Trim();

                    name = nStr[1];
                    type = nStr[3];

                    if (name == Global.agentTag)
                    {
                        if (type == "uplink")
                        {
                            server.proxyUp = value;
                        }
                        else if (type == "downlink")
                        {
                            server.proxyDown = value;
                        }
                    }
                    else if (name == Global.directTag)
                    {
                        if (type == "uplink")
                        {
                            server.directUp = value;
                        }
                        else if (type == "downlink")
                        {
                            server.directDown = value;
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
