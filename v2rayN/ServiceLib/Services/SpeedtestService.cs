using ServiceLib.UdpTest;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

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
                var regularForRealping = lstSelected.Where(it => it.ConfigType != EConfigType.Custom).ToList();
                var customForRealping = lstSelected.Where(it => it.ConfigType == EConfigType.Custom).ToList();
                await RunRealPingBatchAsync(regularForRealping, exitLoopKey);
                await RunMixedTestAsync(customForRealping, Math.Max(1, customForRealping.Count), false, exitLoopKey);
                break;

            case ESpeedActionType.UdpTest:
                var regularForUdp = lstSelected.Where(it => it.ConfigType != EConfigType.Custom).ToList();
                var customForUdp = lstSelected.Where(it => it.ConfigType == EConfigType.Custom).ToList();
                await RunUdpTestBatchAsync(regularForUdp, exitLoopKey);
                foreach (var it in customForUdp)
                {
                    await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip, "");
                }
                break;

            case ESpeedActionType.Speedtest:
                var regularForSpeed = lstSelected.Where(it => it.ConfigType != EConfigType.Custom).ToList();
                var customForSpeed = lstSelected.Where(it => it.ConfigType == EConfigType.Custom).ToList();
                await RunMixedTestAsync(regularForSpeed, 1, true, exitLoopKey);
                await RunMixedTestAsync(customForSpeed, 1, true, exitLoopKey);
                break;

            case ESpeedActionType.Mixedtest:
                var regularForMixed = lstSelected.Where(it => it.ConfigType != EConfigType.Custom).ToList();
                var customForMixed = lstSelected.Where(it => it.ConfigType == EConfigType.Custom).ToList();
                await RunMixedTestAsync(regularForMixed, _config.SpeedTestItem.MixedConcurrencyCount, true, exitLoopKey);
                await RunMixedTestAsync(customForMixed, _config.SpeedTestItem.MixedConcurrencyCount, true, exitLoopKey);
                break;
        }
    }

    private async Task<List<ServerTestItem>> GetClearItem(ESpeedActionType actionType, List<ProfileItem> selecteds)
    {
        var lstSelected = new List<ServerTestItem>(selecteds.Count);

        var ids = selecteds
            .Where(it => !it.IndexId.IsNullOrEmpty()
                && (it.ConfigType == EConfigType.Custom
                    || it.ConfigType.IsComplexType()
                    || it.Port > 0))
            .Select(it => it.IndexId)
            .ToList();

        var profileMap = await AppManager.Instance.GetProfileItemsByIndexIdsAsMap(ids);

        for (var i = 0; i < selecteds.Count; i++)
        {
            var it = selecteds[i];
            var profile = profileMap.GetValueOrDefault(it.IndexId, it);

            if (it.ConfigType == EConfigType.Custom)
            {
                await AddCustomTestItem(actionType, it, profile, i, lstSelected);
                continue;
            }

            if (!it.ConfigType.IsComplexType() && it.Port <= 0)
            {
                continue;
            }

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

        if (lstSelected.Count > 1
            && (actionType == ESpeedActionType.Speedtest || actionType == ESpeedActionType.Mixedtest))
        {
            NoticeManager.Instance.Enqueue(ResUI.SpeedtestingPressEscToExit);
        }

        return lstSelected;
    }

    private async Task AddCustomTestItem(
        ESpeedActionType actionType,
        ProfileItem it,
        ProfileItem profile,
        int index,
        List<ServerTestItem> lstSelected)
    {
        switch (actionType)
        {
            case ESpeedActionType.Tcping:
            {
                var (address, port) = await TryGetCustomTcpingEndpoint(profile);
                if (address.IsNotEmpty() && port > 0)
                {
                    lstSelected.Add(new ServerTestItem()
                    {
                        IndexId = it.IndexId,
                        Address = address,
                        Port = port,
                        ConfigType = it.ConfigType,
                        QueueNum = index,
                        Profile = profile,
                        CoreType = AppManager.Instance.GetCoreType(profile, it.ConfigType),
                    });
                }
                else
                {
                    await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip, "");
                }
                break;
            }

            case ESpeedActionType.Realping:
            case ESpeedActionType.Speedtest:
            case ESpeedActionType.Mixedtest:
            {
                var configFile = profile.Address;
                if (!File.Exists(configFile))
                {
                    configFile = Utils.GetConfigPath(configFile);
                }
                if (!File.Exists(configFile))
                {
                    await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip, "");
                    break;
                }

                lstSelected.Add(new ServerTestItem()
                {
                    IndexId = it.IndexId,
                    Address = it.Address,
                    Port = 0,
                    ConfigType = it.ConfigType,
                    QueueNum = index,
                    Profile = profile,
                    CoreType = AppManager.Instance.GetCoreType(profile, it.ConfigType),
                });
                break;
            }

            case ESpeedActionType.UdpTest:
                await UpdateFunc(it.IndexId, ResUI.SpeedtestingSkip, "");
                break;
        }
    }

    private async Task RunTcpingAsync(List<ServerTestItem> selecteds, string exitLoopKey)
    {
        var pageSize = Math.Min(selecteds.Count, _speedTestPageSize);
        if (pageSize <= 0)
        {
            return;
        }
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
        if (lstSelected.Count == 0)
        {
            return;
        }
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
        ProcessService? processService = null;
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
                await processService.StopAsync();
            }
        }
        return true;
    }

    private async Task RunUdpTestBatchAsync(List<ServerTestItem> lstSelected, string exitLoopKey, int pageSize = 0)
    {
        if (lstSelected.Count == 0)
        {
            return;
        }
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
        ProcessService? processService = null;
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
                await processService.StopAsync();
            }
        }
        return true;
    }

    private async Task RunMixedTestAsync(List<ServerTestItem> selecteds, int concurrencyCount, bool blSpeedTest, string exitLoopKey)
    {
        if (selecteds.Count == 0)
        {
            return;
        }

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
                ProcessService? processService = null;
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
                        await processService.StopAsync();
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

        var lst1 = lstSelected.Where(t => t.CoreType == ECoreType.Xray
                                          && t.ConfigType != EConfigType.Custom).ToList();
        var lst2 = lstSelected.Where(t => t.CoreType == ECoreType.sing_box
                                          && t.ConfigType != EConfigType.Custom).ToList();

        var lstCustomTcping = lstSelected.Where(t => t.ConfigType == EConfigType.Custom).ToList();

        for (var num = 0; num < (int)Math.Ceiling(lst1.Count * 1.0 / pageSize); num++)
        {
            lstTest.Add(lst1.Skip(num * pageSize).Take(pageSize).ToList());
        }
        for (var num = 0; num < (int)Math.Ceiling(lst2.Count * 1.0 / pageSize); num++)
        {
            lstTest.Add(lst2.Skip(num * pageSize).Take(pageSize).ToList());
        }
        if (lstCustomTcping.Count > 0)
        {
            for (var num = 0; num < (int)Math.Ceiling(lstCustomTcping.Count * 1.0 / pageSize); num++)
            {
                lstTest.Add(lstCustomTcping.Skip(num * pageSize).Take(pageSize).ToList());
            }
        }

        return lstTest;
    }

    private async Task<(string Address, int Port)> TryGetCustomTcpingEndpoint(ProfileItem node)
    {
        try
        {
            var fileName = node.Address;
            if (!File.Exists(fileName))
            {
                fileName = Utils.GetConfigPath(fileName);
            }
            if (!File.Exists(fileName))
            {
                return (string.Empty, 0);
            }

            var content = await File.ReadAllTextAsync(fileName);
            if (content.IsNullOrWhiteSpace())
            {
                return (string.Empty, 0);
            }

            var json = JsonUtils.ParseJson(content);
            if (json != null)
            {
                return TryGetEndpointFromJson(json);
            }

            return TryGetEndpointFromYaml(content);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return (string.Empty, 0);
        }
    }

    private static (string Address, int Port) TryGetEndpointFromJson(JsonNode root)
    {
        if (root["outbounds"] is not JsonArray outbounds)
        {
            return (string.Empty, 0);
        }

        foreach (var outbound in outbounds)
        {
            if (TryReadJsonEndpoint(outbound, out var address, out var port))
            {
                return (address, port);
            }
        }

        return (string.Empty, 0);
    }

    private static bool TryReadJsonEndpoint(JsonNode? outbound, out string address, out int port)
    {
        address = string.Empty;
        port = 0;

        if (TryReadString(outbound, "server", out address)
            && (TryReadInt(outbound, "server_port", out port) || TryReadInt(outbound, "port", out port)))
        {
            return true;
        }

        if (TryReadString(outbound, "address", out address)
            && TryReadInt(outbound, "port", out port))
        {
            return true;
        }

        var settings = outbound?["settings"];
        if (settings != null)
        {
            if (settings["vnext"] is JsonArray vnext)
            {
                foreach (var server in vnext)
                {
                    if (TryReadString(server, "address", out address)
                        && TryReadInt(server, "port", out port))
                    {
                        return true;
                    }
                }
            }

            if (settings["servers"] is JsonArray servers)
            {
                foreach (var server in servers)
                {
                    if (TryReadString(server, "address", out address)
                        && TryReadInt(server, "port", out port))
                    {
                        return true;
                    }
                }
            }

            if (TryReadString(settings, "address", out address)
                && TryReadInt(settings, "port", out port))
            {
                return true;
            }
        }

        return false;
    }

    private static (string Address, int Port) TryGetEndpointFromYaml(string yaml)
    {
        try
        {
            var root = new DeserializerBuilder()
                .Build()
                .Deserialize<Dictionary<object, object>>(yaml);

            if (root == null
                || !TryGetMapValue(root, "proxies", out var proxiesObj)
                || proxiesObj is not IEnumerable<object> proxies)
            {
                return (string.Empty, 0);
            }

            foreach (var proxy in proxies.OfType<Dictionary<object, object>>())
            {
                if (TryGetMapValue(proxy, "server", out var serverObj)
                    && TryGetMapValue(proxy, "port", out var portObj)
                    && serverObj?.ToString().IsNotEmpty() == true
                    && int.TryParse(portObj?.ToString(), out var port)
                    && port > 0)
                {
                    return (serverObj.ToString()!, port);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return (string.Empty, 0);
    }

    private static bool TryGetMapValue(Dictionary<object, object> map, string key, out object? value)
    {
        foreach (var kv in map)
        {
            if (string.Equals(kv.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryReadString(JsonNode? node, string name, out string value)
    {
        value = node?[name]?.ToString()?.Trim() ?? string.Empty;
        return value.IsNotEmpty();
    }

    private static bool TryReadInt(JsonNode? node, string name, out int value)
    {
        value = 0;
        return int.TryParse(node?[name]?.ToString(), out value) && value > 0;
    }

    #endregion Custom config endpoint parsing

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
