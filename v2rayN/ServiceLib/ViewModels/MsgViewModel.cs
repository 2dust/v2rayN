namespace ServiceLib.ViewModels;

public class MsgViewModel : MyReactiveObject
{
    private const int MaxQueuedMessagesVisible = 2_000;
    private const int MaxQueuedMessagesHidden = 500;
    private const int MaxFlushMessages = 200;
    private const int MaxFlushChars = 64 * 1024;
    private readonly ConcurrentQueue<string> _queueMsg = new();
    private volatile bool _lastMsgFilterNotAvailable;
    private int _queuedMessageCount = 0;
    private int _showLock = 0; // 0 = unlocked, 1 = locked
    private long _droppedMessageCount = 0;
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
         //.ObserveOn(RxApp.MainThreadScheduler)
         .Subscribe(content => _ = AppendQueueMsg(content));
    }

    private Task AppendQueueMsg(string msg)
    {
        if (AutoRefresh == false)
        {
            return Task.CompletedTask;
        }

        if (!EnqueueQueueMsg(msg))
        {
            return Task.CompletedTask;
        }

        TryScheduleDrain();
        return Task.CompletedTask;
    }

    private void TryScheduleDrain()
    {
        if (!AppManager.Instance.ShowInTaskbar)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _showLock, 1, 0) != 0)
        {
            return;
        }

        _ = DrainQueueMsgAsync();
    }

    private async Task DrainQueueMsgAsync()
    {
        try
        {
            await Task.Delay(500).ConfigureAwait(false);

            var batch = DequeueBatch();
            if (_updateView != null && !string.IsNullOrEmpty(batch))
            {
                await _updateView(EViewAction.DispatcherShowMsg, batch);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _showLock, 0);
        }

        if (AppManager.Instance.ShowInTaskbar && !_queueMsg.IsEmpty)
        {
            TryScheduleDrain();
        }
    }

    private string DequeueBatch()
    {
        var sb = new StringBuilder();
        var droppedCount = Interlocked.Exchange(ref _droppedMessageCount, 0);
        if (droppedCount > 0)
        {
            sb.Append($"----- Message queue trimmed: dropped {droppedCount} messages -----{Environment.NewLine}");
        }

        var dequeuedCount = 0;
        while (dequeuedCount < MaxFlushMessages
               && _queueMsg.TryDequeue(out var line))
        {
            Interlocked.Decrement(ref _queuedMessageCount);
            sb.Append(line);
            dequeuedCount++;

            if (sb.Length >= MaxFlushChars)
            {
                break;
            }
        }

        return sb.ToString();
    }

    private bool EnqueueQueueMsg(string msg)
    {
        //filter msg
        if (MsgFilter.IsNotEmpty() && !_lastMsgFilterNotAvailable)
        {
            try
            {
                if (!Regex.IsMatch(msg, MsgFilter))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                _lastMsgFilterNotAvailable = true;
            }
        }

        _queueMsg.Enqueue(NormalizeMessage(msg));
        Interlocked.Increment(ref _queuedMessageCount);
        TrimQueuedMessages();
        return true;
    }

    private void TrimQueuedMessages()
    {
        var maxQueuedMessages = AppManager.Instance.ShowInTaskbar
            ? MaxQueuedMessagesVisible
            : MaxQueuedMessagesHidden;

        while (Volatile.Read(ref _queuedMessageCount) > maxQueuedMessages
               && _queueMsg.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _queuedMessageCount);
            Interlocked.Increment(ref _droppedMessageCount);
        }
    }

    private static string NormalizeMessage(string msg)
    {
        return msg.EndsWith(Environment.NewLine) ? msg : msg + Environment.NewLine;
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
