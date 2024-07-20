using v2rayMiniConsole.Resx;
using v2rayN;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

namespace v2rayMiniConsole
{
    internal class MainTask
    {
        private static readonly Lazy<MainTask> _instance = new(() => new());
        public static MainTask Instance => _instance.Value;

        // 用来通知后台线程退出
        private CancellationTokenSource _cts { get; } = new CancellationTokenSource();
        #region private prop

        private CoreHandler? _coreHandler;
        private NoticeHandler _noticeHandler = new NoticeHandler();
        
        private string _subId = string.Empty;
        private string _serverFilter = string.Empty;
        private static Config? _config;

        #endregion private prop

        #region public system proxy

        public bool BlSystemProxyClear { get; set; }
        public bool BlSystemProxySet { get; set; }
        public bool BlSystemProxyNothing { get; set; }
        public bool BlSystemProxyPac { get; set; }
        public int SystemProxySelected { get; set; }

        #endregion public system proxy

        public string? ServerFilter { get; set; }

        public bool BlServers { get; set; }

        public ProfileExItem? CurrentServerStats;



        public bool BlReloadEnabled { get; set; }
        public bool ShowCalshUI { get; set; }

        private MainTask() => Init();

        private void Init()
        {
            Logging.Setup();

            if (ConfigHandler.LoadConfig(ref _config) != 0)
            {
                Console.Write($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
                Environment.Exit(0);
                return;
            }

            Logging.LoggingEnabled(_config.guiItem.enableLog);
            Logging.SaveLog($"v2rayMiniConsole start up | {Utils.GetVersion()} | {Utils.GetExePath()}");
            Logging.ClearLogs();

            Thread.CurrentThread.CurrentUICulture = new(_config.uiItem.currentLanguage);

            ConfigHandler.InitBuiltinRouting(_config);
            ConfigHandler.InitBuiltinDNS(_config);
            _coreHandler = new CoreHandler(_config, UpdateHandler);
            //Under Win10
            if (Environment.OSVersion.Version.Major < 10)
            {
                Environment.SetEnvironmentVariable("DOTNET_EnableWriteXorExecute", "0", EnvironmentVariableTarget.User);
            }            

            var item = ConfigHandler.GetDefaultServer(_config);
            if (item == null || item.configType == EConfigType.Custom)
            {
                MainFormHandler.Instance.UpdateTask(_config, UpdateTaskHandler);
                return;
            }
            if (ConfigHandler.SetDefaultServerIndex(_config, item.indexId) == 0)
            {
                Reload();
            }
            CurrentServerStats = ProfileExHandler.Instance.ProfileExs.Where(t => t.indexId == item.indexId).FirstOrDefault();
            if (CurrentServerStats == null)
            {
                CurrentServerStats = new ProfileExItem();
                CurrentServerStats.indexId = item.indexId;
            }

            MainFormHandler.Instance.UpdateTask(_config, UpdateTaskHandler);
        }

        private void UpdateHandler(bool notify, string msg)
        {
            _noticeHandler?.SendMessage(msg);
        }

        private void UpdateTaskHandler(bool success, string msg)
        {
            _noticeHandler.SendMessage(msg, true);              
        }

        #region proxy_management
        public void SetListenerType(ESysProxyType type)
        {
            if (_config.sysProxyType == type)
            {
                return;
            }
            _config.sysProxyType = type;
            ChangeSystemProxyStatus(type, true);

            SystemProxySelected = (int)_config.sysProxyType;
            ConfigHandler.SaveConfig(_config, false);
        }

        public void ChangeSystemProxyMode()
        {
            List<string> proxyModes = new List<string>(){ 
                ResUI.menuSystemProxyClear,
                ResUI.menuSystemProxySet,
                ResUI.menuSystemProxyNothing,
                ResUI.menuSystemProxyPac
            };
            List<ESysProxyType> eSysProxyTypes = new List<ESysProxyType>() {
                ESysProxyType.ForcedClear,
                ESysProxyType.ForcedChange,
                ESysProxyType.Unchanged,
                ESysProxyType.Pac
            };
            UserPromptUI(proxyModes, choice => 
            {
                SetListenerType(eSysProxyTypes[choice - 1]);
            });
        }

        public void ChangeSystemProxyStatus(ESysProxyType type, bool blChange)
        {
            SysProxyHandle.UpdateSysProxy(_config, _config.tunModeItem.enableTun ? true : false);
            _noticeHandler.SendMessage($"{ResUI.TipChangeSystemProxy} - {_config.sysProxyType.ToString()}", true);
            RunningObjects.Instance.SetStatus();
            BlSystemProxyClear = (type == ESysProxyType.ForcedClear);
            BlSystemProxySet = (type == ESysProxyType.ForcedChange);
            BlSystemProxyNothing = (type == ESysProxyType.Unchanged);
            BlSystemProxyPac = (type == ESysProxyType.Pac);
        }

        #endregion proxy_management

        #region routing_management

        public void Show_Current_Routing()
        {
            Console.WriteLine();
            var currentRoutingItem = LazyConfig.Instance.GetRoutingItem(_config.routingBasicItem.routingIndexId);
            Console.WriteLine(currentRoutingItem.remarks);
        }

        public void Change_Routing()
        {
            var routings = LazyConfig.Instance.RoutingItems();
            var routingStr = routings.Select(t => t.remarks).ToList();
            UserPromptUI(routingStr, choice => 
            {
                RoutingItemChanged(routings[choice - 1]);
            });
        }

        private void RoutingItemChanged(RoutingItem item)
        {
            if (_config.routingBasicItem.routingIndexId == item.id)
            {
                return;
            }

            if (ConfigHandler.SetDefaultRouting(_config, item) == 0)
            {
                _noticeHandler?.SendMessage(ResUI.TipChangeRouting, true);
                RunningObjects.Instance.SetStatus();
                Reload();
            }
        }

        #endregion routing_management

        public void ServerSpeedtest()
        {
            if (RunningObjects.Instance.ProfileItems.Count == 0)
            {
                return;
            }
            RunningObjects.Instance.ProfileItems = new System.Collections.Concurrent.ConcurrentBag<ProfileItem>(
                RemoveDuplicateServer(RunningObjects.Instance.ProfileItems.ToList()));
            new SpeedtestHandler(_config, _coreHandler, RunningObjects.Instance.ProfileItems.ToList(), ESpeedActionType.Mixedtest, UpdateSpeedtestHandler);
        }

        private void UpdateSpeedtestHandler(string indexId, string delay, string speed)
        {
            Task.Run((Action)(() =>
            {
                SetTestResult(indexId, delay, speed);
            }));
        }

        private void SetTestResult(string indexId, string delay, string speed)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                _noticeHandler.SendMessage(delay, true);
                if (delay == ResUI.SpeedtestingCompleted)
                {
                    // 测速结束，将可用服务器持久化。若有更快的服务器，换过去
                    SaveAvailableProfileItemsAndUpdateCurrentServer();
                    // 拿一键测速后的结果更新数据库现有服务器失败计数器
                    UpdateServerFailureAfterMultiSpeedTest();
                }
                return;
            }
            var item = ProfileExHandler.Instance.ProfileExs.Where(it => it.indexId == indexId).FirstOrDefault();
            if (item == null)
            {
                _noticeHandler.SendMessage($"profileEx didn't find {indexId}", true);
                return;
            }
            if (!Utils.IsNullOrEmpty(delay))
            {
                int.TryParse(delay, out int temp);
                item.delay = temp;
                _noticeHandler.SendMessage($"delay:{indexId}-{delay} {Global.DelayUnit}", true);
            }
            if (!Utils.IsNullOrEmpty(speed))
            {
                _noticeHandler.SendMessage($"speed:{indexId}-{speed} {Global.SpeedUnit}", true);
            }
        }

