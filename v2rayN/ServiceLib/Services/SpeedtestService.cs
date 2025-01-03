using ReactiveUI;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ServiceLib.Services
{
    public class SpeedtestService
    {
        private Config? _config;
        private Action<SpeedTestResult>? _updateFunc;

        private bool _exitLoop = false;
        private static readonly string _tag = "SpeedtestService";

        public SpeedtestService(Config config, List<ProfileItem> selecteds, ESpeedActionType actionType, Action<SpeedTestResult> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;

            var lstSelected = new List<ServerTestItem>();
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
                lstSelected.Add(new ServerTestItem()
                {
                    IndexId = it.IndexId,
                    Address = it.Address,
                    Port = it.Port,
                    ConfigType = it.ConfigType
                });
            }

            //clear test result
            foreach (var it in lstSelected)
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

            MessageBus.Current.Listen<string>(EMsgCommand.StopSpeedtest.ToString()).Subscribe(ExitLoop);

            Task.Run(async () => { await RunAsync(actionType, lstSelected); });
        }

        private async Task RunAsync(ESpeedActionType actionType, List<ServerTestItem> lstSelected)
        {
            if (actionType == ESpeedActionType.Tcping)
            {
                await RunTcpingAsync(lstSelected);
                return;
            }

            var pageSize = _config.SpeedTestItem.SpeedTestPageSize;
            if (pageSize is <= 0 or > 1000)
            {
                pageSize = 1000;
            }

            List<List<ServerTestItem>> lstTest = new();
            var lst1 = lstSelected.Where(t => t.ConfigType is not (EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.WireGuard)).ToList();
            var lst2 = lstSelected.Where(t => t.ConfigType is EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.WireGuard).ToList();

            for (var num = 0; num < (int)Math.Ceiling(lst1.Count * 1.0 / pageSize); num++)
            {
                lstTest.Add(lst1.Skip(num * pageSize).Take(pageSize).ToList());
            }
            for (var num = 0; num < (int)Math.Ceiling(lst2.Count * 1.0 / pageSize); num++)
            {
                lstTest.Add(lst2.Skip(num * pageSize).Take(pageSize).ToList());
            }

            foreach (var lst in lstTest)
            {
                switch (actionType)
                {
                    case ESpeedActionType.Realping:
                        await RunRealPingAsync(lst);
                        break;

                    case ESpeedActionType.Speedtest:
                        await RunSpeedTestAsync(lst);
                        break;

                    case ESpeedActionType.Mixedtest:
                        await RunMixedTestAsync(lst);
                        break;
                }

                await Task.Delay(100);
            }

            UpdateFunc("", ResUI.SpeedtestingCompleted);
        }

        private void ExitLoop(string x)
        {
            if (_exitLoop) return;
            _exitLoop = true;
            UpdateFunc("", ResUI.SpeedtestingStop);
        }

        private async Task RunTcpingAsync(List<ServerTestItem> selecteds)
        {
            try
            {
                List<Task> tasks = [];
                foreach (var it in selecteds)
                {
                    if (it.ConfigType == EConfigType.Custom)
                    {
                        continue;
                    }
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var time = await GetTcpingTime(it.Address, it.Port);
                            var output = FormatOut(time, Global.DelayUnit);

                            ProfileExHandler.Instance.SetTestDelay(it.IndexId, output);
                            UpdateFunc(it.IndexId, output);
                        }
                        catch (Exception ex)
                        {
                            Logging.SaveLog(_tag, ex);
                        }
                    }));
                }
                Task.WaitAll([.. tasks]);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
            finally
            {
                await ProfileExHandler.Instance.SaveTo();
            }
        }

        private async Task RunRealPingAsync(List<ServerTestItem> selecteds)
        {
            var pid = -1;
            try
            {
                pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(selecteds);
                if (pid < 0)
                {
                    UpdateFunc("", ResUI.FailedToRunCore);
                    return;
                }

                var downloadHandle = new DownloadService();

                List<Task> tasks = new();
                foreach (var it in selecteds)
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
                            var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
                            var output = await GetRealPingTime(downloadHandle, webProxy);

                            ProfileExHandler.Instance.SetTestDelay(it.IndexId, output);
                            UpdateFunc(it.IndexId, output);
                            int.TryParse(output, out var delay);
                            it.Delay = delay;
                        }
                        catch (Exception ex)
                        {
                            Logging.SaveLog(_tag, ex);
                        }
                    }));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
            finally
            {
                if (pid > 0)
                {
                    await CoreHandler.Instance.CoreStopPid(pid);
                }
                await ProfileExHandler.Instance.SaveTo();
            }
        }

        private async Task RunSpeedTestAsync(List<ServerTestItem> selecteds)
        {
            var pid = -1;
            pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            var url = _config.SpeedTestItem.SpeedTestUrl;
            var timeout = _config.SpeedTestItem.SpeedTestTimeout;

            DownloadService downloadHandle = new();

            foreach (var it in selecteds)
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

                var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");

                await downloadHandle.DownloadDataAsync(url, webProxy, timeout, (success, msg) =>
                {
                    decimal.TryParse(msg, out var dec);
                    if (dec > 0)
                    {
                        ProfileExHandler.Instance.SetTestSpeed(it.IndexId, msg);
                    }
                    UpdateFunc(it.IndexId, "", msg);
                });
            }

            if (pid > 0)
            {
                await CoreHandler.Instance.CoreStopPid(pid);
            }
            await ProfileExHandler.Instance.SaveTo();
        }

        private async Task RunSpeedTestMulti(List<ServerTestItem> selecteds)
        {
            var pid = -1;
            pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(selecteds);
            if (pid < 0)
            {
                UpdateFunc("", ResUI.FailedToRunCore);
                return;
            }

            var url = _config.SpeedTestItem.SpeedTestUrl;
            var timeout = _config.SpeedTestItem.SpeedTestTimeout;

            DownloadService downloadHandle = new();

            foreach (var it in selecteds)
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

                var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
                _ = downloadHandle.DownloadDataAsync(url, webProxy, timeout, (success, msg) =>
                {
                    decimal.TryParse(msg, out var dec);
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
                await CoreHandler.Instance.CoreStopPid(pid);
            }
            await ProfileExHandler.Instance.SaveTo();
        }

        private async Task RunMixedTestAsync(List<ServerTestItem> selecteds)
        {
            await RunRealPingAsync(selecteds);

            await Task.Delay(1000);

            await RunSpeedTestMulti(selecteds);
        }

        private async Task<string> GetRealPingTime(DownloadService downloadHandle, IWebProxy webProxy)
        {
            var responseTime = await downloadHandle.GetRealPingTime(_config.SpeedTestItem.SpeedPingTestUrl, webProxy, 10);
            //string output = Utile.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : status;
            return FormatOut(responseTime, Global.DelayUnit);
        }

        private async Task<int> GetTcpingTime(string url, int port)
        {
            var responseTime = -1;

            try
            {
                if (!IPAddress.TryParse(url, out var ipAddress))
                {
                    var ipHostInfo = await Dns.GetHostEntryAsync(url);
                    ipAddress = ipHostInfo.AddressList.First();
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
                Logging.SaveLog(_tag, ex);
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