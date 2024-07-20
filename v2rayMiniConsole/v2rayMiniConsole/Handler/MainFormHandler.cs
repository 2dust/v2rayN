using v2rayN.Models;
using v2rayMiniConsole;
using System.Collections.Concurrent;

namespace v2rayN.Handler
{
    public sealed class MainFormHandler
    {
        private static readonly Lazy<MainFormHandler> instance = new(() => new());
        public static MainFormHandler Instance => instance.Value;

        // 使用ConcurrentBag来存储任务，因为它提供了线程安全的集合
        public readonly ConcurrentBag<Task> MainFormTasks = new ConcurrentBag<Task>();


        public void UpdateTask(Config config, Action<bool, string> update)
        {
            MainFormTasks.Add(Task.Run(() => TimedTask()));
            MainFormTasks.Add(Task.Run(() => UpdateTaskRunSubscription(config, update)));
            MainFormTasks.Add(Task.Run(() => UpdateTaskRunGeo(config, update)));
        }

        private async Task TimedTask()
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(MainTask.Instance.GetCancellationToken()))
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(30000, cts.Token); // 传入CancellationToken以便在取消时中断Delay
                    MainTask.Instance.TestServerAvailability();
                }
            }
        }
        
        private async Task UpdateTaskRunSubscription(Config config, Action<bool, string> update)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(MainTask.Instance.GetCancellationToken()))
            {
                Logging.SaveLog("UpdateTaskRunSubscription");

                var updateHandle = new UpdateHandle();
            
                while (!cts.IsCancellationRequested)
                {
                    var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                    var lstSubs = LazyConfig.Instance.SubItems();
                    if (!lstSubs.Any())
                    {
                        RunningObjects.Instance.SetStatus("Subscription list is empty, use \"add_subscription\" to add at least one.");
                    }
                    else
                    {
                        await Task.Delay(5000, cts.Token);
                    }
                    lstSubs = lstSubs.Where(t => t.autoUpdateInterval > 0)
                                .Where(t => updateTime - t.updateTime >= t.autoUpdateInterval * 60)
                                .ToList();
                    if (lstSubs.Any())
                    {
                        RunningObjects.Instance.ProfileItems?.Clear();
                    }
                    foreach (var item in lstSubs)
                    {
                        updateHandle.UpdateSubscriptionProcess(config, item.id, true, (bool success, string msg) =>
                        {
                            update(success, msg);
                            if (success)
                            {
                                Logging.SaveLog("subscription" + msg);
                            }
                        });
                        item.updateTime = updateTime;
                        ConfigHandler.AddSubItem(config, item);

                        await Task.Delay(5000, cts.Token);
                    }
                    await Task.Delay(60000, cts.Token);
                }
            }                
        }

        private async Task UpdateTaskRunGeo(Config config, Action<bool, string> update)
        {
            var autoUpdateGeoTime = DateTime.Now;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(MainTask.Instance.GetCancellationToken()))
            {
                await Task.Delay(1000 * 120, cts.Token);
                Logging.SaveLog("UpdateTaskRunGeo");

                var updateHandle = new UpdateHandle();
                while (!cts.IsCancellationRequested)
                {
                    var dtNow = DateTime.Now;
                    if (config.guiItem.autoUpdateInterval > 0)
                    {
                        if ((dtNow - autoUpdateGeoTime).Hours % config.guiItem.autoUpdateInterval == 0)
                        {
                            updateHandle.UpdateGeoFileAll(config, (bool success, string msg) =>
                            {
                                update(false, msg);
                            });
                            autoUpdateGeoTime = dtNow;
                        }
                    }

                    await Task.Delay(1000 * 3600, cts.Token);
                }
            }
                
        }
    }
}