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
    private readonly int _numMaxMsg = 500;
    private bool _lastMsgFilterNotAvailable;
    private bool _blLockShow = false;

    private readonly Queue<string> _window = new();
    private readonly int _maxLines = 350;

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

        this.WhenAnyValue(x => x.MsgFilter)
            .Subscribe(_ => DoMsgFilter());

        this.WhenAnyValue(x => x.AutoRefresh, y => y == true)
            .Subscribe(_ => { _config.MsgUIItem.AutoRefresh = AutoRefresh; });

        AppEvents.SendMsgViewRequested
            .AsObservable()
            //.ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async content => await AppendQueueMsg(content));
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

        var sb = new StringBuilder();
        var needRebuild = false;

        while (_queueMsg.TryDequeue(out var line))
        {
            _window.Enqueue(line);
            if (_window.Count > _maxLines)
            {
                _window.Dequeue();
                needRebuild = true;
            }
            if (!needRebuild)
            {
                sb.Append(line);
            }
        }

        if (needRebuild)
        {
            var sbAll = new StringBuilder();
            foreach (var s in _window)
            {
                sbAll.Append(s);
            }
            await _updateView?.Invoke(EViewAction.DispatcherShowMsg, sbAll.ToString());
        }
        else
        {
            if (sb.Length > 0)
            {
                await _updateView?.Invoke(EViewAction.DispatcherShowMsg, sb.ToString());
            }
        }

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

        // 限制待处理队列规模，避免短时洪峰占用
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
        _window.Clear();
    }

    private void DoMsgFilter()
    {
        _config.MsgUIItem.MainMsgFilter = MsgFilter;
        _lastMsgFilterNotAvailable = false;
    }
}
