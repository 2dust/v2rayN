using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    public delegate void SetDefaultServerDelegate(string s);
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
                int iFailTimeMax = 30;
                int iTestInterval = iFailTimeMax / 3;
                DownloadHandle downloadHandle = new();
                long failtimestart = 0;
                List<ProfileItem> listprofile = new List<ProfileItem>();
                while (!bStop)
                {
                    Thread.Sleep(iTestInterval * 1000);
                    int res = downloadHandle.RunAvailabilityCheck(null).Result;
                    if (res <= 0)
                    {
                        _noticeHandler?.SendMessage("Current server test failed!", true);
                        if (failtimestart == 0)
                            failtimestart = GetTimestamp(DateTime.Now);
                        if (GetTimestamp(DateTime.Now) - failtimestart >= iFailTimeMax)
                        {
                            if (testResultItems.Count == 0 || listprofile.Count == 0)
                                listprofile = LazyConfig.Instance.ProfileItemsAutoSwitch();
                            if (listprofile.Count > 0)
                            {
                                var _config = LazyConfig.Instance.GetConfig();
                                if (testResultItems.Count == 0)
                                {
                                    var _coreHandler = new CoreHandler(_config, (bool x, string y) => { });
                                    new SpeedtestHandler(_config, _coreHandler, listprofile, Mode.ESpeedActionType.Tcping, UpdateSpeedtestHandler);
                                    while (testResultItems.Count < listprofile.Count)
                                    {
                                        Thread.Sleep(20);
                                    }
                                }

                                for(int i=testResultItems.Count-1; i>=0; i--) {
                                    if (testResultItems[i].latency <= 0 || testResultItems[i].indexId == _config.indexId)
                                        testResultItems.RemoveAt(i);
                                }

                                if (testResultItems.Count > 0)
                                {
                                    testResultItems.Sort((x, y) => x.latency.CompareTo(y.latency));
                                    setDefaultServerDelegates(testResultItems[0].indexId);
                                    //if (ConfigHandler.SetDefaultServerIndex(ref _config, testResultItems[0].indexId) == 0)
                                    //{
                                    //    RefreshServers();
                                    //    Reload();
                                    //}
       
                                    testResultItems.RemoveAt(0);

                                }
                            }
                        }
                    }
                    else
                    {
                        failtimestart = 0;
                        testResultItems.Clear();
                        listprofile.Clear();
                    }

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
