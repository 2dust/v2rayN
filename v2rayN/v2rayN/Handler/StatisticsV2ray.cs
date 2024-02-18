﻿using Grpc.Core;
using Grpc.Net.Client;
using ProtosLib.Statistics;
using v2rayN.Model;

namespace v2rayN.Handler
{
    internal class StatisticsV2ray
    {
        private Model.Config _config;
        private GrpcChannel? _channel;
        private StatsService.StatsServiceClient? _client;
        private bool _exitFlag;
        private Action<ServerSpeedItem> _updateFunc;

        public StatisticsV2ray(Model.Config config, Action<ServerSpeedItem> update)
        {
            _config = config;
            _updateFunc = update;
            _exitFlag = false;

            GrpcInit();

            Task.Run(Run);
        }

        private void GrpcInit()
        {
            if (_channel is null)
            {
                try
                {
                    _channel = GrpcChannel.ForAddress($"{Global.HttpProtocol}{Global.Loopback}:{LazyConfig.Instance.StatePort}");
                    _client = new StatsService.StatsServiceClient(_channel);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(ex.Message, ex);
                }
            }
        }

        public void Close()
        {
            _exitFlag = true;
        }

        private async void Run()
        {
            while (!_exitFlag)
            {
                try
                {
                    if (_channel?.State == ConnectivityState.Ready)
                    {
                        QueryStatsResponse? res = null;
                        try
                        {
                            res = await _client.QueryStatsAsync(new QueryStatsRequest() { Pattern = "", Reset = true });
                        }
                        catch
                        {
                        }

                        if (res != null)
                        {
                            ParseOutput(res.Stat, out ServerSpeedItem server);
                            _updateFunc(server);
                        }
                    }
                    await Task.Delay(1000);
                    if (_channel != null) await _channel.ConnectAsync();
                }
                catch
                {
                }
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

                    if (name == Global.ProxyTag)
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
                    else if (name == Global.DirectTag)
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
            catch
            {
            }
        }
    }
}