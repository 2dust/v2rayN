namespace ServiceLib.Handler
{
    public class TaskHandler
    {
        private static readonly Lazy<TaskHandler> _instance = new(() => new());
        public static TaskHandler Instance => _instance.Value;

        public void RegUpdateTask(Config config, Action<bool, string> updateFunc)
        {
            Task.Run(() => UpdateTaskRunSubscription(config, updateFunc));
            Task.Run(() => UpdateTaskRunGeo(config, updateFunc));
            Task.Run(() => UpdateTaskRunCore(config, updateFunc));
            Task.Run(() => UpdateTaskRunGui(config, updateFunc));
        }

        private async Task UpdateTaskRunSubscription(Config config, Action<bool, string> updateFunc)
        {
            await Task.Delay(60000);
            Logging.SaveLog("UpdateTaskRunSubscription");

            var updateHandle = new UpdateService();
            while (true)
            {
                var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                var lstSubs = (await AppHandler.Instance.SubItems())
                            .Where(t => t.AutoUpdateInterval > 0)
                            .Where(t => updateTime - t.UpdateTime >= t.AutoUpdateInterval * 60)
                            .ToList();

                foreach (var item in lstSubs)
                {
                    await updateHandle.UpdateSubscriptionProcess(config, item.Id, true, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                            if (success)
                                Logging.SaveLog("subscription" + msg);
                        });
                    item.UpdateTime = updateTime;
                    await ConfigHandler.AddSubItem(config, item);

                    await Task.Delay(5000);
                }
                await Task.Delay(60000);
            }
        }

        private async Task UpdateTaskRunGeo(Config config, Action<bool, string> updateFunc)
        {
            var autoUpdateGeoTime = DateTime.Now;

            Logging.SaveLog("UpdateTaskRunGeo");

            var updateHandle = new UpdateService();
            while (true)
            {
                await Task.Delay(1000 * 3600);

                var dtNow = DateTime.Now;
                if (config.GuiItem.AutoUpdateInterval > 0)
                {
                    if ((dtNow - autoUpdateGeoTime).Hours % config.GuiItem.AutoUpdateInterval == 0)
                    {
                        await updateHandle.UpdateGeoFileAll(config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(false, msg);
                        });
                        autoUpdateGeoTime = dtNow;
                    }
                }
            }
        }

        private async Task UpdateTaskRunCore(Config config, Action<bool, string> updateFunc)
        {
            var autoUpdateCoreTime = DateTime.Now;

            Logging.SaveLog("UpdateTaskRunCore");

            var updateHandle = new UpdateService();
            while (true)
            {
                await Task.Delay(1000 * 3600);

                var dtNow = DateTime.Now;
                if (config.GuiItem.AutoUpdateCoreInterval > 0)
                {
                    if ((dtNow - autoUpdateCoreTime).Hours % config.GuiItem.AutoUpdateCoreInterval == 0)
                    {
                        await updateHandle.CheckUpdateCore(ECoreType.Xray, config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);

                        await updateHandle.CheckUpdateCore(ECoreType.sing_box, config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);

                        await updateHandle.CheckUpdateCore(ECoreType.mihomo, config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);

                        autoUpdateCoreTime = dtNow;
                    }
                }
            }
        }

        private async Task UpdateTaskRunGui(Config config, Action<bool, string> updateFunc)
        {
            var autoUpdateGuiTime = DateTime.Now;

            Logging.SaveLog("UpdateTaskRunGui");

            var updateHandle = new UpdateService();
            while (true)
            {
                await Task.Delay(1000 * 3600);

                var dtNow = DateTime.Now;
                if (config.GuiItem.AutoUpdateCoreInterval > 0)
                {
                    if ((dtNow - autoUpdateGuiTime).Hours % config.GuiItem.AutoUpdateCoreInterval == 0)
                    {
                        await updateHandle.CheckUpdateGuiN(config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);
                        autoUpdateGuiTime = dtNow;
                    }
                }
            }
        }
    }
}
