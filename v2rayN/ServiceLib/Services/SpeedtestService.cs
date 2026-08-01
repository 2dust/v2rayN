using ServiceLib.UdpTest;

namespace ServiceLib.Services;

public class SpeedtestService(Config config, Func<SpeedTestResult, Task> updateFunc, Func<ESpeedTestGroup, bool, Task>? updateRunningFunc = null)
{
    private static readonly string _tag = "SpeedtestService";
    private readonly Config? _config = config;
    private readonly Func<SpeedTestResult, Task>? _updateFunc = updateFunc;
    private readonly Func<ESpeedTestGroup, bool, Task>? _updateRunningFunc = updateRunningFunc;
    private static readonly ConcurrentDictionary<string, SpeedTestRunning> _dicRunning = new();
    private readonly int _speedTestPageSize = config.SpeedTestItem.SpeedTestPageSize ?? Global.SpeedTestPageSize;
    private readonly TimeSpan _delayInterval = TimeSpan.FromSeconds(config.SpeedTestItem.SpeedTestDelayInterval ?? 1);

    private class SpeedTestRunning
    {
        public ESpeedTestGroup Group { get; init; }

        /// <summary>
        /// Profiles that received a delay measurement, so that a stopped run can tell the
        /// untouched ones apart and clear their progress text.
        /// </summary>
        public ConcurrentDictionary<string, byte> DelayMeasured { get; } = new();

        /// <summary>
        /// Deliberately not disposed: it owns no timer and no wait handle, and disposing it
        /// would race with a concurrent stop request coming from the UI thread.
        /// </summary>
        public CancellationTokenSource Cts { get; } = new();
    }

    /// <summary>
    /// The delay buttons and the speed buttons can be started and stopped independently.
    /// </summary>
    public static ESpeedTestGroup GetTestGroup(ESpeedActionType actionType)
    {
        return actionType switch
        {
            ESpeedActionType.Speedtest or ESpeedActionType.Mixedtest => ESpeedTestGroup.Speed,
            _ => ESpeedTestGroup.Delay
        };
    }

    public static bool IsRunning(ESpeedTestGroup group)
    {
        return _dicRunning.Values.Any(t => t.Group == group);
    }

    public void RunLoop(ESpeedActionType actionType, List<ProfileItem> selecteds)
    {
        var group = GetTestGroup(actionType);
        var exitLoopKey = Utils.GetGuid(false);
        var running = new SpeedTestRunning { Group = group };
        _dicRunning[exitLoopKey] = running;

        Task.Run(async () =>
        {
            await UpdateRunningFunc(group);
            try
            {
                await RunAsync(actionType, selecteds, exitLoopKey);
                await ProfileExManager.Instance.SaveTo();
                await UpdateFunc("", ResUI.SpeedtestingCompleted);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
            finally
            {
                _dicRunning.TryRemove(exitLoopKey, out _);
                await UpdateRunningFunc(group);
            }
        });
    }

    /// <summary>
    /// Stops every running test, whichever group it belongs to.
    /// </summary>
    public void ExitLoop()
    {
        StopRunning(_dicRunning.Values.ToList());
    }

    /// <summary>
    /// Stops only the tests of the given group, leaving the other group alone.
    /// </summary>
    public void ExitLoop(ESpeedTestGroup group)
    {
        StopRunning(_dicRunning.Values.Where(t => t.Group == group).ToList());
    }

    /// <summary>
    /// Stops the tests of the given group and waits for them to unwind, so that the other group
    /// can start without briefly overlapping them.
    /// </summary>
    public async Task ExitLoopAndWait(ESpeedTestGroup group, TimeSpan timeout)
    {
        ExitLoop(group);

        var waitUntil = DateTime.Now.Add(timeout);
        while (IsRunning(group) && DateTime.Now < waitUntil)
        {
            await Task.Delay(100);
        }
    }

    private void StopRunning(List<SpeedTestRunning> runnings)
    {
        var stopped = false;
        foreach (var running in runnings)
        {
            if (running.Cts.IsCancellationRequested)
            {
                continue;
            }
            running.Cts.Cancel();
            stopped = true;
        }

        if (stopped)
        {
            _ = UpdateFunc("", ResUI.SpeedtestingStop);
        }
    }

    private static bool ShouldStopTest(string exitLoopKey)
    {
        return !_dicRunning.TryGetValue(exitLoopKey, out var running) || running.Cts.IsCancellationRequested;
    }

    private static CancellationToken GetToken(string exitLoopKey)
    {
        return _dicRunning.TryGetValue(exitLoopKey, out var running)
            ? running.Cts.Token
            : new CancellationToken(true);
    }

    /// <summary>
    /// Waits for the given interval, returning as soon as the test is stopped.
    /// </summary>
    private static async Task DelayAsync(TimeSpan delay, string exitLoopKey)
    {
        // WhenAny observes the cancellation without rethrowing it, so callers fall through to
        // their next ShouldStopTest checkpoint instead of unwinding through an exception.
        await Task.WhenAny(Task.Delay(delay, GetToken(exitLoopKey)));
    }

    private async Task RunAsync(ESpeedActionType actionType, List<ProfileItem> selecteds, string exitLoopKey)
    {
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

        if (ShouldStopTest(exitLoopKey))
        {
            await ClearUnmeasuredDelays(actionType, lstSelected, exitLoopKey);
        }
    }

    /// <summary>
    /// A stopped run leaves "testing" in the delay column of the profiles it never reached.
    /// Replace that with the skipped text so the grid does not look stuck.
    /// </summary>
    private async Task ClearUnmeasuredDelays(ESpeedActionType actionType, List<ServerTestItem> lstSelected, string exitLoopKey)
    {
        //Speedtest leaves the previous delay visible, so it has no progress text to clear
        if (actionType is not (ESpeedActionType.Tcping
            or ESpeedActionType.Realping
            or ESpeedActionType.UdpTest
            or ESpeedActionType.Mixedtest))
        {
            return;
        }

        _dicRunning.TryGetValue(exitLoopKey, out var running);
        foreach (var it in lstSelected)
        {
            if (running is not null && running.DelayMeasured.ContainsKey(it.IndexId))
            {
                continue;
            }
            await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip);
        }
    }

