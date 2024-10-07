using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ServiceLib.ViewModels
{
    public class MsgViewModel : MyReactiveObject
    {
        private ConcurrentQueue<string> _queueMsg = new();
        private int _numMaxMsg = 500;
        private string _lastMsgFilter = string.Empty;
        private bool _lastMsgFilterNotAvailable;
        private bool _blLockShow = false;

        [Reactive]
        public string MsgFilter { get; set; }

        [Reactive]
        public bool AutoRefresh { get; set; }

        public MsgViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();

            MessageBus.Current.Listen<string>(Global.CommandSendMsgView).Subscribe(async x => await AppendQueueMsg(x));

            MsgFilter = _config.msgUIItem.mainMsgFilter ?? string.Empty;
            AutoRefresh = _config.msgUIItem.autoRefresh ?? true;

            this.WhenAnyValue(
               x => x.MsgFilter)
                   .Subscribe(c => _config.msgUIItem.mainMsgFilter = MsgFilter);

            this.WhenAnyValue(
              x => x.AutoRefresh,
              y => y == true)
                  .Subscribe(c => { _config.msgUIItem.autoRefresh = AutoRefresh; });
        }

        private async Task AppendQueueMsg(string msg)
        {
            //if (msg == Global.CommandClearMsg)
            //{
            //    ClearMsg();
            //    return;
            //}
            if (AutoRefresh == false)
            {
                return;
            }
            _ = EnqueueQueueMsg(msg);

            if (_blLockShow)
            {
                return;
            }

            _blLockShow = true;
            if (!_config.uiItem.showInTaskbar)
            {
                await Task.Delay(1000);
            }

            await Task.Delay(100);
            var txt = string.Join("", _queueMsg.ToArray());
            await _updateView?.Invoke(EViewAction.DispatcherShowMsg, txt);

            _blLockShow = false;
        }

        private async Task EnqueueQueueMsg(string msg)
        {
            //filter msg
            if (MsgFilter != _lastMsgFilter) _lastMsgFilterNotAvailable = false;
            if (Utils.IsNotEmpty(MsgFilter) && !_lastMsgFilterNotAvailable)
            {
                try
                {
                    if (!Regex.IsMatch(msg, MsgFilter))
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                    _lastMsgFilterNotAvailable = true;
                }
            }
            _lastMsgFilter = MsgFilter;

            //Enqueue
            if (_queueMsg.Count > _numMaxMsg)
            {
                for (int k = 0; k < _queueMsg.Count - _numMaxMsg; k++)
                {
                    _queueMsg.TryDequeue(out _);
                }
            }
            _queueMsg.Enqueue(msg);
            if (!msg.EndsWith(Environment.NewLine))
            {
                _queueMsg.Enqueue(Environment.NewLine);
            }
        }

        public void ClearMsg()
        {
            _queueMsg.Clear();
        }
    }
}