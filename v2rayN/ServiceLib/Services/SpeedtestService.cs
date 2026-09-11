using ServiceLib.UdpTest;

namespace ServiceLib.Services;

public class SpeedtestService(Config config, Func<SpeedTestResult, Task> updateFunc)
{
    private static readonly string _tag = "SpeedtestService";
    private readonly Config? _config = config;
    private readonly Func<SpeedTestResult, Task>? _updateFunc = updateFunc;
    private readonly Lock _runLock = new();
    private CancellationTokenSource? _runCts;
    private readonly int _speedTestPageSize = config.SpeedTestItem.SpeedTestPageSize ?? Global.SpeedTestPageSize;
    private readonly TimeSpan _delayInterval = TimeSpan.FromSeconds(config.SpeedTestItem.SpeedTestDelayInterval ?? 1);

    public Task RunLoop(ESpeedActionType actionType, List<ProfileItem> selecteds, CancellationToken ct = default)
    {
        CancellationTokenSource runCts;

        lock (_runLock)
        {
            _runCts?.Cancel();

            runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _runCts = runCts;
        }

        return RunLoopAsync(actionType, selecteds, runCts);
    }

    public void ExitLoop()
    {
        CancellationTokenSource? runCts;

        lock (_runLock)
        {
            runCts = _runCts;
        }

        if (runCts is not null)
        {
            _ = UpdateFunc("", ResUI.SpeedtestingStop);
            runCts.Cancel();
        }
    }

    private async Task RunLoopAsync(ESpeedActionType actionType, List<ProfileItem> selecteds, CancellationTokenSource runCts)
    {
        try
        {
            await RunAsync(actionType, selecteds, runCts.Token);
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
            // Ignored
        }
        finally
        {
            try
            {
                await ProfileExManager.Instance.SaveTo();
            }
            finally
            {
                await UpdateFunc("", ResUI.SpeedtestingCompleted);
            }

            lock (_runLock)
            {
                if (ReferenceEquals(_runCts, runCts))
                {
                    _runCts = null;
                }
            }

            runCts.Dispose();
        }
    }
    
