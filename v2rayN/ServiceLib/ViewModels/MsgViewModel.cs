using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class MsgViewModel : MyReactiveObject
{
    private ConcurrentQueue<string> _queueMsg = new();
    private int _numMaxMsg = 500;
    private bool _lastMsgFilterNotAvailable;
    private bool _blLockShow = false;

    [Reactive]
    public string MsgFilter { get; set; }

    [Reactive]
    public bool AutoRefresh { get; set; }

    public MsgViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;
        MsgFilter = _config.MsgUIItem.MainMsgFilter ?? string.Empty;
        AutoRefresh = _config.MsgUIItem.AutoRefresh ?? true;

        this.WhenAnyValue(
           x => x.MsgFilter)
               .Subscribe(c => DoMsgFilter());

        this.WhenAnyValue(
          x => x.AutoRefresh,
          y => y == true)
              .Subscribe(c => { _config.MsgUIItem.AutoRefresh = AutoRefresh; });

        MessageBus.Current.Listen<string>(EMsgCommand.SendMsgView.ToString()).Subscribe(OnNext);
    }

    private async void OnNext(string x)
    {
        await AppendQueueMsg(x);
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
        if (!_config.UiItem.ShowInTaskbar)
        {
            return;
        }

        _blLockShow = true;

        await Task.Delay(500);
        var txt = string.Join("", _queueMsg.ToArray());
        await _updateView?.Invoke(EViewAction.DispatcherShowMsg, txt);

        _blLockShow = false;
    }

    private async Task EnqueueQueueMsg(string msg)
    {
        //filter msg
        if (MsgFilter.IsNotEmpty() && !_lastMsgFilterNotAvailable)
        {
            try
            {
                if (!Regex.IsMatch(msg, MsgFilter))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _queueMsg.Enqueue(ex.Message);
                _lastMsgFilterNotAvailable = true;
            }
        }

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
        await Task.CompletedTask;
    }

    public void ClearMsg()
    {
        _queueMsg.Clear();
    }

    private void DoMsgFilter()
    {
        _config.MsgUIItem.MainMsgFilter = MsgFilter;
        _lastMsgFilterNotAvailable = false;
    }
}
