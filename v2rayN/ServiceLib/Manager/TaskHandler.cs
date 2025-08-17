namespace ServiceLib.Manager;

public class TaskHandler
{
    private static readonly Lazy<TaskHandler> _instance = new(() => new());
    public static TaskHandler Instance => _instance.Value;

    public void RegUpdateTask(Config config, Action<bool, string> updateFunc)
    {
        Task.Run(() => ScheduledTasks(config, updateFunc));
    }

    private async Task ScheduledTasks(Config config, Action<bool, string> updateFunc)
    {
        Logging.SaveLog("Setup Scheduled Tasks");

        var numOfExecuted = 1;
        while (true)
        {
            //1 minute
            await Task.Delay(1000 * 60);

            //Execute once 1 minute
            await UpdateTaskRunSubscription(config, updateFunc);

            //Execute once 20 minute
            if (numOfExecuted % 20 == 0)
            {
                //Logging.SaveLog("Execute save config");

                await ConfigHandler.SaveConfig(config);
                await ProfileExHandler.Instance.SaveTo();
            }

            //Execute once 1 hour
            if (numOfExecuted % 60 == 0)
            {
                //Logging.SaveLog("Execute delete expired files");

                FileManager.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1));
                FileManager.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddMonths(-1));
                FileManager.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddMonths(-1));

                //Check once 1 hour
                await UpdateTaskRunGeo(config, numOfExecuted / 60, updateFunc);
            }

            numOfExecuted++;
        }
    }

    private async Task UpdateTaskRunSubscription(Config config, Action<bool, string> updateFunc)
    {
        var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        var lstSubs = (await AppHandler.Instance.SubItems())?
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
            await SubscriptionHandler.UpdateProcess(config, item.Id, true, (success, msg) =>
            {
                updateFunc?.Invoke(success, msg);
                if (success)
                {
                    Logging.SaveLog($"Update subscription end. {msg}");
                }
            });
            item.UpdateTime = updateTime;
            await ConfigHandler.AddSubItem(config, item);
            await Task.Delay(1000);
        }
    }

    private async Task UpdateTaskRunGeo(Config config, int hours, Action<bool, string> updateFunc)
    {
        if (config.GuiItem.AutoUpdateInterval > 0 && hours > 0 && hours % config.GuiItem.AutoUpdateInterval == 0)
        {
            Logging.SaveLog("Execute update geo files");

            var updateHandle = new UpdateService();
            await updateHandle.UpdateGeoFileAll(config, (success, msg) =>
            {
                updateFunc?.Invoke(false, msg);
            });
        }
    }
}
