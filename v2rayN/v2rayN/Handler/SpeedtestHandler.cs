﻿using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using v2rayN.Model;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    internal class SpeedtestHandler
    {
        private Config? _config;
        private CoreHandler _coreHandler;
        private List<ServerTestItem> _selecteds;
        private ESpeedActionType _actionType;
        private Action<string, string, string> _updateFunc;

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

        private Task RunTcping()
        {
            try
            {
                List<Task> tasks = [];
                foreach (var it in _selecteds)
                {
                    if (it.configType == EConfigType.Custom)
                    {
                        continue;
                    }
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            int time = GetTcpingTime(it.address, it.port);
                            var output = FormatOut(time, Global.DelayUnit);

                            ProfileExHandler.Instance.SetTestDelay(it.indexId, output);
                            UpdateFunc(it.indexId, output);
                        }
                        catch (Exception ex)
                        {
                            Logging.SaveLog(ex.Message, ex);
                        }
                    }));
                }
                Task.WaitAll([.. tasks]);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                ProfileExHandler.Instance.SaveTo();
            }

            return Task.CompletedTask;
        }

        private Task RunRealPing()
        {
            int pid = -1;
            try
            {
                string msg = string.Empty;

                pid = _coreHandler.LoadCoreConfigSpeedtest(_selecteds);
                if (pid < 0)
                {
                    UpdateFunc("", ResUI.FailedToRunCore);
                    return Task.CompletedTask;
                }

                DownloadHandle downloadHandle = new DownloadHandle();

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
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            WebProxy webProxy = new(Global.Loopback, it.port);
                            string output = await GetRealPingTime(downloadHandle, webProxy);

                            ProfileExHandler.Instance.SetTestDelay(it.indexId, output);
                            UpdateFunc(it.indexId, output);
                            int.TryParse(output, out int delay);
                            it.delay = delay;
                        }
                        catch (Exception ex)
                        {
                            Logging.SaveLog(ex.Message, ex);
                        }
                    }));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                if (pid > 0)
                {
                    _coreHandler.CoreStopPid(pid);
                }
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

            pid = _coreHandler.LoadCoreConfigSpeedtest(_selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            string url = _config.speedTestItem.speedTestUrl;
            var timeout = _config.speedTestItem.speedTestTimeout;

            DownloadHandle downloadHandle = new();

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

                await downloadHandle.DownloadDataAsync(url, webProxy, timeout, (bool success, string msg) =>
                {
                    decimal.TryParse(msg, out decimal dec);
                    if (dec > 0)
                    {
                        ProfileExHandler.Instance.SetTestSpeed(it.indexId, msg);
                    }
                    UpdateFunc(it.indexId, "", msg);
                });
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
            pid = _coreHandler.LoadCoreConfigSpeedtest(_selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            string url = _config.speedTestItem.speedTestUrl;
            var timeout = _config.speedTestItem.speedTestTimeout;

            DownloadHandle downloadHandle = new();

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
                if (it.delay < 0)
                {
                    UpdateFunc(it.indexId, "", ResUI.SpeedtestingSkip);
                    continue;
                }
                ProfileExHandler.Instance.SetTestSpeed(it.indexId, "-1");
                UpdateFunc(it.indexId, "", ResUI.Speedtesting);

                var item = LazyConfig.Instance.GetProfileItem(it.indexId);
                if (item is null) continue;

                WebProxy webProxy = new(Global.Loopback, it.port);
                _ = downloadHandle.DownloadDataAsync(url, webProxy, timeout, (bool success, string msg) =>
                {
                    decimal.TryParse(msg, out decimal dec);
                    if (dec > 0)
                    {
                        ProfileExHandler.Instance.SetTestSpeed(it.indexId, msg);
                    }
                    UpdateFunc(it.indexId, "", msg);
                });
                await Task.Delay(2000);
            }

            await Task.Delay((timeout + 2) * 1000);

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

            await Task.Delay(1000);

            await RunSpeedTestMulti();
        }

        private async Task<string> GetRealPingTime(DownloadHandle downloadHandle, IWebProxy webProxy)
        {
            int responseTime = await downloadHandle.GetRealPingTime(_config.speedTestItem.speedPingTestUrl, webProxy, 10);
            //string output = Utile.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : status;
            return FormatOut(responseTime, Global.DelayUnit);
        }

        private int GetTcpingTime(string url, int port)
        {
            int responseTime = -1;

            try
            {
                if (!IPAddress.TryParse(url, out IPAddress? ipAddress))
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
                Logging.SaveLog(ex.Message, ex);
            }
            return responseTime;
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