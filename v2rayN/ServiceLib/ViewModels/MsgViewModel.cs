namespace ServiceLib.ViewModels;

public partial class MsgViewModel : MyReactiveObject
{
    public Interaction<string, RxVoid> ShowMsgInteraction { get; } = new();

    private readonly ConcurrentQueue<string> _queueMsg = new();
    private volatile bool _lastMsgFilterNotAvailable;
    public int NumMaxMsg => 500;

    [Reactive]
    public partial string MsgFilter { get; set; }

    [Reactive]
    public partial bool AutoRefresh { get; set; }

    public MsgViewModel()
    {
        _config = AppManager.Instance.Config;
        MsgFilter = _config.MsgUIItem.MainMsgFilter ?? string.Empty;
        AutoRefresh = _config.MsgUIItem.AutoRefresh ?? true;

        this.WhenAnyValue(
                x => x.MsgFilter)
            .Subscribe(c => DoMsgFilter());

        this.WhenAnyValue(x => x.AutoRefresh)
            .Subscribe(_ => _config.MsgUIItem.AutoRefresh = AutoRefresh);

        AppEvents.SendMsgViewRequested
            .AsObservable()
            .Subscribe(EnqueueQueueMsg);

        this.WhenActivated(disposables =>
        {
            Signal.Every(TimeSpan.FromSeconds(1))
                .Where(_ => AutoRefresh && AppManager.Instance.ShowInTaskbar)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => FlushQueueToView())
                .DisposeWith(disposables);
        });
    }

    private void FlushQueueToView()
    {
        if (!AutoRefresh || _queueMsg.IsEmpty)
        {
            return;
        }

        if (!AppManager.Instance.ShowInTaskbar)
        {
            return;
        }

        var sb = new StringBuilder();
        while (_queueMsg.TryDequeue(out var msg))
        {
            sb.Append(msg);
        }

        if (sb.Length > 0)
        {
            ShowMsgInteraction.HandleSafe(sb.ToString()).Subscribe();
        }
    }

    private void EnqueueQueueMsg(string msg)
    {
        if (string.IsNullOrEmpty(msg))
        {
            return;
        }

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

        var formattedMsg = msg.EndsWith(Environment.NewLine)
            ? msg
            : msg + Environment.NewLine;

        EnqueueWithLimit(formattedMsg);
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
