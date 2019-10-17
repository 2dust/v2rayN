using Grpc.Core;
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
        private V2rayUpdateHandle v2rayUpdateHandle2;
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
                _workThread.Start();
            }
            if (actionType == "tcping")
            {
                _workThread = new Thread(new ThreadStart(RunTcping));
                _workThread.Start();
            }
            else if (actionType == "realping")
            {
                _workThread = new Thread(new ThreadStart(RunRealPing));
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

        public void RunPing()
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

                Thread.Sleep(1);

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void RunTcping()
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

                Thread.Sleep(1);

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void RunRealPing()
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
                        if (ConfigHandler.SetDefaultServer(ref _config, index) == 0)
                        {
                            _v2rayHandler.LoadV2ray(_config);
                        }
                        else
                        {
                            return;
                        }
                        Thread.Sleep(1000 * 5);

                        int responseTime = -1;
                        var status = GetRealPingTime(Global.SpeedPingTestUrl, out responseTime);
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
                }

                Thread.Sleep(1);

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

            string url = Global.SpeedTestUrl;
            testCounter = 0;
            if (v2rayUpdateHandle2 == null)
            {
                v2rayUpdateHandle2 = new V2rayUpdateHandle();
                v2rayUpdateHandle2.UpdateCompleted += (sender2, args) =>
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
                v2rayUpdateHandle2.Error += (sender2, args) =>
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
                return -1;
            }

            if (ConfigHandler.SetDefaultServer(ref _config, _selecteds[index]) == 0)
            {
                _v2rayHandler.LoadV2ray(_config);

                testCounter++;

                v2rayUpdateHandle2.DownloadFileAsync(_config, url);

                return 0;
            }
            else
            {
                return -1;
            }
        }

        private int GetTcpingTime(string url, int port)
        {
            var responseTime = -1;

            try
            {
                IPHostEntry ipHostInfo = System.Net.Dns.Resolve(url);
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

        private string GetRealPingTime(string url, out int responseTime)
        {
            string msg = string.Empty;
            responseTime = -1;

            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                myHttpWebRequest.Timeout = 5000;
                myHttpWebRequest.Proxy = new WebProxy(Global.Loopback, Global.sysAgentPort);

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
