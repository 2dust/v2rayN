using Grpc.Core;
using Grpc.Net.Client;
using ProtosLib.Statistics;
using System.Net;
using System.Net.Sockets;
using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    internal class StatisticsHandler
    {
        private Mode.Config config_;
        private GrpcChannel _channel;
        private StatsService.StatsServiceClient _client;
        private bool _exitFlag;
        private ServerStatItem? _serverStatItem;
        private List<ServerStatItem> _lstServerStat;
        public List<ServerStatItem> ServerStat => _lstServerStat;

        private Action<ServerSpeedItem> _updateFunc;

        public bool Enable
        {
            get; set;
        }

        public StatisticsHandler(Mode.Config config, Action<ServerSpeedItem> update)
        {
            config_ = config;
            Enable = config.guiItem.enableStatistics;
            _updateFunc = update;
            _exitFlag = false;

            Init();
            GrpcInit();

            Task.Run(Run);
        }

        private void GrpcInit()
        {
            if (_channel == null)
            {
                Global.statePort = GetFreePort();

                _channel = GrpcChannel.ForAddress($"{Global.httpProtocol}{Global.Loopback}:{Global.statePort}");
                _client = new StatsService.StatsServiceClient(_channel);
            }
        }

        public void Close()
        {
            try
            {
                _exitFlag = true;
                //channel_.ShutdownAsync();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public async void Run()
        {
            while (!_exitFlag)
            {
                try
                {
                    if (Enable && _channel.State == ConnectivityState.Ready)
                    {
                        QueryStatsResponse? res = null;
                        try
                        {
                            res = await _client.QueryStatsAsync(new QueryStatsRequest() { Pattern = "", Reset = true });
                        }
                        catch (Exception ex)
                        {
                            //Utils.SaveLog(ex.Message, ex);
                        }

                        if (res != null)
                        {
                            GetServerStatItem(config_.indexId);
                            ParseOutput(res.Stat, out ServerSpeedItem server);

                            if (server.proxyUp != 0 || server.proxyDown != 0)
                            {
                                _serverStatItem.todayUp += server.proxyUp;
                                _serverStatItem.todayDown += server.proxyDown;
                                _serverStatItem.totalUp += server.proxyUp;
                                _serverStatItem.totalDown += server.proxyDown;
                            }
                            if (Global.ShowInTaskbar)
                            {
                                server.indexId = config_.indexId;
                                server.todayUp = _serverStatItem.todayUp;
                                server.todayDown = _serverStatItem.todayDown;
                                server.totalUp = _serverStatItem.totalUp;
                                server.totalDown = _serverStatItem.totalDown;
                                _updateFunc(server);
                            }
                        }
                    }
                    var sleep = config_.guiItem.statisticsFreshRate < 1 ? 1 : config_.guiItem.statisticsFreshRate;
                    Thread.Sleep(1000 * sleep);
                    await _channel.ConnectAsync();
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
            _lstServerStat = new();
        }

        public void SaveTo()
        {
            try
            {
                SqliteHelper.Instance.UpdateAll(_lstServerStat);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private void Init()
        {
            SqliteHelper.Instance.Execute($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

            long ticks = DateTime.Now.Date.Ticks;
            SqliteHelper.Instance.Execute($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

            _lstServerStat = SqliteHelper.Instance.Table<ServerStatItem>().ToList();
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
                _serverStatItem = _lstServerStat.FirstOrDefault(t => t.indexId == indexId);
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
                    SqliteHelper.Instance.Replace(_serverStatItem);
                    _lstServerStat.Add(_serverStatItem);
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
                TcpListener l = new(IPAddress.Loopback, 0);
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