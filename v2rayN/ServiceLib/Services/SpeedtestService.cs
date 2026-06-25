using ServiceLib.UdpTest;

namespace ServiceLib.Services;

public class SpeedtestService(Config config, Func<SpeedTestResult, Task> updateFunc)
{
    private static readonly string _tag = "SpeedtestService";
    private readonly Config? _config = config;
    private readonly Func<SpeedTestResult, Task>? _updateFunc = updateFunc;
    private static readonly ConcurrentBag<string> _lstExitLoop = [];
    private readonly int _speedTestPageSize = config.SpeedTestItem.SpeedTestPageSize ?? Global.SpeedTestPageSize;
    private readonly TimeSpan _delayInterval = TimeSpan.FromSeconds(config.SpeedTestItem.SpeedTestDelayInterval ?? 1);

    public void RunLoop(ESpeedActionType actionType, List<ProfileItem> selecteds)
    {
        Task.Run(async () =>
        {
            await RunAsync(actionType, selecteds);
            await ProfileExManager.Instance.SaveTo();
            await UpdateFunc("", ResUI.SpeedtestingCompleted);
        });
    }

    public void ExitLoop()
    {
        if (!_lstExitLoop.IsEmpty)
        {
            _ = UpdateFunc("", ResUI.SpeedtestingStop);

            _lstExitLoop.Clear();
        }
    }

    private static bool ShouldStopTest(string exitLoopKey)
    {
        return _lstExitLoop.All(p => p != exitLoopKey);
    }

    private async Task RunAsync(ESpeedActionType actionType, List<ProfileItem> selecteds)
    {
        var exitLoopKey = Utils.GetGuid(false);
        _lstExitLoop.Add(exitLoopKey);

        var lstSelected = await GetClearItem(actionType, selecteds);

        switch (actionType)
        {
            case ESpeedActionType.Tcping:
                await RunTcpingAsync(lstSelected, exitLoopKey);
                break;

            case ESpeedActionType.Realping:
                await RunRealPingBatchAsync(lstSelected, exitLoopKey);
                break;

            case ESpeedActionType.UdpTest:
                await RunUdpTestBatchAsync(lstSelected, exitLoopKey);
                break;

            case ESpeedActionType.Speedtest:
                await RunMixedTestAsync(lstSelected, 1, true, exitLoopKey);
                break;

            case ESpeedActionType.Mixedtest:
                await RunMixedTestAsync(lstSelected, _config.SpeedTestItem.MixedConcurrencyCount, true, exitLoopKey);
                break;
        }
    }

    private async Task<List<ServerTestItem>> GetClearItem(ESpeedActionType actionType, List<ProfileItem> selecteds)
    {
        var lstSelected = new List<ServerTestItem>(selecteds.Count);
        var ids = selecteds.Where(it => !it.IndexId.IsNullOrEmpty()
            && it.ConfigType != EConfigType.Custom
            && (it.ConfigType.IsComplexType() || it.Port > 0))
            .Select(it => it.IndexId)
            .ToList();
        var profileMap = await AppManager.Instance.GetProfileItemsByIndexIdsAsMap(ids);
        for (var i = 0; i < selecteds.Count; i++)
        {
            var it = selecteds[i];
            if (it.ConfigType == EConfigType.Custom)
            {
                continue;
            }

            if (!it.ConfigType.IsComplexType() && it.Port <= 0)
            {
                continue;
            }

            var profile = profileMap.GetValueOrDefault(it.IndexId, it);
            lstSelected.Add(new ServerTestItem()
            {
                IndexId = it.IndexId,
                Address = it.Address,
                Port = it.Port,
                ConfigType = it.ConfigType,
                QueueNum = i,
                Profile = profile,
                CoreType = AppManager.Instance.GetCoreType(profile, it.ConfigType),
            });
        }

        //clear test result
        foreach (var it in lstSelected)
        {
            switch (actionType)
            {
                case ESpeedActionType.Tcping:
                case ESpeedActionType.Realping:
                case ESpeedActionType.UdpTest:
                    await UpdateFunc(it.IndexId, ResUI.Speedtesting, "");
                    ProfileExManager.Instance.SetTestDelay(it.IndexId, 0);
                    break;

                case ESpeedActionType.Speedtest:
                    await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingWait);
                    ProfileExManager.Instance.SetTestSpeed(it.IndexId, 0);
                    break;

                case ESpeedActionType.Mixedtest:
                    await UpdateFunc(it.IndexId, ResUI.Speedtesting, ResUI.SpeedtestingWait);
                    ProfileExManager.Instance.SetTestDelay(it.IndexId, 0);
                    ProfileExManager.Instance.SetTestSpeed(it.IndexId, 0);
                    break;
            }
        }

        if (lstSelected.Count > 1 && (actionType == ESpeedActionType.Speedtest || actionType == ESpeedActionType.Mixedtest))
        {
            NoticeManager.Instance.Enqueue(ResUI.SpeedtestingPressEscToExit);
        }

