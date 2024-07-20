using System.Collections.Concurrent;
using System.Resources;
using v2rayN;
using v2rayN.Handler;
using v2rayN.Models;

namespace v2rayMiniConsole
{
    public sealed class RunningObjects
    {
        private static readonly Lazy<RunningObjects> _instance = new(() => new());
        public static RunningObjects Instance => _instance.Value;
        public ConcurrentBag<ProfileItem> ProfileItems = new ConcurrentBag<ProfileItem>();

        //public ResourceManager rm = new ResourceManager("", typeof(Program).Assembly);

        private readonly object lockObj = new object();
        private bool _messageOn = false;
        private string _statusStr = string.Empty;

        #region message_management
        public bool IsMessageOn()
        {
            return _messageOn;
        }

        public void TurnOnMessage()
        {
            _messageOn = true;
            Console.WriteLine("\nmessage turned on");
        }
        public void TurnOffMessage()
        {
            _messageOn = false;
            Console.WriteLine("\nmessage turned off");
        }
        public void ToggleMessage()
        {
            _messageOn = !_messageOn;
            string status = _messageOn ? "on" : "off";
            Console.WriteLine($"message turned {status}");
        }
        #endregion message_management

        #region status_management
        public void SetStatus()
        {
            var config = LazyConfig.Instance.GetConfig();
            var currentServer = LazyConfig.Instance.GetProfileItem(config.indexId);
            var currentRouting = LazyConfig.Instance.GetRoutingItem(config.routingBasicItem.routingIndexId);
            lock (lockObj) 
            {
                _statusStr = Utils.GetVersion(false) + " / " + string.Format(Global.StatusTemplate,
                currentServer?.address + ":" + currentServer?.port + "-> " + MainTask.Instance.CurrentServerStats?.delay + "ms",
                config.sysProxyType.ToString(), currentRouting.remarks);
                Console.Title = _statusStr;
            }
            
        }

        public void SetStatus(string testItem,int totalTaskCount, int completedTaskCount)
        {
            string status = $"{_statusStr} | {testItem}: {completedTaskCount}/{totalTaskCount}";
            lock (lockObj)
            {
                Console.Title = status;
            }
            
        }

        public void SetStatus(string message)
        {
            string status = Utils.GetVersion(false) + " / " + message;
            lock (lockObj)
            {
                Console.Title = status;
            };
        }

        #endregion status_management
    }
}
