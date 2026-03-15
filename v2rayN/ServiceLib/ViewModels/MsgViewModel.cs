namespace ServiceLib.ViewModels;

public class MsgViewModel : MyReactiveObject
{
    private readonly ConcurrentQueue<string> _queueMsg = new();
    private volatile bool _lastMsgFilterNotAvailable;
    private int _showLock = 0; // 0 = unlocked, 1 = locked
    public int NumMaxMsg { get; } = 500;

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
              .Subscribe(c => _config.MsgUIItem.AutoRefresh = AutoRefresh);

        AppEvents.SendMsgViewRequested
         .AsObservable()
         //.ObserveOn(RxSchedulers.MainThreadScheduler)
         .Subscribe(content => _ = AppendQueueMsg(content));
    }

    private async Task AppendQueueMsg(string msg)
    {
        if (AutoRefresh == false)
        {
            return;
        }

        EnqueueQueueMsg(msg);

        if (!AppManager.Instance.ShowInTaskbar)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _showLock, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await Task.Delay(500).ConfigureAwait(false);

            var sb = new StringBuilder();
            while (_queueMsg.TryDequeue(out var line))
            {
                sb.Append(line);
            }

            await _updateView?.Invoke(EViewAction.DispatcherShowMsg, sb.ToString());
        }
        finally
        {
            Interlocked.Exchange(ref _showLock, 0);
        }
    }

    private void EnqueueQueueMsg(string msg)
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
                EnqueueWithLimit(ex.Message);
                _lastMsgFilterNotAvailable = true;
            }
        }

        EnqueueWithLimit(msg);
        if (!msg.EndsWith(Environment.NewLine))
        {
            EnqueueWithLimit(Environment.NewLine);
        }
    }

    private void EnqueueWithLimit(string item)
    {
        _queueMsg.Enqueue(item);

        while (_queueMsg.Count > NumMaxMsg)
        {
            _queueMsg.TryDequeue(out _);
        }
    }

    //public void ClearMsg()
    //{
    //    _queueMsg.Clear();
    //}

    private void DoMsgFilter()
    {
        _config.MsgUIItem.MainMsgFilter = MsgFilter;
        _lastMsgFilterNotAvailable = false;
    }
}
