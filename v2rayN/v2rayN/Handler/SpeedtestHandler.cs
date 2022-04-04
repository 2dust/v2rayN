using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    class SpeedtestHandler
    {
        private Config _config;
        private V2rayHandler _v2rayHandler;
        private List<ServerTestItem> _selecteds;
        Action<string, string> _updateFunc;

        public SpeedtestHandler(ref Config config)
        {
            _config = config;
        }

        public SpeedtestHandler(ref Config config, V2rayHandler v2rayHandler, List<VmessItem> selecteds, ESpeedActionType actionType, Action<string, string> update)
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
                    indexId = it.indexId,
                    address = it.address,
                    port = it.port,
                    configType = it.configType
                });
            }

            if (actionType == ESpeedActionType.Ping)
            {
                Task.Run(() => RunPing());
            }
            else if (actionType == ESpeedActionType.Tcping)
            {
                Task.Run(() => RunTcping());
            }
            else if (actionType == ESpeedActionType.Realping)
            {
                Task.Run(() => RunRealPing());
            }
            else if (actionType == ESpeedActionType.Speedtest)
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
                    if (it.configType == EConfigType.Custom)
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

                _updateFunc(it.indexId, FormatOut(time, "ms"));
            });
        }

        private void RunTcping()
        {
            RunPingSub((ServerTestItem it) =>
            {
                int time = GetTcpingTime(it.address, it.port);

                _updateFunc(it.indexId, FormatOut(time, "ms"));
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
                    _updateFunc(_selecteds[0].indexId, ResUI.OperationFailed);
                    return;
                }

                //Thread.Sleep(5000);
                List<Task> tasks = new List<Task>();
                foreach (var it in _selecteds)
                {
                    if (!it.allowTest)
                    {
                        continue;
                    }
                    if (it.configType == EConfigType.Custom)
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

                            _config.GetVmessItem(it.indexId)?.SetTestResult(output);
                            _updateFunc(it.indexId, output);
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
                int httpPort = _config.GetLocalPort(Global.InboundHttp2);

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

            pid = _v2rayHandler.LoadV2rayConfigString(_config, _selecteds);
            if (pid < 0)
            {
                _updateFunc(_selecteds[0].indexId, ResUI.OperationFailed);
                return;
            }

            string url = _config.constItem.speedTestUrl;
            DownloadHandle downloadHandle2 = new DownloadHandle();
            downloadHandle2.UpdateCompleted += (sender2, args) =>
            {
                _config.GetVmessItem(testIndexId)?.SetTestResult(args.Msg);
                _updateFunc(testIndexId, args.Msg);
            };
            downloadHandle2.Error += (sender2, args) =>
            {
                _updateFunc(testIndexId, args.GetException().Message);
            };

            var timeout = 10;
            foreach (var it in _selecteds)
            {
                if (!it.allowTest)
                {
                    continue;
                }
                if (it.configType == EConfigType.Custom)
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