        public void UpdateProfileItemFailure(bool serverFailure, string indexId)
        {
            var profileItem = LazyConfig.Instance.GetProfileItem(indexId);
            if (profileItem == null)  // 当订阅更新时，有新服务器就会触发这个，不用打印了
            {
                //_noticeHandler?.SendMessage("profileItem is null while profileExItem not in SetTestResult", true);
                return;
            }
            if (serverFailure) // 若测速或测延时失败，当failures >= 10时删除此服务器记录，否则failures自增
            {
                if (++profileItem.failures >= 10)
                {
                    SQLiteHelper.Instance.Delete(profileItem);
                    _noticeHandler?.SendMessage($"{indexId} failed for more than 10 times, dropping record", true);
                }
                else
                {
                    SQLiteHelper.Instance.Update(profileItem);
                    _noticeHandler?.SendMessage($"{indexId} failed for {profileItem.failures} times", true);
                }
            }
            else  // 若测速或测延时成功，当failures == 0时不做操作，否则failures自减
            {
                if (profileItem.failures > 0)
                {
                    --profileItem.failures;
                    SQLiteHelper.Instance.Update(profileItem);
                    _noticeHandler?.SendMessage($"{indexId} succeeded, failures = {profileItem.failures}", true);
                }
            }
        }

        public void SwitchServer()
        {
            var profileItems = LazyConfig.Instance.ProfileItems();
            if (profileItems.Count <= 1)
            {
                RunningObjects.Instance.SetStatus("no more available server to use.");
                return;
            }
            var currentIndex = profileItems.FindIndex(t => t.indexId == MainTask.Instance.CurrentServerStats.indexId);
            var profile = profileItems[(++currentIndex) % profileItems.Count];
            MainTask.Instance.SetDefaultServer(profile.indexId);
        }