    /// <summary>
    /// Records a delay measurement and remembers that this profile was actually measured.
    /// </summary>
    private async Task SetDelayResult(string exitLoopKey, string indexId, int responseTime)
    {
        ProfileExManager.Instance.SetTestDelay(indexId, responseTime);
        if (_dicRunning.TryGetValue(exitLoopKey, out var running))
        {
            running.DelayMeasured[indexId] = 0;
        }
        await UpdateFunc(indexId, responseTime.ToString());
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

        if (lstSelected.Count > 1)
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
                        var responseTime = await GetTcpingTime(it.Address, it.Port, GetToken(exitLoopKey));
                        if (ShouldStopTest(exitLoopKey))
                        {
                            //a stopped test reports no measurement, so keep the previous value
                            return;
                        }

                        await SetDelayResult(exitLoopKey, it.IndexId, responseTime);
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

            await DelayAsync(_delayInterval, exitLoopKey);
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
            if (ShouldStopTest(exitLoopKey))
            {
                await UpdateFunc("", ResUI.SpeedtestingSkip);
                return;
            }

            var ret = await RunRealPingAsync(lst, exitLoopKey);
            if (ret == false)
            {
                lstFailed.AddRange(lst);
            }
            await DelayAsync(_delayInterval, exitLoopKey);
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
            await DelayAsync(TimeSpan.FromSeconds(1), exitLoopKey);

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
                    await DoRealPing(it, exitLoopKey);
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
            if (ShouldStopTest(exitLoopKey))
            {
                await UpdateFunc("", ResUI.SpeedtestingSkip);
                return;
            }

            var ret = await RunUdpTestAsync(lst, exitLoopKey);
            if (ret == false)
            {
                lstFailed.AddRange(lst);
            }
            await DelayAsync(_delayInterval, exitLoopKey);
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
            await DelayAsync(TimeSpan.FromSeconds(1), exitLoopKey);

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
                    await DoUdpTest(it, exitLoopKey);
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

            //WaitAsync with a timeout returns false instead of throwing when the test is stopped
            var acquired = false;
            while (!acquired && !ShouldStopTest(exitLoopKey))
            {
                acquired = await concurrencySemaphore.WaitAsync(TimeSpan.FromMilliseconds(200));
            }
            if (ShouldStopTest(exitLoopKey))
            {
                if (acquired)
                {
                    concurrencySemaphore.Release();
                }
                await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                continue;
            }

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

                    await DelayAsync(TimeSpan.FromSeconds(1), exitLoopKey);

                    if (ShouldStopTest(exitLoopKey))
                    {
                        await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                        return;
                    }

                    var delay = await DoRealPing(it, exitLoopKey);
                    if (blSpeedTest)
                    {
                        if (ShouldStopTest(exitLoopKey))
                        {
                            await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                            return;
                        }

                        if (delay > 0)
                        {
                            await DoSpeedTest(downloadHandle, it, GetToken(exitLoopKey));
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

    private async Task<int> DoRealPing(ServerTestItem it, string exitLoopKey)
    {
        var token = GetToken(exitLoopKey);
        var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
        var responseTime = await ConnectionHandler.GetRealPingTime(webProxy, token: token);

        await SetDelayResult(exitLoopKey, it.IndexId, responseTime);

        if (token.IsCancellationRequested)
        {
            return responseTime;
        }

        if (!_config.UiItem.HideColumnIpInfo && responseTime > 0)
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

    private async Task DoSpeedTest(DownloadService downloadHandle, ServerTestItem it, CancellationToken token)
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
        }, token);
    }

    private async Task<int> DoUdpTest(ServerTestItem it, string exitLoopKey)
    {
        var udpService = UdpTestService.CreateFromTarget(_config?.SpeedTestItem.UdpTestTarget, out var udpTestUrl);
        var responseTime = -1;
        try
        {
            responseTime = (int)(await udpService.SendUdpRequestAsync(udpTestUrl, it.Port, TimeSpan.FromSeconds(5), GetToken(exitLoopKey))).TotalMilliseconds;
        }
        catch
        {
            // ignored
        }

        await SetDelayResult(exitLoopKey, it.IndexId, responseTime);
        return responseTime;
    }

    /// <summary>
    /// Measures the TCP handshake time, returning -1 when the host cannot be reached in time
    /// or the test was stopped.
    /// </summary>
    private async Task<int> GetTcpingTime(string url, int port, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            if (!IPAddress.TryParse(url, out var ipAddress))
            {
                var ipHostInfo = await Dns.GetHostEntryAsync(url, cts.Token);
                ipAddress = ipHostInfo.AddressList.First();
            }

            IPEndPoint endPoint = new(ipAddress, port);
            using Socket clientSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            //start timing after name resolution so that only the handshake is measured
            var timer = Stopwatch.StartNew();
            await clientSocket.ConnectAsync(endPoint, cts.Token).ConfigureAwait(false);
            return (int)timer.ElapsedMilliseconds;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
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

    private async Task UpdateRunningFunc(ESpeedTestGroup group)
    {
        if (_updateRunningFunc is null)
        {
            return;
        }
        await _updateRunningFunc.Invoke(group, IsRunning(group));
    }
}
