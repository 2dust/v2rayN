using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ServiceLib.Services
{
    public class SpeedtestService
    {
        private static readonly string _tag = "SpeedtestService";
        private Config? _config;
        private Action<SpeedTestResult>? _updateFunc;
        private static readonly ConcurrentBag<string> _lstExitLoop = new();

        public SpeedtestService(Config config, Action<SpeedTestResult> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;
        }

        public void RunLoop(ESpeedActionType actionType, List<ProfileItem> selecteds)
        {
            Task.Run(async () =>
            {
                await RunAsync(actionType, selecteds);
                await ProfileExHandler.Instance.SaveTo();
                UpdateFunc("", ResUI.SpeedtestingCompleted);

                FileManager.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1));
            });
        }

        public void ExitLoop()
        {
            if (_lstExitLoop.Count > 0)
            {
                UpdateFunc("", ResUI.SpeedtestingStop);

                _lstExitLoop.Clear();
            }
        }

        private async Task RunAsync(ESpeedActionType actionType, List<ProfileItem> selecteds)
        {
            var exitLoopKey = Utils.GetGuid(false);
            _lstExitLoop.Add(exitLoopKey);

            var lstSelected = GetClearItem(actionType, selecteds);

            switch (actionType)
            {
                case ESpeedActionType.Tcping:
                    await RunTcpingAsync(lstSelected);
                    break;

                case ESpeedActionType.Realping:
                    await RunRealPingBatchAsync(lstSelected, exitLoopKey);
                    break;

                case ESpeedActionType.Speedtest:
                    await RunMixedTestAsync(lstSelected, 1, exitLoopKey);
                    break;

                case ESpeedActionType.Mixedtest:
                    await RunMixedTestAsync(lstSelected, 5, exitLoopKey);
                    break;
            }
        }

        private List<ServerTestItem> GetClearItem(ESpeedActionType actionType, List<ProfileItem> selecteds)
        {
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
                    ConfigType = it.ConfigType,
                    QueueNum = selecteds.IndexOf(it)
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

            return lstSelected;
        }

        private async Task RunTcpingAsync(List<ServerTestItem> selecteds)
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

        private async Task RunRealPingBatchAsync(List<ServerTestItem> lstSelected, string exitLoopKey, int pageSize = 0)
        {
            if (pageSize <= 0)
            {
                pageSize = lstSelected.Count < Global.SpeedTestPageSize ? lstSelected.Count : Global.SpeedTestPageSize;
            }
            var lstTest = GetTestBatchItem(lstSelected, pageSize);

            List<ServerTestItem> lstFailed = new();
            foreach (var lst in lstTest)
            {
                var ret = await RunRealPingAsync(lst, exitLoopKey);
                if (ret == false)
                {
                    lstFailed.AddRange(lst);
                }
                await Task.Delay(100);
            }

            //Retest the failed part
            var pageSizeNext = pageSize / 2;
            if (lstFailed.Count > 0 && pageSizeNext > 0)
            {
                if (_lstExitLoop.Any(p => p == exitLoopKey) == false)
                {
                    UpdateFunc("", ResUI.SpeedtestingSkip);
                    return;
                }

                UpdateFunc("", string.Format(ResUI.SpeedtestingTestFailedPart, lstFailed.Count));
                await RunRealPingBatchAsync(lstFailed, exitLoopKey, pageSizeNext);
            }
        }

        private async Task<bool> RunRealPingAsync(List<ServerTestItem> selecteds, string exitLoopKey)
        {
            var pid = -1;
            try
            {
                pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(selecteds);
                if (pid < 0)
                {
                    return false;
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
                        await DoRealPing(downloadHandle, it);
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
                    await ProcUtils.ProcessKill(pid);
                }
            }
            return true;
        }

        private async Task RunMixedTestAsync(List<ServerTestItem> selecteds, int concurrencyCount, string exitLoopKey)
        {
            using var concurrencySemaphore = new SemaphoreSlim(concurrencyCount);
            var downloadHandle = new DownloadService();
            List<Task> tasks = new();
            foreach (var it in selecteds)
            {
                if (_lstExitLoop.Any(p => p == exitLoopKey) == false)
                {
                    UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                    continue;
                }
                if (it.ConfigType == EConfigType.Custom)
                {
                    continue;
                }
                await concurrencySemaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    var pid = -1;
                    try
                    {
                        pid = await CoreHandler.Instance.LoadCoreConfigSpeedtest(it);
                        if (pid > 0)
                        {
                            await Task.Delay(1000);
                            await DoRealPing(downloadHandle, it);
                            await DoSpeedTest(downloadHandle, it);
                        }
                        else
                        {
                            UpdateFunc(it.IndexId, "", ResUI.FailedToRunCore);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.SaveLog(_tag, ex);
                    }
                    finally
                    {
                        if (pid > 0)
                        {
                            await ProcUtils.ProcessKill(pid);
                        }
                        concurrencySemaphore.Release();
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
        }

        private async Task DoRealPing(DownloadService downloadHandle, ServerTestItem it)
        {
            var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
            var responseTime = await downloadHandle.GetRealPingTime(_config.SpeedTestItem.SpeedPingTestUrl, webProxy, 10);
            var output = FormatOut(responseTime, Global.DelayUnit);

            ProfileExHandler.Instance.SetTestDelay(it.IndexId, output);
            UpdateFunc(it.IndexId, output);
        }

        private async Task DoSpeedTest(DownloadService downloadHandle, ServerTestItem it)
        {
            ProfileExHandler.Instance.SetTestSpeed(it.IndexId, "-1");
            UpdateFunc(it.IndexId, "", ResUI.Speedtesting);

            var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
            var url = _config.SpeedTestItem.SpeedTestUrl;
            var timeout = _config.SpeedTestItem.SpeedTestTimeout;
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

        private List<List<ServerTestItem>> GetTestBatchItem(List<ServerTestItem> lstSelected, int pageSize)
        {
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

            return lstTest;
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
