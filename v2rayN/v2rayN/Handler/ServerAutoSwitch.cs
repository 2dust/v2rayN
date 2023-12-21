using DynamicData;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using v2rayN.Mode;
using v2rayN.Resx;
using v2rayN.ViewModels;

namespace v2rayN.Handler
{
    public delegate void SetDefaultServerDelegate(string s);
    public delegate void SetTestResultDelegate(string i, string d, string s);
    public class TestResultItem
    {
        public string indexId { get; set; }
        public long latency { get; set; }
    }
    public class ServerAutoSwitch
    {
        private List<TestResultItem> testResultItems= new List<TestResultItem>();
        private static readonly object objLock = new();
        private bool bStop = false;
        private NoticeHandler? _noticeHandler;
        private Task taskmain = null;
        private SetDefaultServerDelegate setDefaultServerDelegates;
        private SetTestResultDelegate setTestResultDelegates;
        private SetAutoSwitchTogDelegate _setAutoSwitchTog;
        public ServerAutoSwitch()
        {
           
        }
        ~ServerAutoSwitch()
        {
            Stop();
        }
        public void Stop()
        {
            bStop = true;
            if(taskmain!=null)
               taskmain.Wait();
            taskmain = null;
        }
        public void SetDelegate(SetDefaultServerDelegate s = null) {
            this.setDefaultServerDelegates = s;
        }
        public void SetDelegate(SetTestResultDelegate s = null)
        {
            this.setTestResultDelegates = s;
        }
        public void SetDelegate(SetAutoSwitchTogDelegate s = null)
        {
            this._setAutoSwitchTog = s;
        }
        private void UpdateHandler(bool notify, string msg)
        {
        }
        private void UpdateSpeedtestHandler(string id, string dl, string speed)
        {
            lock (objLock)
            {
                int i;
                bool isNumeric = Int32.TryParse(dl, out i);
                if (!isNumeric)
                    return;

                //if (i <= 0 || id == LazyConfig.Instance.GetConfig().indexId)
                //    return;

                testResultItems.Add(new TestResultItem
                {
                    indexId = id,
                    latency = i
                });
                setTestResultDelegates(id, dl, speed);
            }
        }
      
        public void Start()
        {
            if (taskmain != null)
                return;

            var profiles = LazyConfig.Instance.ProfileItemsAutoSwitch();
            if (profiles.Count < 2)
                return;
            bStop = false;

            taskmain = Task.Run(() =>
            {
                _noticeHandler = Locator.Current.GetService<NoticeHandler>();
                DownloadHandle downloadHandle = new();
                long failtimestart = 0;
                int LastServerSelectMode = -1;
                List<ProfileItem> listprofile = new List<ProfileItem>();
                while (!bStop)
                {
                    var _config = LazyConfig.Instance.GetConfig();
                    int iFailTimeMax = LazyConfig.Instance.GetConfig().autoSwitchItem.FailTimeMax;
                    int iTestInterval = iFailTimeMax / 3;
                    int ServerSelectMode = LazyConfig.Instance.GetConfig().autoSwitchItem.ServerSelectMode;
                    int AutoSwitchMode = LazyConfig.Instance.GetConfig().autoSwitchItem.mode;

                    Thread.Sleep(iTestInterval * 1000);
                    int res = downloadHandle.RunAvailabilityCheck(null).Result;
                    if (!bStop)
                        Task.Run(() => setTestResultDelegates(_config.indexId, res.ToString(), ""));
                    else
                        break;
                    if (res <= 0)
                    {
                        _noticeHandler?.SendMessage("Current server test failed!",true);
                        if (failtimestart == 0)
                            failtimestart = GetTimestamp(DateTime.Now);
                        if (GetTimestamp(DateTime.Now) - failtimestart >= iFailTimeMax)
                        {
                            if (testResultItems.Count == 0 || listprofile.Count == 0 || ServerSelectMode!= LastServerSelectMode)
                            {
                                testResultItems.Clear();
                                listprofile = LazyConfig.Instance.ProfileItemsAutoSwitch();
                            }

                            if (listprofile.Count >= 2)
                            {

                                if (testResultItems.Count == 0)
                                {
                                    var _coreHandler = new CoreHandler(_config, (bool x, string y) => { });

                                    if (ServerSelectMode == 0 || ServerSelectMode == 1)
                                        new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Tcping, UpdateSpeedtestHandler);
                                    else if (ServerSelectMode == 2)
                                        new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Realping, UpdateSpeedtestHandler);
                                    if (bStop) break;
                                    while (!bStop && testResultItems.Count < listprofile.Count)
                                    {
                                        Thread.Sleep(20);
                                    }
                                    if (bStop) break;
                                    if (ServerSelectMode == 0)
                                    {
                                        List<TestResultItem> templist = new List<TestResultItem>();

                                        foreach (var item in listprofile)
                                        {
                                            var item2 = testResultItems.Find(x => x.indexId == item.indexId);
                                            if (item2 != null)
                                                templist.Add(item2);
                                        }
                                        testResultItems = templist;
                                    }
                                }
                                if (ServerSelectMode == 0)
                                {
                                    for (int i = testResultItems.Count - 1; i >= 0; i--)
                                    {
                                        if (testResultItems[i].latency <= 0 && testResultItems[i].indexId != _config.indexId)
                                            testResultItems.RemoveAt(i);
                                    }
                                    if (testResultItems.Count > 1)
                                    {
                                        int j = testResultItems.FindIndex(x => x.indexId == _config.indexId);
                                        int k = j + 1;
                                        if (k > testResultItems.Count - 1)
                                            k = 0;
                                        setDefaultServerDelegates(testResultItems[k].indexId);
                                        testResultItems.RemoveAt(j);
                                        if (testResultItems.Count <= 1)
                                            testResultItems.Clear();
                                    }
                                    else
                                        testResultItems.Clear();

                                }
                                else if (ServerSelectMode == 1 || ServerSelectMode == 2)
                                {
                                    for (int i = testResultItems.Count - 1; i >= 0; i--)
                                    {
                                        if (testResultItems[i].latency <= 0 || testResultItems[i].indexId == _config.indexId)
                                            testResultItems.RemoveAt(i);
                                    }

                                    if (testResultItems.Count > 0)
                                    {
                                        testResultItems.Sort((x, y) => x.latency.CompareTo(y.latency));
                                        setDefaultServerDelegates(testResultItems[0].indexId);
                                        testResultItems.RemoveAt(0);
                                    }
                                }

                            }
                            else
                            {
                                _setAutoSwitchTog(false);
                                _config.autoSwitchItem.EnableAutoSwitch = false;
                                MessageBox.Show(ResUI.stopSwtichWarn);                           
                            }
                                
                        }
                    }
                    else
                    {
                        failtimestart = 0;
                        testResultItems.Clear();
                        listprofile.Clear();
                    }
                    LastServerSelectMode = ServerSelectMode;
                }
            });
        }
        public static long GetTimestamp(DateTime dt)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            long timeStamp = (long)(dt- startTime).TotalSeconds;
            return timeStamp;
        }
    }
}