        private void SaveAvailableProfileItemsAndUpdateCurrentServer()
        {
            var availableProfileExItems = ProfileExHandler.Instance.ProfileExs.Where(item=> item.speed > 0).ToList();
            
            if (availableProfileExItems.Count == 0)
            {
                _noticeHandler.SendMessage("no available server found", true);
                return;
            }

            // 新的可用服务器入库
            var availableProfileItems = RunningObjects.Instance.ProfileItems.Where(item => availableProfileExItems.Select(exItem => exItem.indexId).Contains(item.indexId)).ToList();
            //availableProfileItems = RemoveDuplicateServer(availableProfileItems);git
            SQLiteHelper.Instance.InsertAll(availableProfileItems);
            
            _noticeHandler.SendMessage("saved availableProfileItems", true);

            // 切换最快服务器
            var FastestExItem = ProfileExHandler.Instance.ProfileExs.OrderByDescending(t => t.speed).FirstOrDefault();
            if (CurrentServerStats == null && FastestExItem == null
                || CurrentServerStats != null && FastestExItem != null && FastestExItem.speed <= CurrentServerStats.speed)
            {
                return;
            }            
            SetDefaultServer(FastestExItem?.indexId);
            CurrentServerStats = FastestExItem;
            var currentProfileItem = LazyConfig.Instance.GetProfileItem(FastestExItem.indexId);
            _noticeHandler.SendMessage($"server changed to { currentProfileItem?.address }-{ currentProfileItem?.remarks }", true);
        }

        private void UpdateServerFailureAfterMultiSpeedTest()
        {
            var profileItems = LazyConfig.Instance.ProfileItems();
            foreach (var exItem in ProfileExHandler.Instance.ProfileExs)
            {
                if (profileItems.Exists(t => t.indexId == exItem.indexId))
                {
                    if (exItem.speed > 0)
                    {
                        UpdateProfileItemFailure(false, exItem.indexId);
                    }
                    else
                    {
                        UpdateProfileItemFailure(true, exItem.indexId);
                    }
                }
            }
        }

        private List<ProfileItem> RemoveDuplicateServer(List<ProfileItem> profiles)
        {
            var ret = ConfigHandler.DedupServerList(profiles);
            Reload();
            return ret;
        }


        public void TestServerAvailability()
        {
            var item = ConfigHandler.GetDefaultServer(_config);
            if (item == null || item.configType == EConfigType.Custom)
            {
                return;
            }
            (new UpdateHandle()).RunAvailabilityCheck((bool success, string msg) =>
            {
                _noticeHandler?.SendMessage(msg, true);
            });
        }

        public void SetDefaultServer(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return;
            }
            if (indexId == _config.indexId)
            {
                return;
            }
            var item = LazyConfig.Instance.GetProfileItem(indexId);
            if (item is null)
            {
                _noticeHandler.SendMessage(ResUI.PleaseSelectServer, true);
                return;
            }

            if (ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
            {
                Reload();
            }
            CurrentServerStats = ProfileExHandler.Instance.ProfileExs.Where(t => t.indexId == indexId).FirstOrDefault();
            if (CurrentServerStats == null)
            {
                CurrentServerStats = new ProfileExItem();
                CurrentServerStats.indexId = indexId;
            }
        }

        #region subscription management
        public void ShowCurrentSubscriptions()
        {
            int i = 0;
            foreach(var subItem in LazyConfig.Instance.SubItems())
            {
                Console.WriteLine($"{i++}->{subItem.remarks}: {subItem.url}, update interval = " +
                    $"{subItem.autoUpdateInterval}, isEnabled = {subItem.enabled}");
            }
        }

        public void AddSubscription()
        {
            SubItem subItem = new SubItem();

            subItem.remarks = GetUserInput("remarks:", input => !string.IsNullOrEmpty(input.Trim()));
            subItem.url = GetUserInput("url:", input => Utils.IsValidUrl(input.Trim().ToLower()), "g");

            int autoUpdateInterval;
            try
            {
                autoUpdateInterval = GetUserIntInput("update interval:", input => int.TryParse(input.Trim(), out autoUpdateInterval) && autoUpdateInterval > 0, "g");
                subItem.autoUpdateInterval = autoUpdateInterval;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Gave up on adding subscription, returning to main task.");
                return;
            }

            subItem.id = Utils.GetGUID(false);

            try
            {
                SQLiteHelper.Instance.Insert(subItem);
                Console.WriteLine();
                Console.WriteLine("Subscription added.");
                RunningObjects.Instance.SetStatus();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while adding the subscription: " + ex.Message);
            }
        }