    private async Task RunAsync(ESpeedActionType actionType, List<ProfileItem> selecteds, CancellationToken ct = default)
    {
        var lstSelected = await GetClearItem(actionType, selecteds);
        var completedIds = new ConcurrentDictionary<string, byte>();

        try
        {
            switch (actionType)
            {
                case ESpeedActionType.Tcping:
                    await RunTcpingAsync(lstSelected, completedIds, ct);
                    break;

                case ESpeedActionType.Realping:
                    await RunRealPingBatchAsync(lstSelected, completedIds, 0, ct);
                    break;

                case ESpeedActionType.UdpTest:
                    await RunUdpTestBatchAsync(lstSelected, completedIds, 0, ct);
                    break;

                case ESpeedActionType.Speedtest:
                    await RunMixedTestAsync(lstSelected, completedIds, 1, true, ct);
                    break;

                case ESpeedActionType.Mixedtest:
                    await RunMixedTestAsync(lstSelected, completedIds, _config.SpeedTestItem.MixedConcurrencyCount, true,
                        ct);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _ = UpdateFunc("", ResUI.SpeedtestingStop);
            await SetTestResultAsync(lstSelected.Where(it => !completedIds.ContainsKey(it.IndexId)).ToList(),
                actionType, ResUI.SpeedtestingSkip).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            _ = UpdateFunc("", ex.Message);
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
        await SetTestResultAsync(lstSelected, actionType, ResUI.Speedtesting).ConfigureAwait(false);

        if (lstSelected.Count > 1 && (actionType == ESpeedActionType.Speedtest || actionType == ESpeedActionType.Mixedtest))
        {
            NoticeManager.Instance.Enqueue(ResUI.SpeedtestingPressEscToExit);
        }

        return lstSelected;
    }

    private async Task SetTestResultAsync(List<ServerTestItem> lstSelected, ESpeedActionType actionType, string message)
    {
        foreach (var it in lstSelected)
        {
            switch (actionType)
            {
                case ESpeedActionType.Tcping:
                case ESpeedActionType.Realping:
                case ESpeedActionType.UdpTest:
                    await UpdateFunc(it.IndexId, message, "");
                    break;
                case ESpeedActionType.Speedtest:
                    await UpdateFunc(it.IndexId, "", message);
                    break;
                case ESpeedActionType.Mixedtest:
                    await UpdateFunc(it.IndexId, message, message);
                    break;
            }
        }
    }

    private async Task RunTcpingAsync(List<ServerTestItem> selecteds,
        ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
    {
        var pageSize = Math.Min(selecteds.Count, _speedTestPageSize);
        var lstBatch = GetTestBatchItem(selecteds, pageSize);

        foreach (var lst in lstBatch)
        {
            ct.ThrowIfCancellationRequested();

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
            };

            await Parallel.ForEachAsync(lst, parallelOptions, async (item, innerCt) =>
            {
                try
                {
                    var responseTime = await GetTcpingTime(item.Address, item.Port, innerCt);

                    ProfileExManager.Instance.SetTestDelay(item.IndexId, responseTime);
                    await UpdateFunc(item.IndexId, responseTime.ToString());
                    completedIds.TryAdd(item.IndexId, 0);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(_tag, ex);
                }
            });

            await Task.Delay(_delayInterval, ct);
        }
    }

    private async Task RunRealPingBatchAsync(List<ServerTestItem> lstSelected,
        ConcurrentDictionary<string, byte> completedIds, int pageSize = 0, CancellationToken ct = default)
    {
        if (pageSize <= 0)
        {
            pageSize = Math.Min(lstSelected.Count, _speedTestPageSize);
        }
        var lstTest = GetTestBatchItem(lstSelected, pageSize);

        List<ServerTestItem> lstFailed = [];
        foreach (var lst in lstTest)
        {
            var ret = await RunRealPingAsync(lst, completedIds, ct);
            if (ret == false)
            {
                lstFailed.AddRange(lst);
            }
            await Task.Delay(_delayInterval, ct);
        }

        //Retest the failed part
        var pageSizeNext = pageSize / 2;
        if (lstFailed.Count > 0 && pageSizeNext > 0)
        {
            ct.ThrowIfCancellationRequested();

            await UpdateFunc("", string.Format(ResUI.SpeedtestingTestFailedPart, lstFailed.Count));

            if (pageSizeNext > _config.SpeedTestItem.MixedConcurrencyCount)
            {
                await RunRealPingBatchAsync(lstFailed, completedIds, pageSizeNext, ct);
            }
            else
            {
                await RunMixedTestAsync(lstSelected, completedIds, _config.SpeedTestItem.MixedConcurrencyCount, false, ct);
            }
        }
    }

    private async Task<bool> RunRealPingAsync(List<ServerTestItem> selecteds,
        ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
    {
        ProcessService processService = null;
        try
        {
            processService = await CoreManager.Instance.LoadCoreConfigSpeedtest(selecteds);
            if (processService is null)
            {
                return false;
            }
            await Task.Delay(1000, ct);

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
            };

            await Parallel.ForEachAsync(selecteds, parallelOptions, async (it, innerCt) =>
            {
                if (!it.AllowTest)
                {
                    await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip);
                    completedIds.TryAdd(it.IndexId, 0);
                    return;
                }

                try
                {
                    await DoRealPing(it, completedIds, innerCt);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(_tag, ex);
                }
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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

    private async Task RunUdpTestBatchAsync(List<ServerTestItem> lstSelected,
        ConcurrentDictionary<string, byte> completedIds, int pageSize = 0, CancellationToken ct = default)
    {
        if (pageSize <= 0)
        {
            pageSize = Math.Min(lstSelected.Count, _speedTestPageSize);
        }
        var lstTest = GetTestBatchItem(lstSelected, pageSize);

        List<ServerTestItem> lstFailed = [];
        foreach (var lst in lstTest)
        {
            var ret = await RunUdpTestAsync(lst, completedIds, ct);
            if (ret == false)
            {
                lstFailed.AddRange(lst);
            }
            await Task.Delay(_delayInterval, ct);
        }

        //Retest the failed part
        if (lstFailed.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            await UpdateFunc("", string.Format(ResUI.SpeedtestingTestFailedPart, lstFailed.Count));

            await RunUdpTestAsync(lstFailed, completedIds, ct);
        }
    }

    private async Task<bool> RunUdpTestAsync(List<ServerTestItem> selecteds,
        ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
    {
        ProcessService processService = null;
        try
        {
            processService = await CoreManager.Instance.LoadCoreConfigSpeedtest(selecteds);
            if (processService is null)
            {
                return false;
            }
            await Task.Delay(1000, ct);

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
            };

            await Parallel.ForEachAsync(selecteds, parallelOptions, async (it, innerCt) =>
            {
                if (!it.AllowTest)
                {
                    await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip);
                    completedIds.TryAdd(it.IndexId, 0);
                    return;
                }

                try
                {
                    await DoUdpTest(it, completedIds, innerCt);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(_tag, ex);
                }
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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

    private async Task RunMixedTestAsync(List<ServerTestItem> selecteds,
        ConcurrentDictionary<string, byte> completedIds, int concurrencyCount, bool blSpeedTest,
        CancellationToken ct = default)
    {
        var downloadHandle = new DownloadService();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrencyCount,
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(selecteds, parallelOptions, async (it, innerCt) =>
        {
            innerCt.ThrowIfCancellationRequested();

            ProcessService processService = null;
            try
            {
                processService = await CoreManager.Instance.LoadCoreConfigSpeedtest(it);
                if (processService is null)
                {
                    await UpdateFunc(it.IndexId, "", ResUI.FailedToRunCore);
                    return;
                }

                await Task.Delay(1000, innerCt);

                var delay = await DoRealPing(it, completedIds, innerCt);
                if (blSpeedTest)
                {
                    if (delay > 0)
                    {
                        await DoSpeedTest(downloadHandle, it, completedIds, innerCt);
                    }
                    else
                    {
                        await UpdateFunc(it.IndexId, "", ResUI.SpeedtestingSkip);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
            finally
            {
                if (processService != null)
                {
                    await processService.StopAsync();
                }
            }
        });
    }

    private async Task<int> DoRealPing(ServerTestItem it,
        ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
    {
        var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
        var responseTime = await ConnectionHandler.GetRealPingTime(webProxy, ct);

        ProfileExManager.Instance.SetTestDelay(it.IndexId, responseTime);
        await UpdateFunc(it.IndexId, responseTime.ToString());

        if (!_config.UiItem.HideColumnIpInfo && responseTime > 0)
        {
            var ipInfo = await ConnectionHandler.GetIPInfo(webProxy, ct);
            var ipStr = ipInfo?.ToString() ?? Global.None;
            ProfileExManager.Instance.SetTestIpInfo(it.IndexId, ipStr);
            await UpdateIpInfoFunc(it.IndexId, ipStr);
        }
        else
        {
            await UpdateIpInfoFunc(it.IndexId, ResUI.SpeedtestingSkip);
        }

        completedIds.TryAdd(it.IndexId, 0);
        return responseTime;
    }

    private async Task DoSpeedTest(DownloadService downloadHandle, ServerTestItem it,
        ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
    {
        await UpdateFunc(it.IndexId, "", ResUI.Speedtesting);

        var webProxy = new WebProxy($"socks5://{Global.Loopback}:{it.Port}");
        var url = _config.SpeedTestItem.SpeedTestUrl;
        var timeout = _config.SpeedTestItem.SpeedTestTimeout;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var linkedCt = linkedCts.Token;
        await downloadHandle.DownloadDataAsync(url, webProxy, async (success, msg) =>
        {
            decimal.TryParse(msg, out var dec);
            if (dec > 0)
            {
                ProfileExManager.Instance.SetTestSpeed(it.IndexId, dec);
            }
            await UpdateFunc(it.IndexId, "", msg);
        }, linkedCt);
        completedIds.TryAdd(it.IndexId, 0);
    }

    private async Task<int> DoUdpTest(ServerTestItem it,
        ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
    {
        var udpService = UdpTestService.CreateFromTarget(_config?.SpeedTestItem.UdpTestTarget, out var udpTestUrl);
        var responseTime = (int)(await udpService.SendUdpRequestAsync(udpTestUrl, it.Port, ct)).TotalMilliseconds;

        ProfileExManager.Instance.SetTestDelay(it.IndexId, responseTime);
        await UpdateFunc(it.IndexId, responseTime.ToString());
        completedIds.TryAdd(it.IndexId, 0);
        return responseTime;
    }

    private async Task<int> GetTcpingTime(string? url, int port, CancellationToken ct = default)
    {
        var responseTime = -1;

        if (url.IsNullOrEmpty() || port <= 0)
        {
            return responseTime;
        }

        if (!IPAddress.TryParse(url, out var ipAddress))
        {
            var ipHostInfo = await Dns.GetHostEntryAsync(url, ct);
            ipAddress = ipHostInfo.AddressList.First();
        }

        IPEndPoint endPoint = new(ipAddress, port);
        using Socket clientSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        var timer = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await clientSocket.ConnectAsync(endPoint, linkedCts.Token).ConfigureAwait(false);
            responseTime = (int)timer.ElapsedMilliseconds;
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
