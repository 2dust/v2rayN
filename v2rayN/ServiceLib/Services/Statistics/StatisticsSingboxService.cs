﻿using System.Net.WebSockets;
using System.Text;

namespace ServiceLib.Services.Statistics
{
    public class StatisticsSingboxService
    {
        private Config _config;
        private bool _exitFlag;
        private ClientWebSocket? webSocket;
        private Action<ServerSpeedItem>? _updateFunc;
        private string Url => $"ws://{Global.Loopback}:{AppHandler.Instance.StatePort2}/traffic";
        
        public StatisticsSingboxService(Config config, Action<ServerSpeedItem> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;
            _exitFlag = false;

            Task.Run(Run);
        }

        private async void Init()
        {
            await Task.Delay(5000);

            try
            {
                if (webSocket == null)
                {
                    webSocket = new ClientWebSocket();
                    await webSocket.ConnectAsync(new Uri(Url), CancellationToken.None);
                }
            }
            catch { }
        }

        public void Close()
        {
            try
            {
                _exitFlag = true;
                if (webSocket != null)
                {
                    webSocket.Abort();
                    webSocket = null;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private async void Run()
        {
            Init();

            while (!_exitFlag)
            {
                await Task.Delay(1000);
                try
                {
                    if (!_config.IsRunningCore(ECoreType.sing_box))
                    {
                        continue;
                    }
                    if (webSocket != null)
                    {
                        if (webSocket.State == WebSocketState.Aborted
                            || webSocket.State == WebSocketState.Closed)
                        {
                            webSocket.Abort();
                            webSocket = null;
                            Init();
                            continue;
                        }

                        if (webSocket.State != WebSocketState.Open)
                        {
                            continue;
                        }

                        var buffer = new byte[1024];
                        var res = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        while (!res.CloseStatus.HasValue)
                        {
                            var result = Encoding.UTF8.GetString(buffer, 0, res.Count);
                            if (Utils.IsNotEmpty(result))
                            {
                                ParseOutput(result, out ulong up, out ulong down);

                                _updateFunc?.Invoke(new ServerSpeedItem()
                                {
                                    ProxyUp = (long)(up / 1000),
                                    ProxyDown = (long)(down / 1000)
                                });
                            }
                            res = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void ParseOutput(string source, out ulong up, out ulong down)
        {
            up = 0; down = 0;
            try
            {
                var trafficItem = JsonUtils.Deserialize<TrafficItem>(source);
                if (trafficItem != null)
                {
                    up = trafficItem.Up;
                    down = trafficItem.Down;
                }
            }
            catch
            {
            }
        }
    }
}