        private string GetUserInput(string prompt, Func<string, bool> validator = null, string quitCommand = null)
        {
            string input;
            do
            {
                Console.Write(prompt);
                input = Console.ReadLine()?.Trim();

                if (quitCommand != null && input?.ToLower() == quitCommand)
                {
                    throw new OperationCanceledException("User canceled operation.");
                }

                if (validator != null && !validator(input))
                {
                    Console.WriteLine("Invalid input. Please try again.");
                }
            } while (validator != null && !validator(input));

            return input;
        }

        private int GetUserIntInput(string prompt, Func<string, bool> validator, string quitCommand)
        {
            int value;
            string input;
            do
            {
                input = GetUserInput(prompt, validator, quitCommand);
                if (!int.TryParse(input, out value))
                {
                    Console.WriteLine("Invalid input. Please enter a valid integer.");
                }
            } while (!int.TryParse(input, out value));

            return value;
        }

        public void RemoveSubscription()
        {
            var subItems = LazyConfig.Instance.SubItems();
            var subItemsStr = LazyConfig.Instance.SubItems().Select(t => t.remarks).ToList();
            UserPromptUI(subItemsStr, choice =>
            {
                SQLiteHelper.Instance.Delete(subItems[choice - 1]);
            });
        }
        
        #endregion subscription management

        public void Reload()
        {
            BlReloadEnabled = false;

            LoadCore().ContinueWith(task =>
            {
                TestServerAvailability();
                BlReloadEnabled = true;
                ShowCalshUI = (_config.runningCoreType is ECoreType.clash or ECoreType.clash_meta or ECoreType.mihomo);
                if (ShowCalshUI)
                {
                    _noticeHandler?.SendMessage("ShowClashUI triggered, how to deal with that?", true);
                }
            });
        }

        #region core_job

        private async Task LoadCore()
        {
            await Task.Run(() =>
            {
                _coreHandler.LoadCore();

                //ConfigHandler.SaveConfig(_config, false);

                ChangeSystemProxyStatus(_config.sysProxyType, false);
            });
        }

        private void CloseCore()
        {
            ConfigHandler.SaveConfig(_config, false);

            ChangeSystemProxyStatus(ESysProxyType.ForcedClear, false);

            _coreHandler.CoreStop();
        }

        #endregion core job

        public bool IsCancellationRequested()
        {
            return _cts.IsCancellationRequested;
        }

        public CancellationToken GetCancellationToken()
        {
            return _cts.Token;
        }
        
        public void BroadcastExit(bool blWindowsShutDown)
        {
            _cts.Cancel();
            // 等待所有任务优雅地结束，但注意这里可能会阻塞，特别是如果有任务永远不会完成
            // 或者使用Task.WaitAll(_mainFormTasks.ToArray()); 但不推荐，因为它会抛出AggregateException
            try
            {
                // 等待所有任务完成，或等待指定的超时时间
                Task.WhenAll(MainFormHandler.Instance.MainFormTasks).Wait();
            }
            catch (AggregateException ae)
            {
                // 处理取消操作等导致的异常
                ae.Handle(e => e is TaskCanceledException);
            }
            
            OnExit(blWindowsShutDown);
            Environment.Exit(0);
        }
        public void OnExit(bool blWindowsShutDown)
        {
            Logging.SaveLog("MyAppExit Begin");

            ConfigHandler.SaveConfig(_config);

            if (blWindowsShutDown)
            {
                SysProxyHandle.ResetIEProxy4WindowsShutDown(); //受到用户注销或关机做的操作 
            }
            else
            {
                SysProxyHandle.UpdateSysProxy(_config, true);
            }

            _coreHandler.CoreStop();
            Logging.SaveLog("MyAppExit End");
        }

        public void SetLanguage()
        {
            UserPromptUI(Global.Languages, choice =>
            {
                var selectedLanguage = Global.Languages[choice - 1];
                if (selectedLanguage == _config.uiItem.currentLanguage)
                {
                    return;
                }
                _config.uiItem.currentLanguage = selectedLanguage;
                ConfigHandler.SaveConfig(_config, false);
                Thread.CurrentThread.CurrentUICulture = new(_config.uiItem.currentLanguage);
            });
        }

        private void UserPromptUI(List<string> options, Action<int> action)
        {
            if (options.Count == 0)
            {
                Console.WriteLine("no options to choose");
                return;
            }
            int i = 1;
            for (; i <= options.Count; ++i)
            {
                Console.WriteLine($"{i}.{options[i - 1]}");
            }
            Console.WriteLine($"{i}.Quit");
            Console.WriteLine();
            Console.Write("Please select:");
            while (true)
            {
                string input = Console.ReadLine();

                if (int.TryParse(input, out int choice) && choice >= 1 && choice <= options.Count + 1)
                {
                    if (choice == options.Count + 1)
                    {
                        return;
                    }
                    action(choice);
                    break;
                }
                else
                {
                    Console.WriteLine("error input, try again");
                }
            }
        }
    }
}
