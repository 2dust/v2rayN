﻿using System;
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
        private Config _config;
        private V2rayHandler _v2rayHandler;
        private List<ServerTestItem> _selecteds;
        Action<int, string> _updateFunc;

        public SpeedtestHandler(ref Config config)
        {
            _config = config;
        }

        public SpeedtestHandler(ref Config config, ref V2rayHandler v2rayHandler, List<int> selecteds, string actionType, Action<int, string> update)
        {
            _config = config;
            _v2rayHandler = v2rayHandler;
            //_selecteds = Utils.DeepCopy(selecteds);
            _updateFunc = update;

            _selecteds = new List<ServerTestItem>();
            foreach (var it in selecteds)
            {
                _selecteds.Add(new ServerTestItem()
                {
                    selected = it,
                    indexId = config.vmess[it].indexId,
                    address = config.vmess[it].address,
                    port = config.vmess[it].port,
                    configType = config.vmess[it].configType
                });
            }

            if (actionType == "ping")
            {
                Task.Run(() => RunPing());
            }
            if (actionType == "tcping")
            {
                Task.Run(() => RunTcping());
            }
            else if (actionType == "realping")
            {
                Task.Run(() => RunRealPing());
            }
            else if (actionType == "speedtest")
            {
                Task.Run(() => RunSpeedTest());
            }
        }

        private void RunPingSub(Action<ServerTestItem> updateFun)
        {
            try
            {
                foreach (var it in _selecteds)
                {
                    if (it.configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    try
                    {
                        updateFun(it);
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
            RunPingSub((ServerTestItem it) =>
            {
                long time = Utils.Ping(it.address);
                var index = _config.FindIndexId(it.indexId);
                if (index < 0) return;
                _updateFunc(index, FormatOut(time, "ms"));
            });
        }

        private void RunTcping()
        {
            RunPingSub((ServerTestItem it) =>
            {
                int time = GetTcpingTime(it.address, it.port);
                var index = _config.FindIndexId(it.indexId);
                if (index < 0) return;
                _updateFunc(index, FormatOut(time, "ms"));
            });
        }

        private void RunRealPing()
        {
            int pid = -1;
            try
            {
                string msg = string.Empty;

                pid = _v2rayHandler.LoadV2rayConfigString(_config, _selecteds);
                if (pid < 0)
                {
                    _updateFunc(_selecteds[0].selected, UIRes.I18N("OperationFailed"));
                    return;
                }

                //Thread.Sleep(5000);
                List<Task> tasks = new List<Task>();
                foreach (var it in _selecteds)
                {
                    if (it.configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    if (it.port <= 0)
                    {
                        continue;
                    }
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            WebProxy webProxy = new WebProxy(Global.Loopback, it.port);
                            int responseTime = -1;
                            string status = GetRealPingTime(_config.constItem.speedPingTestUrl, webProxy, out responseTime);
                            string output = Utils.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : status;
                            var index = _config.FindIndexId(it.indexId);
                            if (index < 0) return;
                            _updateFunc(index, output);
                        }
                        catch (Exception ex)
                        {
                            Utils.SaveLog(ex.Message, ex);
                        }
                    }));
                    //Thread.Sleep(100);
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            finally
            {
                if (pid > 0) _v2rayHandler.V2rayStopPid(pid);
            }
        }

        public int RunAvailabilityCheck() // alias: isLive
        {
            try
            {
                int httpPort = _config.GetLocalPort(Global.InboundHttp);

                Task<int> t = Task.Run(() =>
                {
                    try
                    {
                        WebProxy webProxy = new WebProxy(Global.Loopback, httpPort);
                        int responseTime = -1;
                        string status = GetRealPingTime(Global.SpeedPingTestUrl, webProxy, out responseTime);
                        bool noError = Utils.IsNullOrEmpty(status);
                        return noError ? responseTime : -1;
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                        return -1;
                    }
                });
                return t.Result;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return -1;
            }
        }

        private void RunSpeedTest()
        {
            string testIndexId = string.Empty;
            int pid = -1;

            if (_config.vmess.Count <= 0)
            {
                return;
            }

            pid = _v2rayHandler.LoadV2rayConfigString(_config, _selecteds);
            if (pid < 0)
            {
                _updateFunc(_selecteds[0].selected, UIRes.I18N("OperationFailed"));
                return;
            }

            string url = _config.constItem.speedTestUrl;
            DownloadHandle downloadHandle2 = new DownloadHandle();
            downloadHandle2.UpdateCompleted += (sender2, args) =>
            {
                var index = _config.FindIndexId(testIndexId);
                if (index < 0) return;
                _updateFunc(index, args.Msg);
            };
            downloadHandle2.Error += (sender2, args) =>
            {
                var index = _config.FindIndexId(testIndexId);
                if (index < 0) return;
                _updateFunc(index, args.GetException().Message);
            };

            var timeout = 10;
            foreach (var it in _selecteds)
            {
                if (it.configType == (int)EConfigType.Custom)
                {
                    continue;
                }
                if (it.port <= 0)
                {
                    continue;
                }
                testIndexId = it.indexId;
                if (_config.FindIndexId(it.indexId) < 0) continue;
              
                WebProxy webProxy = new WebProxy(Global.Loopback, it.port);
                var ws = downloadHandle2.DownloadDataAsync(url, webProxy, timeout - 2);

                Thread.Sleep(1000 * timeout);

                ws.CancelAsync();
                ws.Dispose();

                Thread.Sleep(1000 * 2);
            }
            if (pid > 0) _v2rayHandler.V2rayStopPid(pid);
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
        private string FormatOut(object time, string unit)
        {
            if (time.ToString().Equals("-1"))
            {
                return "Timeout";
            }
            return string.Format("{0}{1}", time, unit).PadLeft(8, ' ');
        }
    }
}
