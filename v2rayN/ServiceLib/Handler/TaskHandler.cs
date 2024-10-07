namespace ServiceLib.Handler
{
    public class TaskHandler
    {
        private static readonly Lazy<TaskHandler> _instance = new(() => new());
        public static TaskHandler Instance => _instance.Value;

        public TaskHandler()
        {
        }

        public void RegUpdateTask(Config config, Action<bool, string> update)
        {
            Task.Run(() => UpdateTaskRunSubscription(config, update));
            Task.Run(() => UpdateTaskRunGeo(config, update));
        }

        private async Task UpdateTaskRunSubscription(Config config, Action<bool, string> update)
        {
            await Task.Delay(60000);
            Logging.SaveLog("UpdateTaskRunSubscription");

            var updateHandle = new UpdateService();
            while (true)
            {
                var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                var lstSubs = AppHandler.Instance.SubItems()
                            .Where(t => t.autoUpdateInterval > 0)
                            .Where(t => updateTime - t.updateTime >= t.autoUpdateInterval * 60)
                            .ToList();

                foreach (var item in lstSubs)
                {
                    updateHandle.UpdateSubscriptionProcess(config, item.id, true, (bool success, string msg) =>
                    {
                        update(success, msg);
                        if (success)
                            Logging.SaveLog("subscription" + msg);
                    });
                    item.updateTime = updateTime;
                    ConfigHandler.AddSubItem(config, item);

                    await Task.Delay(5000);
                }
                await Task.Delay(60000);
            }
        }

        private async Task UpdateTaskRunGeo(Config config, Action<bool, string> update)
        {
            var autoUpdateGeoTime = DateTime.Now;

            //await Task.Delay(1000 * 120);
            Logging.SaveLog("UpdateTaskRunGeo");

            var updateHandle = new UpdateService();
            while (true)
            {
                await Task.Delay(1000 * 3600);

                var dtNow = DateTime.Now;
                if (config.guiItem.autoUpdateInterval > 0)
                {
                    if ((dtNow - autoUpdateGeoTime).Hours % config.guiItem.autoUpdateInterval == 0)
                    {
                        await updateHandle.UpdateGeoFileAll(config, (bool success, string msg) =>
                        {
                            update(false, msg);
                        });
                        autoUpdateGeoTime = dtNow;
                    }
                }
            }
        }
    }
}