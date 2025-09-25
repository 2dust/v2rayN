using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class MsgViewModel : MyReactiveObject
{
    private readonly ConcurrentQueue<string> _queueMsg = new();
    private readonly StringBuilder _msgBuilder = new();
    private readonly int _numMaxMsg = 500;
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

        AppEvents.SendMsgViewRequested
         .AsObservable()
         //.ObserveOn(RxApp.MainThreadScheduler)
         .Subscribe(async content => await AppendQueueMsg(content));
    }

    private async Task AppendQueueMsg(string msg)
    {        
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

        _msgBuilder.Clear();
        //foreach (var it in _queueMsg)
        //{
        //    _msgBuilder.Append(it);
        //}
        while (_queueMsg.TryDequeue(out var line))
        {
            _msgBuilder.Append(line);
        }


        await _updateView?.Invoke(EViewAction.DispatcherShowMsg, _msgBuilder.ToString());

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
