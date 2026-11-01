using ServiceLib.UdpTest;

namespace ServiceLib.Services;

public class SpeedtestService(Config config, Func<SpeedTestResult, Task> updateFunc)
{
    private static readonly string _tag = "SpeedtestService";
    private readonly Config? _config = config;
    private readonly Func<SpeedTestResult, Task>? _updateFunc = updateFunc;
    private readonly Lock _runLock = new();
    private readonly List<CancellationTokenSource> _runCtsList = [];
    private readonly int _speedTestPageSize = config.SpeedTestItem.SpeedTestPageSize ?? Global.SpeedTestPageSize;
    private readonly TimeSpan _delayInterval = TimeSpan.FromSeconds(config.SpeedTestItem.SpeedTestDelayInterval ?? 1);

    public Task RunLoop(ESpeedActionType actionType, List<ProfileItem> selecteds, CancellationToken ct = default)
    {
        CancellationTokenSource runCts;

        lock (_runLock)
        {
            runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _runCtsList.Add(runCts);
        }

        return RunLoopAsync(actionType, selecteds, runCts);
    }

    public void ExitLoop()
    {
        var counter = 0;
        List<CancellationTokenSource> listToCancel;

        lock (_runLock)
        {
            listToCancel = _runCtsList.ToList();
            counter = listToCancel.Count;
        }

        foreach (var cts in listToCancel)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignored
            }
        }

        if (counter > 0)
        {
            _ = UpdateFunc("", ResUI.SpeedtestingStop);
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
                _runCtsList.Remove(runCts);
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
        finally
        {
            // 兜底：无论什么原因没测到的节点，都不要一直停在"测试中"
            var notTested = lstSelected.Where(it => !completedIds.ContainsKey(it.IndexId ?? string.Empty)).ToList();
            if (notTested.Count > 0)
            {
                await SetTestResultAsync(notTested, actionType, ResUI.SpeedtestingSkip).ConfigureAwait(false);
            }
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
                MaxDegreeOfParallelism = lst.Count,
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
            //每个批次内按核心类型再拆分，避免 mihomo / Xray / sing-box 节点混在同一个配置里
            foreach (var coreGroup in lst.GroupBy(t => t.CoreType))
            {
                var sub = coreGroup.ToList();
                var ret = await RunRealPingAsync(sub, completedIds, ct);
                if (ret == false)
                {
                    lstFailed.AddRange(sub);
                }
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
                Logging.SaveLog($"{_tag} ping-batch core-is-null count={selecteds.Count}");
                return false;
            }
            Logging.SaveLog($"{_tag} ping-batch core={selecteds.FirstOrDefault()?.CoreType} count={selecteds.Count} ports={string.Join(",", selecteds.Select(t => t.Port))}");
            await WaitForLocalPort(selecteds.FirstOrDefault(t => t.Port > 0)?.Port ?? 0, ct, 8000);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = selecteds.Count,
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
            //每个批次内按核心类型再拆分
            foreach (var coreGroup in lst.GroupBy(t => t.CoreType))
            {
                var sub = coreGroup.ToList();
                var ret = await RunUdpTestAsync(sub, completedIds, ct);
                if (ret == false)
                {
                    lstFailed.AddRange(sub);
                }
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
            await WaitForLocalPort(selecteds.FirstOrDefault(t => t.Port > 0)?.Port ?? 0, ct, 8000);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = selecteds.Count,
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

                await WaitForLocalPort(it.Port, innerCt);
                Logging.SaveLog($"{_tag} mixed index={it.IndexId} port={it.Port} remarks={it.Profile?.Remarks}");

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
        Logging.SaveLog($"{_tag} ping port={it.Port} index={it.IndexId} result={responseTime}");

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
        catch
        {
            // Ignore
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
        if (lstSelected.Count == 0)
        {
            return lstTest;
        }

        pageSize = Math.Max(pageSize, 1);

        // 按核心类型分批：同一批次只能用同一个核心生成配置。
        // 这里不能只挑 Xray/sing_box，否则 mihomo 节点会被丢掉、一直停在"测试中"。
        foreach (var group in lstSelected.GroupBy(t => t.CoreType))
        {
            var list = group.ToList();
            for (var num = 0; num < (int)Math.Ceiling(list.Count * 1.0 / pageSize); num++)
            {
                lstTest.Add(list.Skip(num * pageSize).Take(pageSize).ToList());
            }
        }

        return lstTest;
    }

    /// <summary>
    /// 等待本地代理端口就绪。mihomo 是单文件压缩启动，固定等 1 秒经常还没监听。
    /// </summary>
    /// <summary>
    /// 等待本地代理端口就绪。mihomo 是单文件压缩启动，固定等 1 秒经常还没监听。
    /// 注意：端口监听 != 能转发。mihomo 实测在绑定端口后还有约 1 秒的预热期，
    /// 这段窗口内发请求会直接 SSL 握手失败（表现就是测速秒回 -1），所以这里要额外等一段时间。
    /// </summary>
    private static async Task<bool> WaitForLocalPort(int port, CancellationToken ct, int timeoutMs = 5000, int warmupMs = 1500)
    {
        if (port <= 0)
        {
            return false;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                await tcp.ConnectAsync(Global.Loopback, port, cts.Token);

                // 已能建立 TCP 连接，等核心完成预热再交给测速
                if (warmupMs > 0)
                {
                    try
                    {
                        await Task.Delay(warmupMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // 端口还没起来，继续等
            }

            try
            {
                await Task.Delay(100, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return false;
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
