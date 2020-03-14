using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class SpeedtestHandler
    {
        private DownloadHandle downloadHandle2;
        private Config _config;
        private V2rayHandler _v2rayHandler;
        private List<int> _selecteds;
        Action<int, string> _updateFunc;

        private int testCounter = 0;
        private int ItemIndex
        {
            get
            {
                return _selecteds[testCounter - 1];
            }
        }

        public SpeedtestHandler(ref Config config, ref V2rayHandler v2rayHandler, List<int> selecteds, string actionType, Action<int, string> update)
        {
            _config = config;
            _v2rayHandler = v2rayHandler;
            _selecteds = selecteds;
            _updateFunc = update;

            if (actionType == "ping")
            {
                Task.Factory.StartNew(() => RunPing());
            }
            if (actionType == "tcping")
            {
                Task.Factory.StartNew(() => RunTcping());
            }
            else if (actionType == "realping")
            {
                Task.Factory.StartNew(() => RunRealPing());
            }
            else if (actionType == "speedtest")
            {
                Task.Factory.StartNew(() => RunSpeedTest());
            }
        }

        private void RunPingSub(Action<int> updateFun)
        {
            try
            {
                foreach (int index in _selecteds)
                {
                    if (_config.vmess[index].configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    try
                    {
                        updateFun(index);
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                    }
                }

                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }


        private void RunPing()
        {
            RunPingSub((int index) =>
            {
                long time = Utils.Ping(_config.vmess[index].address);
                _updateFunc(index, string.Format("{0}ms", time));
            });
        }

        private void RunTcping()
        {
            RunPingSub((int index) =>
            {
                int time = GetTcpingTime(_config.vmess[index].address, _config.vmess[index].port);
                _updateFunc(index, string.Format("{0}ms", time));
            });
        }

        private void RunRealPing()
        {
            try
            {
                string msg = string.Empty;

                Global.reloadV2ray = true;
                _v2rayHandler.LoadV2ray(_config, _selecteds);

                Thread.Sleep(5000);
                int httpPort = _config.GetLocalPort("speedtest");
                Task[] tasks = new Task[_selecteds.Count];
                int i = -1;
                foreach (int itemIndex in _selecteds)
                {
                    if (_config.vmess[itemIndex].configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }

                    i++;
                    tasks[i] = Task.Factory.StartNew(() => {
                        try
                        {
                            WebProxy webProxy = new WebProxy(Global.Loopback, httpPort + itemIndex);
                            int responseTime = -1;
                            string status = GetRealPingTime(Global.SpeedPingTestUrl, webProxy, out responseTime);
                            string output = Utils.IsNullOrEmpty(status) ? string.Format("{0}ms", responseTime) : string.Format("{0}", status);
                            _updateFunc(itemIndex, output);
                        }
                        catch (Exception ex)
                        {
                            Utils.SaveLog(ex.Message, ex);
                        }
                    });
                    //Thread.Sleep(100);
                }
                Task.WaitAll(tasks);

                Global.reloadV2ray = true;
                _v2rayHandler.LoadV2ray(_config);
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }


        private void RunSpeedTest()
        {
            if (_config.vmess.Count <= 0)
            {
                return;
            }

            Global.reloadV2ray = true;
            _v2rayHandler.LoadV2ray(_config, _selecteds);

            Thread.Sleep(5000);

            string url = Global.SpeedTestUrl;
            testCounter = 0;
            if (downloadHandle2 == null)
            {
                downloadHandle2 = new DownloadHandle();
                downloadHandle2.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        _updateFunc(ItemIndex, args.Msg);
                        if (ServerSpeedTestSub(testCounter, url) != 0)
                        {
                            return;
                        }
                    }
                    else
                    {
                        _updateFunc(ItemIndex, args.Msg);
                    }
                };
                downloadHandle2.Error += (sender2, args) =>
                {
                    _updateFunc(ItemIndex, args.GetException().Message);
                    if (ServerSpeedTestSub(testCounter, url) != 0)
                    {
                        return;
                    }
                };
            }

            if (ServerSpeedTestSub(testCounter, url) != 0)
            {
                return;
            }
        }

        private int ServerSpeedTestSub(int index, string url)
        {
            if (index >= _selecteds.Count)
            {
                Global.reloadV2ray = true;
                _v2rayHandler.LoadV2ray(_config);
                return -1;
            }

            int httpPort = _config.GetLocalPort("speedtest");
            index = _selecteds[index];

            testCounter++;
            WebProxy webProxy = new WebProxy(Global.Loopback, httpPort + index);
            downloadHandle2.DownloadFileAsync(_config, url, webProxy, 20);

            return 0;
        }

        private int GetTcpingTime(string url, int port)
        {
            int responseTime = -1;

            try
            {
                if (!IPAddress.TryParse(url, out IPAddress ipAddress))
                {
                    IPHostEntry ipHostInfo = System.Net.Dns.GetHostEntry(url);
                    ipAddress = ipHostInfo.AddressList[0];
                }

                Stopwatch timer = new Stopwatch();
                timer.Start();

                IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
                Socket clientSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                IAsyncResult result = clientSocket.BeginConnect(endPoint, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("connect timeout (5s): " + url);
                clientSocket.EndConnect(result);

                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;
                clientSocket.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return responseTime;
        }

        private string GetRealPingTime(string url, WebProxy webProxy, out int responseTime)
        {
            string msg = string.Empty;
            responseTime = -1;
            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                myHttpWebRequest.Timeout = 5000;
                myHttpWebRequest.Proxy = webProxy;//new WebProxy(Global.Loopback, Global.httpPort);

                Stopwatch timer = new Stopwatch();
                timer.Start();

                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                if (myHttpWebResponse.StatusCode != HttpStatusCode.OK
                    && myHttpWebResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    msg = myHttpWebResponse.StatusDescription;
                }
                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;

                myHttpWebResponse.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ex.Message;
            }
            return msg;
        }
    }
}
