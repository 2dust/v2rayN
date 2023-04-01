using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    class SpeedtestHandler
    {
        private Config _config;
        private CoreHandler _coreHandler;
        private List<ServerTestItem> _selecteds;
        private ESpeedActionType _actionType;
        Action<string, string, string> _updateFunc;

        public SpeedtestHandler(Config config)
        {
            _config = config;
        }

        public SpeedtestHandler(Config config, CoreHandler coreHandler, List<ProfileItem> selecteds, ESpeedActionType actionType, Action<string, string, string> update)
        {
            _config = config;
            _coreHandler = coreHandler;
            _actionType = actionType;
            _updateFunc = update;

            _selecteds = new List<ServerTestItem>();
            foreach (var it in selecteds)
            {
                if (it.configType == EConfigType.Custom)
                {
                    continue;
                }
                if (it.port <= 0)
                {
                    continue;
                }
                _selecteds.Add(new ServerTestItem()
                {
                    indexId = it.indexId,
                    address = it.address,
                    port = it.port,
                    configType = it.configType
                });
            }
            //clear test result
            foreach (var it in _selecteds)
            {
                switch (actionType)
                {
                    case ESpeedActionType.Ping:
                    case ESpeedActionType.Tcping:
                    case ESpeedActionType.Realping:
                        UpdateFunc(it.indexId, ResUI.Speedtesting, "");
                        ProfileExHandler.Instance.SetTestDelay(it.indexId, "0");
                        break;
                    case ESpeedActionType.Speedtest:
                        UpdateFunc(it.indexId, "", ResUI.SpeedtestingWait);
                        ProfileExHandler.Instance.SetTestSpeed(it.indexId, "0");
                        break;
                    case ESpeedActionType.Mixedtest:
                        UpdateFunc(it.indexId, ResUI.Speedtesting, ResUI.SpeedtestingWait);
                        ProfileExHandler.Instance.SetTestDelay(it.indexId, "0");
                        ProfileExHandler.Instance.SetTestSpeed(it.indexId, "0");
                        break;
                }
            }

            switch (actionType)
            {
                case ESpeedActionType.Ping:
                    Task.Run(RunPing);
                    break;
                case ESpeedActionType.Tcping:
                    Task.Run(RunTcping);
                    break;
                case ESpeedActionType.Realping:
                    Task.Run(RunRealPing);
                    break;
                case ESpeedActionType.Speedtest:
                    Task.Run(RunSpeedTestAsync);
                    break;
                case ESpeedActionType.Mixedtest:
                    Task.Run(RunMixedtestAsync);
                    break;
            }
        }

        private void RunPingSub(Action<ServerTestItem> updateFun)
        {
            try
            {
                foreach (var it in _selecteds.Where(it => it.configType != EConfigType.Custom))
                {
                    try
                    {
                        Task.Run(() => updateFun(it));
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
                long time = Ping(it.address);
                var output = FormatOut(time, Global.DelayUnit);

                ProfileExHandler.Instance.SetTestDelay(it.indexId, output);
                UpdateFunc(it.indexId, output);
            });
        }

        private void RunTcping()
        {
            RunPingSub((ServerTestItem it) =>
            {
                int time = GetTcpingTime(it.address, it.port);
                var output = FormatOut(time, Global.DelayUnit);

                ProfileExHandler.Instance.SetTestDelay(it.indexId, output);
                UpdateFunc(it.indexId, output);
            });
        }
        
        private readonly object _lock = new object();
        private Task RunRealPing()
        {
            int pid = -1;
            try
            {
                string msg = string.Empty;

                pid = _coreHandler.LoadCoreConfigString(_config, _selecteds);
                if (pid < 0)
                {
                    UpdateFunc("", ResUI.FailedToRunCore);
                    return Task.CompletedTask;
                }

                DownloadHandle downloadHandle = new DownloadHandle();
                //Thread.Sleep(5000);
                List<Task> tasks = new();
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

                            WebProxy webProxy = new(Global.Loopback, it.port);
                            string output = GetRealPingTime(downloadHandle, webProxy);

                            ProfileExHandler.Instance.SetTestDelay(it.indexId, output);
                            // add a lock to synchronize access to _selecteds list
                            lock (_lock){ 
                              UpdateFunc(it.indexId, output);
                              int.TryParse(output, out int delay);
                              it.delay = delay;
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.SaveLog(ex.Message, ex);
                            // remove the failed item from the list
                            lock (_lock){
                              _selecteds.Remove(it);
                            }
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
                if (pid > 0) _coreHandler.CoreStopPid(pid);
                ProfileExHandler.Instance.SaveTo();
            }

            return Task.CompletedTask;
        }

        private async Task RunSpeedTestAsync()
        {
            int pid = -1;
            //if (_actionType == ESpeedActionType.Mixedtest)
            //{
            //    _selecteds = _selecteds.OrderBy(t => t.delay).ToList();
            //}

            pid = _coreHandler.LoadCoreConfigString(_config, _selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            string url = _config.speedTestItem.speedTestUrl;
            var timeout = _config.speedTestItem.speedTestTimeout;

            DownloadHandle downloadHandle = new();
            
            // add a lock to synchronize access to _selecteds list
            lock (_lock){
               _selecteds = _selecteds.Where(it => it.allowTest && it.configType != EConfigType.Custom).ToList();
            }

            foreach (var it in _selecteds)
            {
                // if (!it.allowTest)
                // {
                //     continue;
                // }
                // if (it.configType == EConfigType.Custom)
                // {
                //     continue;
                // }
                //if (it.delay < 0)
                //{
                //    UpdateFunc(it.indexId, "", ResUI.SpeedtestingSkip);
                //    continue;
                //}
                ProfileExHandler.Instance.SetTestSpeed(it.indexId, "-1");
                UpdateFunc(it.indexId, "", ResUI.Speedtesting);

                var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                if (item is null) continue;

                WebProxy webProxy = new(Global.Loopback, it.port);

                try{
                    var msg = await downloadHandle.DownloadDataAsync(url, webProxy, timeout);
                    decimal.TryParse(msg, out decimal dec);
                    if (dec > 0)
                    {
                        ProfileExHandler.Instance.SetTestSpeed(it.indexId, msg);
                    }
                    UpdateFunc(it.indexId, "", msg);
                }
                catch (Exception ex){
                   Utils.SaveLog(ex.Message, ex);
                   lock (_lock){
                     _selecteds.RemoveAll(s => s.indexId == it.indexId);
                   }
                }
            }

            if (pid > 0)
            {
                _coreHandler.CoreStopPid(pid);
            }
            UpdateFunc("", ResUI.SpeedtestingCompleted);
            ProfileExHandler.Instance.SaveTo();
        }

        private async Task RunSpeedTestMulti()
        {
            int pid = -1;
            pid = _coreHandler.LoadCoreConfigString(_config, _selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            string url = _config.speedTestItem.speedTestUrl;
            var timeout = _config.speedTestItem.speedTestTimeout;

            DownloadHandle downloadHandle = new();
            
            // add a lock to synchronize access to _selecteds list
            var _lock = new object();
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
                ProfileExHandler.Instance.SetTestSpeed(it.indexId, "-1");
                UpdateFunc(it.indexId, "", ResUI.Speedtesting);

                var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                if (item is null) continue;

                WebProxy webProxy = new(Global.Loopback, it.port);
                try{
                  await downloadHandle.DownloadDataAsync(url, webProxy, timeout, async (bool success, string msg) =>{
                   decimal.TryParse(msg, out decimal dec);
                   if (dec > 0)
                   {
                      ProfileExHandler.Instance.SetTestSpeed(it.indexId, msg);
                   }
                   UpdateFunc(it.indexId, "", msg);
                  });
                }
                catch (Exception ex){
                  // remove the failed item from the list
                  lock (_lock){
                    _selecteds.Remove(it);
                  }
                  UpdateFunc(it.indexId, "", ResUI.SpeedtestingFailed + ": " + ex.Message);
                }
            }

            Thread.Sleep((timeout + 2) * 1000);

            if (pid > 0)
            {
                _coreHandler.CoreStopPid(pid);
            }
            UpdateFunc("", ResUI.SpeedtestingCompleted);
            ProfileExHandler.Instance.SaveTo();
        }

        private async Task RunMixedtestAsync()
        {
            await RunRealPing();

            Thread.Sleep(1000);

            await RunSpeedTestMulti();
        }

        public string GetRealPingTime(DownloadHandle downloadHandle, IWebProxy webProxy)
        {
            string status = downloadHandle.GetRealPingTime(_config.speedTestItem.speedPingTestUrl, webProxy, 10, out int responseTime);
            //string output = Utils.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : status;
            return FormatOut(Utils.IsNullOrEmpty(status) ? responseTime : -1, Global.DelayUnit);
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

                Stopwatch timer = new();
                timer.Start();

                IPEndPoint endPoint = new(ipAddress, port);
                using Socket clientSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                IAsyncResult result = clientSocket.BeginConnect(endPoint, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("connect timeout (5s): " + url);
                clientSocket.EndConnect(result);

                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return responseTime;
        }


        /// <summary>
        /// Ping
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        public long Ping(string host)
        {
            long roundtripTime = -1;
            try
            {
                int timeout = 30;
                int echoNum = 2;
                using Ping pingSender = new();
                for (int i = 0; i < echoNum; i++)
                {
                    PingReply reply = pingSender.Send(host, timeout);
                    if (reply.Status == IPStatus.Success)
                    {
                        if (reply.RoundtripTime < 0)
                        {
                            continue;
                        }
                        if (roundtripTime < 0 || reply.RoundtripTime < roundtripTime)
                        {
                            roundtripTime = reply.RoundtripTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return -1;
            }
            return roundtripTime;
        }

        private string FormatOut(object time, string unit)
        {
            //if (time.ToString().Equals("-1"))
            //{
            //    return "Timeout";
            //}
            return $"{time}";
        }

        private void UpdateFunc(string indexId, string delay, string speed = "")
        {
            _updateFunc(indexId, delay, speed);
        }
    }
}