        return lstSelected;
    }

    private async Task RunTcpingAsync(List<ServerTestItem> selecteds, string exitLoopKey)
    {
        var pageSize = Math.Min(selecteds.Count, _speedTestPageSize);
        var lstBatch = GetTestBatchItem(selecteds, pageSize);

        foreach (var lst in lstBatch)
        {
            if (ShouldStopTest(exitLoopKey))
            {
                await UpdateFunc("", ResUI.SpeedtestingSkip);
                return;
            }

            List<Task> tasks = [];

            foreach (var it in lst)
            {
                if (ShouldStopTest(exitLoopKey))
                {
                    return;
                }

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var responseTime = await GetTcpingTime(it.Address, it.Port);

                        ProfileExManager.Instance.SetTestDelay(it.IndexId, responseTime);
                        await UpdateFunc(it.IndexId, responseTime.ToString());
                    }
                    catch (Exception ex)
                    {
                        Logging.SaveLog(_tag, ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            if (ShouldStopTest(exitLoopKey))
            {
                return;
            }

            await Task.Delay(_delayInterval);
        }
    }

    private async Task RunRealPingBatchAsync(List<ServerTestItem> lstSelected, string exitLoopKey, int pageSize = 0)
    {
        if (pageSize <= 0)
        {
            pageSize = Math.Min(lstSelected.Count, _speedTestPageSize);
        }
        var lstTest = GetTestBatchItem(lstSelected, pageSize);

        List<ServerTestItem> lstFailed = [];
        foreach (var lst in lstTest)
        {
            var ret = await RunRealPingAsync(lst, exitLoopKey);
            if (ret == false)
            {
                lstFailed.AddRange(lst);
            }
            await Task.Delay(_delayInterval);
        }

        //Retest the failed part
        var pageSizeNext = pageSize / 2;
        if (lstFailed.Count > 0 && pageSizeNext > 0)
        {
            if (ShouldStopTest(exitLoopKey))
            {
                await UpdateFunc("", ResUI.SpeedtestingSkip);
                return;
            }

            await UpdateFunc("", string.Format(ResUI.SpeedtestingTestFailedPart, lstFailed.Count));

            if (pageSizeNext > _config.SpeedTestItem.MixedConcurrencyCount)
            {
                await RunRealPingBatchAsync(lstFailed, exitLoopKey, pageSizeNext);
            }
            else
            {
                await RunMixedTestAsync(lstSelected, _config.SpeedTestItem.MixedConcurrencyCount, false, exitLoopKey);
            }
        }
    }

    private async Task<bool> RunRealPingAsync(List<ServerTestItem> selecteds, string exitLoopKey)
    {
        ProcessService processService = null;
        try
        {
            processService = await CoreManager.Instance.LoadCoreConfigSpeedtest(selecteds);
            if (processService is null)
            {
                return false;
            }
            await Task.Delay(1000);

            List<Task> tasks = [];
            foreach (var it in selecteds)
            {
                if (!it.AllowTest)
                {
                    await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip);
                    continue;
                }

                if (ShouldStopTest(exitLoopKey))
                {
                    return false;
                }

                tasks.Add(Task.Run(async () =>
                {
                    await DoRealPing(it);
                }));
            }
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            if (processService != null)
            {
                await processService?.StopAsync();
            }
        }
        return true;
    }

    private async Task RunUdpTestBatchAsync(List<ServerTestItem> lstSelected, string exitLoopKey, int pageSize = 0)
    {
        if (pageSize <= 0)
        {
            pageSize = Math.Min(lstSelected.Count, _speedTestPageSize);
        }
        var lstTest = GetTestBatchItem(lstSelected, pageSize);

        List<ServerTestItem> lstFailed = [];
        foreach (var lst in lstTest)
        {
            var ret = await RunUdpTestAsync(lst, exitLoopKey);
            if (ret == false)
            {
                lstFailed.AddRange(lst);
            }
            await Task.Delay(_delayInterval);
        }

        //Retest the failed part
        if (lstFailed.Count > 0)
        {
            if (ShouldStopTest(exitLoopKey))
            {
                await UpdateFunc("", ResUI.SpeedtestingSkip);
                return;
            }

            await UpdateFunc("", string.Format(ResUI.SpeedtestingTestFailedPart, lstFailed.Count));

            await RunUdpTestAsync(lstFailed, exitLoopKey);
        }
    }

    private async Task<bool> RunUdpTestAsync(List<ServerTestItem> selecteds, string exitLoopKey)
    {
        ProcessService processService = null;
        try
        {
            processService = await CoreManager.Instance.LoadCoreConfigSpeedtest(selecteds);
            if (processService is null)
            {
                return false;
            }
            await Task.Delay(1000);

            List<Task> tasks = [];
            foreach (var it in selecteds)
            {
                if (!it.AllowTest)
                {
                    continue;
                }

                if (ShouldStopTest(exitLoopKey))
                {
                    return false;
                }

                tasks.Add(Task.Run(async () =>
                {
                    await DoUdpTest(it);
                }));
            }
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            if (processService != null)
            {
                await processService?.StopAsync();
            }
        }
        return true;
    }

    private async Task RunMixedTestAsync(List<ServerTestItem> selecteds, int concurrencyCount, bool blSpeedTest, string exitLoopKey)
    {
        using var concurrencySemaphore = new SemaphoreSlim(concurrencyCount);
        var downloadHandle = new DownloadService();
        List<Task> tasks = [];
        foreach (var it in selecteds)
        {
            if (ShouldStopTest(exitLoopKey))
            {
                await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                continue;
            }
            await concurrencySemaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                ProcessService processService = null;
                try
                {
                    processService = await CoreManager.Instance.LoadCoreConfigSpeedtest(it);
                    if (processService is null)
                    {
                        await UpdateFunc(it.IndexId, "", ResUI.FailedToRunCore);
                        return;
                    }

                    await Task.Delay(1000);

                    var delay = await DoRealPing(it);
                    if (blSpeedTest)
                    {
                        if (ShouldStopTest(exitLoopKey))
                        {
                            await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                            return;
                        }

                        if (delay > 0)
                        {
                            await DoSpeedTest(downloadHandle, it);
                        }
                        else
                        {
                            await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(_tag, ex);
                }
                finally
                {
                    if (processService != null)
                    {
                        await processService?.StopAsync();
                    }
                    concurrencySemaphore.Release();
                }
            }));
        }
        await Task.WhenAll(tasks);
    }

    private async Task<int> DoRealPing(ServerTestItem it)
    {
        var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
        var responseTime = await ConnectionHandler.GetRealPingTime(webProxy, 10);

        ProfileExManager.Instance.SetTestDelay(it.IndexId, responseTime);
        await UpdateFunc(it.IndexId, responseTime.ToString());

        if (responseTime > 0)
        {
            var ipInfo = await ConnectionHandler.GetIPInfo(webProxy);
            var ipStr = ipInfo?.ToString() ?? Global.None;
            ProfileExManager.Instance.SetTestIpInfo(it.IndexId, ipStr);
            await UpdateIpInfoFunc(it.IndexId, ipStr);
        }
        else
        {
            await UpdateIpInfoFunc(it.IndexId, ResUI.SpeedtestingSkip);
        }

        return responseTime;
    }

    private async Task DoSpeedTest(DownloadService downloadHandle, ServerTestItem it)
    {
        await UpdateFunc(it.IndexId, "", ResUI.Speedtesting);

        var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
        var url = _config.SpeedTestItem.SpeedTestUrl;
        var timeout = _config.SpeedTestItem.SpeedTestTimeout;
        await downloadHandle.DownloadDataAsync(url, webProxy, timeout, async (success, msg) =>
        {
            decimal.TryParse(msg, out var dec);
            if (dec > 0)
            {
                ProfileExManager.Instance.SetTestSpeed(it.IndexId, dec);
            }
            await UpdateFunc(it.IndexId, "", msg);
        });
    }

    private async Task<int> DoUdpTest(ServerTestItem it)
    {
        var udpService = UdpTestService.CreateFromTarget(_config?.SpeedTestItem.UdpTestTarget, out var udpTestUrl);
        var responseTime = -1;
        try
        {
            responseTime = (int)(await udpService.SendUdpRequestAsync(udpTestUrl, it.Port, TimeSpan.FromSeconds(5))).TotalMilliseconds;
        }
        catch
        {
            // ignored
        }

        ProfileExManager.Instance.SetTestDelay(it.IndexId, responseTime);
        await UpdateFunc(it.IndexId, responseTime.ToString());
        return responseTime;
    }

    private async Task<int> GetTcpingTime(string url, int port)
    {
        var responseTime = -1;

        if (!IPAddress.TryParse(url, out var ipAddress))
        {
            var ipHostInfo = await Dns.GetHostEntryAsync(url);
            ipAddress = ipHostInfo.AddressList.First();
        }

        IPEndPoint endPoint = new(ipAddress, port);
        using Socket clientSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        var timer = Stopwatch.StartNew();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await clientSocket.ConnectAsync(endPoint, cts.Token).ConfigureAwait(false);
            responseTime = (int)timer.ElapsedMilliseconds;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            timer.Stop();
        }
        return responseTime;
    }

    private List<List<ServerTestItem>> GetTestBatchItem(List<ServerTestItem> lstSelected, int pageSize)
    {
        List<List<ServerTestItem>> lstTest = [];
        var lst1 = lstSelected.Where(t => t.CoreType == ECoreType.Xray).ToList();
        var lst2 = lstSelected.Where(t => t.CoreType == ECoreType.sing_box).ToList();

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

    private async Task UpdateFunc(string indexId, string delay, string speed = "")
    {
        await _updateFunc?.Invoke(new() { IndexId = indexId, Delay = delay, Speed = speed });
        if (indexId.IsNotEmpty() && speed.IsNotEmpty())
        {
            ProfileExManager.Instance.SetTestMessage(indexId, speed);
        }
    }

    private async Task UpdateIpInfoFunc(string indexId, string ip)
    {
        await _updateFunc?.Invoke(new() { IndexId = indexId, IpInfo = ip });
    }
}
