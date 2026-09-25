namespace ServiceLib.Manager;

public class TaskManager
{
    private static readonly Lazy<TaskManager> _instance = new(() => new());
    public static TaskManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;
    private long _lastAutoDelayTestTime;

    public void RegUpdateTask(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        _ = Task.Factory.StartNew(
            ScheduledTasks,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private async Task ScheduledTasks()
    {
        Logging.SaveLog("Setup Scheduled Tasks");

        var numOfExecuted = 1;
        _lastAutoDelayTestTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        while (true)
        {
            //1 minute
            await Task.Delay(1000 * 60);

            //Execute once 1 minute
            try
            {
                await UpdateTaskRunSubscription();
            }
            catch (Exception ex)
            {
                Logging.SaveLog("ScheduledTasks - UpdateTaskRunSubscription", ex);
            }

            //Execute auto delay test by interval
            try
            {
                await UpdateTaskRunAutoDelayTest();
            }
            catch (Exception ex)
            {
                Logging.SaveLog("ScheduledTasks - UpdateTaskRunAutoDelayTest", ex);
            }

            //Execute once 20 minute
            if (numOfExecuted % 20 == 0)
            {
                //Logging.SaveLog("Execute save config");

                try
                {
                    await ConfigHandler.SaveConfig(_config);
                    await ProfileExManager.Instance.SaveTo();
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - SaveConfig", ex);
                }
            }

            //Execute once 1 hour
            if (numOfExecuted % 60 == 0)
            {
                //Logging.SaveLog("Execute delete expired files");

                FileUtils.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1), "Test");
                FileUtils.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddDays(-7));
                FileUtils.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddDays(-7));

                try
                {
                    await UpdateTaskRunGeo(numOfExecuted / 60);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - UpdateTaskRunGeo", ex);
                }
            }

            //Execute once 24 hour
            if (numOfExecuted % 1440 == 1)
            {
                try
                {
                    await UpdateTaskRunCheckUpdate();
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - UpdateTaskRunCheckUpdate", ex);
                }
            }
            numOfExecuted++;
        }
    }

    private async Task UpdateTaskRunSubscription()
    {
        var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        var lstSubs = (await AppManager.Instance.SubItems())?
            .Where(t => t.AutoUpdateInterval > 0)
            .Where(t => updateTime - t.UpdateTime >= t.AutoUpdateInterval * 60)
            .ToList();

        if (lstSubs is not { Count: > 0 })
        {
            return;
        }

        Logging.SaveLog("Execute update subscription");

        foreach (var item in lstSubs)
        {
            await SubscriptionHandler.UpdateProcess(_config, item.Id, true, async (success, msg) =>
            {
                await _updateFunc?.Invoke(success, msg);
                if (success)
                {
                    Logging.SaveLog($"Update subscription end. {msg}");
                }
            });
            item.UpdateTime = updateTime;
            await ConfigHandler.AddSubItem(_config, item);
            await Task.Delay(1000);
        }
    }

    private async Task UpdateTaskRunAutoDelayTest()
    {
        var interval = _config.SpeedTestItem.AutoDelayTestInterval;
        if (!_config.SpeedTestItem.AutoDelayTestEnabled || interval <= 0)
        {
            return;
        }

        var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        if (updateTime - _lastAutoDelayTestTime < interval * 60)
        {
            return;
        }
        _lastAutoDelayTestTime = updateTime;

        Logging.SaveLog("Execute auto delay test");

        await AutoDelayTestManager.Instance.RunOnce();
    }

    private async Task UpdateTaskRunGeo(int hours)
    {
        if (_config.GuiItem.AutoUpdateInterval > 0 && hours > 0 && hours % _config.GuiItem.AutoUpdateInterval == 0)
        {
            Logging.SaveLog("Execute update geo files");

            await new UpdateService(_config, async (success, msg) =>
            {
                await _updateFunc?.Invoke(false, msg);
            }).UpdateGeoFileAll();
        }
    }

    private async Task UpdateTaskRunCheckUpdate()
    {
        Logging.SaveLog("Execute check update");

        var updateService = new UpdateService(_config, async (success, msg) => await Task.CompletedTask);

        var msgs = await updateService.CheckHasUpdateOnlyAll(_config.CheckUpdateItem.CheckPreReleaseUpdate, _config.CheckUpdateItem.UpdateViaProxy);
        foreach (var msg in msgs)
        {
            await _updateFunc?.Invoke(false, msg);
        }
        NoticeManager.Instance.Enqueue(string.Join("\n", msgs));

        if (msgs.Count > 0)
        {
            AppEvents.HasUpdateNotified.Publish(true);
        }
    }
}
