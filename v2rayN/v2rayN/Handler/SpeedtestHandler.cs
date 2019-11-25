using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class SpeedtestHandler
    {
        private DownloadHandle downloadHandle2;
        private Config _config;
        private V2rayHandler _v2rayHandler;
        private List<int> _selecteds;
        private Thread _workThread;
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
                _workThread = new Thread(new ThreadStart(RunPing));
                _workThread.IsBackground = true;
                _workThread.Start();
            }
            if (actionType == "tcping")
            {
                _workThread = new Thread(new ThreadStart(RunTcping));
                _workThread.IsBackground = true;
                _workThread.Start();
            }
            else if (actionType == "realping")
            {
                _workThread = new Thread(new ThreadStart(RunRealPing));
                _workThread.IsBackground = true;
                _workThread.Start();
            }
            else if (actionType == "speedtest")
            {
                RunSpeedTest();
            }
        }

        public void Close()
        {
            try
            {
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private void RunPing()
        {
            try
            {
                for (int k = 0; k < _selecteds.Count; k++)
                {
                    int index = _selecteds[k];
                    if (_config.vmess[index].configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    try
                    {
                        long time = Utils.Ping(_config.vmess[index].address);
                        _updateFunc(index, string.Format("{0}ms", time));
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                    }
                }

                Thread.Sleep(100);

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private void RunTcping()
        {
            try
            {
                for (int k = 0; k < _selecteds.Count; k++)
                {
                    int index = _selecteds[k];
                    if (_config.vmess[index].configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    try
                    {
                        var time = GetTcpingTime(_config.vmess[index].address, _config.vmess[index].port);
                        _updateFunc(index, string.Format("{0}ms", time));
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                    }
                }

                Thread.Sleep(100);

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private void RunRealPing()
        {
            try
            {
                string msg = string.Empty;

                Global.reloadV2ray = true;
                _v2rayHandler.LoadV2ray(_config, _selecteds);

                Thread.Sleep(5000);

                var httpPort = _config.GetLocalPort("speedtest");
                for (int k = 0; k < _selecteds.Count; k++)
                {
                    int index = _selecteds[k];
                    if (_config.vmess[index].configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }

                    try
                    {
                        var webProxy = new WebProxy(Global.Loopback, httpPort + index);
                        int responseTime = -1;
                        var status = GetRealPingTime(Global.SpeedPingTestUrl, webProxy, out responseTime);
                        if (!Utils.IsNullOrEmpty(status))
                        {
                            _updateFunc(index, string.Format("{0}", status));
                        }
                        else
                        {
                            _updateFunc(index, string.Format("{0}ms", responseTime));
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                    }
                    Thread.Sleep(100);
                }

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

            var httpPort = _config.GetLocalPort("speedtest");
            index = _selecteds[index];

            testCounter++;
            var webProxy = new WebProxy(Global.Loopback, httpPort + index);
            downloadHandle2.DownloadFileAsync(_config, url, webProxy, 30);

            return 0;
        }

        private int GetTcpingTime(string url, int port)
        {
            var responseTime = -1;

            try
            {
                IPHostEntry ipHostInfo = System.Net.Dns.GetHostEntry(url);
                IPAddress ipAddress = ipHostInfo.AddressList[0];

                var timer = new Stopwatch();
                timer.Start();

                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(new IPEndPoint(ipAddress, port));
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

                var timer = new Stopwatch();
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
