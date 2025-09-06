namespace ServiceLib.Manager;

public class TaskManager
{
    private static readonly Lazy<TaskManager> _instance = new(() => new());
    public static TaskManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;

    public void RegUpdateTask(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        Task.Run(ScheduledTasks);
    }

    private async Task ScheduledTasks()
    {
        Logging.SaveLog("Setup Scheduled Tasks");

        var numOfExecuted = 1;
        while (true)
        {
            //1 minute
            await Task.Delay(1000 * 60);

            //Execute once 1 minute
            await UpdateTaskRunSubscription();

            //Execute once 20 minute
            if (numOfExecuted % 20 == 0)
            {
                //Logging.SaveLog("Execute save config");

                await ConfigHandler.SaveConfig(_config);
                await ProfileExManager.Instance.SaveTo();
                await ProfileGroupItemManager.Instance.SaveTo();
            }

            //Execute once 1 hour
            if (numOfExecuted % 60 == 0)
            {
                //Logging.SaveLog("Execute delete expired files");

                FileManager.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1));
                FileManager.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddMonths(-1));
                FileManager.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddMonths(-1));

                //Check once 1 hour
                await UpdateTaskRunGeo(numOfExecuted / 60);
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

    private async Task UpdateTaskRunGeo(int hours)
    {
        if (_config.GuiItem.AutoUpdateInterval > 0 && hours > 0 && hours % _config.GuiItem.AutoUpdateInterval == 0)
        {
            Logging.SaveLog("Execute update geo files");

            var updateHandle = new UpdateService();
            await updateHandle.UpdateGeoFileAll(_config, async (success, msg) =>
            {
                await _updateFunc?.Invoke(false, msg);
            });
        }
    }
}
