using ReactiveUI;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ServiceLib.Services
{
    public class SpeedtestService
    {
        private Config? _config;
        private List<ServerTestItem> _selecteds;
        private ESpeedActionType _actionType;
        private Action<SpeedTestResult>? _updateFunc;
        private bool _exitLoop = false;

        public SpeedtestService(Config config, List<ProfileItem> selecteds, ESpeedActionType actionType, Action<SpeedTestResult> updateFunc)
        {
            _config = config;

            _actionType = actionType;
            _updateFunc = updateFunc;

            _selecteds = new List<ServerTestItem>();
            foreach (var it in selecteds)
            {
                if (it.ConfigType == EConfigType.Custom)
                {
                    continue;
                }
                if (it.Port <= 0)
                {
                    continue;
                }
                _selecteds.Add(new ServerTestItem()
                {
                    IndexId = it.IndexId,
                    Address = it.Address,
                    Port = it.Port,
                    ConfigType = it.ConfigType
                });
            }
            //clear test result
            foreach (var it in _selecteds)
            {
                switch (actionType)
                {
                    case ESpeedActionType.Tcping:
                    case ESpeedActionType.Realping:
                        UpdateFunc(it.IndexId, ResUI.Speedtesting, "");
                        ProfileExHandler.Instance.SetTestDelay(it.IndexId, "0");
                        break;

                    case ESpeedActionType.Speedtest:
                        UpdateFunc(it.IndexId, "", ResUI.SpeedtestingWait);
                        ProfileExHandler.Instance.SetTestSpeed(it.IndexId, "0");
                        break;

                    case ESpeedActionType.Mixedtest:
                        UpdateFunc(it.IndexId, ResUI.Speedtesting, ResUI.SpeedtestingWait);
                        ProfileExHandler.Instance.SetTestDelay(it.IndexId, "0");
                        ProfileExHandler.Instance.SetTestSpeed(it.IndexId, "0");
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
            MessageBus.Current.Listen<string>(EMsgCommand.StopSpeedtest.ToString()).Subscribe(ExitLoop);
        }

        private void ExitLoop(string x)
        {
            if (_exitLoop) return;
            _exitLoop = true;
            UpdateFunc("", ResUI.SpeedtestingStop);
        }

        private async Task RunTcping()
        {
            try
            {
                List<Task> tasks = [];
                foreach (var it in _selecteds)
                {
                    if (it.ConfigType == EConfigType.Custom)
                    {
                        continue;
                    }
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            int time = await GetTcpingTime(it.Address, it.Port);
                            var output = FormatOut(time, Global.DelayUnit);

                            ProfileExHandler.Instance.SetTestDelay(it.IndexId, output);
                            UpdateFunc(it.IndexId, output);
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
                await ProfileExHandler.Instance.SaveTo();
            }
        }

        private async Task RunRealPing()
        {
            int pid = -1;
            try
            {
                string msg = string.Empty;

                pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(_selecteds);
                if (pid < 0)
                {
                    UpdateFunc("", ResUI.FailedToRunCore);
                    return;
                }

                DownloadService downloadHandle = new DownloadService();

                List<Task> tasks = new();
                foreach (var it in _selecteds)
                {
                    if (!it.AllowTest)
                    {
                        continue;
                    }
                    if (it.ConfigType == EConfigType.Custom)
                    {
                        continue;
                    }
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            WebProxy webProxy = new(Global.Loopback, it.Port);
                            string output = await GetRealPingTime(downloadHandle, webProxy);

                            ProfileExHandler.Instance.SetTestDelay(it.IndexId, output);
                            UpdateFunc(it.IndexId, output);
                            int.TryParse(output, out int delay);
                            it.Delay = delay;
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
                    CoreHandler.Instance.CoreStopPid(pid);
                }
                await ProfileExHandler.Instance.SaveTo();
            }
        }

        private async Task RunSpeedTestAsync()
        {
            int pid = -1;
            //if (_actionType == ESpeedActionType.Mixedtest)
            //{
            //    _selecteds = _selecteds.OrderBy(t => t.delay).ToList();
            //}

            pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(_selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            string url = _config.SpeedTestItem.SpeedTestUrl;
            var timeout = _config.SpeedTestItem.SpeedTestTimeout;

            DownloadService downloadHandle = new();

            foreach (var it in _selecteds)
            {
                if (_exitLoop)
                {
                    UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                    continue;
                }
                if (!it.AllowTest)
                {
                    continue;
                }
                if (it.ConfigType == EConfigType.Custom)
                {
                    continue;
                }
                //if (it.delay < 0)
                //{
                //    UpdateFunc(it.indexId, "", ResUI.SpeedtestingSkip);
                //    continue;
                //}
                ProfileExHandler.Instance.SetTestSpeed(it.IndexId, "-1");
                UpdateFunc(it.IndexId, "", ResUI.Speedtesting);

                var item = await AppHandler.Instance.GetProfileItem(it.IndexId);
                if (item is null) continue;

                WebProxy webProxy = new(Global.Loopback, it.Port);

                await downloadHandle.DownloadDataAsync(url, webProxy, timeout, (success, msg) =>
                {
                    decimal.TryParse(msg, out decimal dec);
                    if (dec > 0)
                    {
                        ProfileExHandler.Instance.SetTestSpeed(it.IndexId, msg);
                    }
                    UpdateFunc(it.IndexId, "", msg);
                });
            }

            if (pid > 0)
            {
                CoreHandler.Instance.CoreStopPid(pid);
            }
            UpdateFunc("", ResUI.SpeedtestingCompleted);
            await ProfileExHandler.Instance.SaveTo();
        }

        private async Task RunSpeedTestMulti()
        {
            int pid = -1;
            pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(_selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            string url = _config.SpeedTestItem.SpeedTestUrl;
            var timeout = _config.SpeedTestItem.SpeedTestTimeout;

            DownloadService downloadHandle = new();

            foreach (var it in _selecteds)
            {
                if (_exitLoop)
                {
                    UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                    continue;
                }

                if (!it.AllowTest)
                {
                    continue;
                }
                if (it.ConfigType == EConfigType.Custom)
                {
                    continue;
                }
                if (it.Delay < 0)
                {
                    UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                    continue;
                }
                ProfileExHandler.Instance.SetTestSpeed(it.IndexId, "-1");
                UpdateFunc(it.IndexId, "", ResUI.Speedtesting);

                var item = await AppHandler.Instance.GetProfileItem(it.IndexId);
                if (item is null) continue;

                WebProxy webProxy = new(Global.Loopback, it.Port);
                _ = downloadHandle.DownloadDataAsync(url, webProxy, timeout, (success, msg) =>
                {
                    decimal.TryParse(msg, out decimal dec);
                    if (dec > 0)
                    {
                        ProfileExHandler.Instance.SetTestSpeed(it.IndexId, msg);
                    }
                    UpdateFunc(it.IndexId, "", msg);
                });
                await Task.Delay(2000);
            }

            await Task.Delay((timeout + 2) * 1000);

            if (pid > 0)
            {
                CoreHandler.Instance.CoreStopPid(pid);
            }
            UpdateFunc("", ResUI.SpeedtestingCompleted);
            await ProfileExHandler.Instance.SaveTo();
        }

        private async Task RunMixedtestAsync()
        {
            await RunRealPing();

            await Task.Delay(1000);

            await RunSpeedTestMulti();
        }

        private async Task<string> GetRealPingTime(DownloadService downloadHandle, IWebProxy webProxy)
        {
            int responseTime = await downloadHandle.GetRealPingTime(_config.SpeedTestItem.SpeedPingTestUrl, webProxy, 10);
            //string output = Utile.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : status;
            return FormatOut(responseTime, Global.DelayUnit);
        }

        private async Task<int> GetTcpingTime(string url, int port)
        {
            int responseTime = -1;

            try
            {
                if (!IPAddress.TryParse(url, out IPAddress? ipAddress))
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(url);
                    ipAddress = ipHostInfo.AddressList[0];
                }

                var timer = Stopwatch.StartNew();

                IPEndPoint endPoint = new(ipAddress, port);
                using Socket clientSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                var result = clientSocket.BeginConnect(endPoint, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("connect timeout (5s): " + url);
                clientSocket.EndConnect(result);

                timer.Stop();
                responseTime = (int)timer.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return responseTime;
        }

        private string FormatOut(object time, string unit)
        {
            return $"{time}";
        }

        private void UpdateFunc(string indexId, string delay, string speed = "")
        {
            _updateFunc?.Invoke(new() { IndexId = indexId, Delay = delay, Speed = speed });
        }
    }
}