namespace ServiceLib.ViewModels;

public partial class ClashConnectionsViewModel : MyReactiveObject
{
    public ClashConnectionsViewModel()
    {
        _config = AppManager.Instance.Config;
        AutoRefresh = _config.ClashUIItem.ConnectionsAutoRefresh;

        var canEditRemove = this.WhenAnyValue(
            x => x.SelectedSource,
            selectedSource => selectedSource?.Id?.IsNotEmpty() == true);

        this.WhenAnyValue(
                x => x.AutoRefresh)
            .Subscribe(_ => { _config.ClashUIItem.ConnectionsAutoRefresh = AutoRefresh; });
        ConnectionCloseCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ClashConnectionClose(false);
        }, canEditRemove);

        ConnectionCloseAllCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ClashConnectionClose(true);
        });

        _ = Task.Factory.StartNew(
            async () => await GetClashConnectionsTask(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public BulkObservableCollection<ClashConnectionModel> ConnectionItems { get; } = [];

    [Reactive] public partial ClashConnectionModel SelectedSource { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> ConnectionCloseCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ConnectionCloseAllCmd { get; }

    [Reactive] public partial string HostFilter { get; set; }

    [Reactive] public partial bool AutoRefresh { get; set; }

    private async Task GetClashConnections()
    {
        var ret = await ClashApiManager.Instance.GetConnections();
        if (ret == null)
        {
            return;
        }

        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            _ = RefreshConnections(ret?.connections);
        });
    }

    public async Task RefreshConnections(List<ConnectionItem>? connections)
    {
        var dtNow = DateTime.Now;
        var lstModel = new List<ClashConnectionModel>();
        foreach (var item in connections ?? [])
        {
            if (item.metadata == null)
            {
                continue;
            }
            var dest = item.metadata.host.IsNullOrEmpty() ? item.metadata.destinationIP : item.metadata.host;
            var hostSb = new StringBuilder();
            hostSb.Append(dest);
            hostSb.Append($":{item.metadata.destinationPort}");
            if (!string.IsNullOrEmpty(item.metadata.sniffHost) &&
                dest?.Equals(item.metadata.sniffHost, StringComparison.OrdinalIgnoreCase) == false)
            {
                hostSb.Append($" ({item.metadata.sniffHost})");
            }
            var host = hostSb.ToString();
            if (HostFilter.IsNotEmpty() && !host.Contains(HostFilter))
            {
                continue;
            }

            var model = new ClashConnectionModel
            {
                Id = item.id,
                Network = item.metadata.network,
                Type = item.metadata.type,
                Host = host,
                Time = (dtNow - item.start).TotalSeconds < 0 ? 1 : (dtNow - item.start).TotalSeconds,
                Elapsed = (dtNow - item.start).ToString(@"hh\:mm\:ss"),
                Chain = $"{item.rule} , {string.Join("->", item.chains ?? [])}",
                ProcessPath = item.metadata.processPath,
            };

            lstModel.Add(model);
        }

        ConnectionItems.ReplaceRange(lstModel);
        await Task.CompletedTask;
    }

    public async Task ClashConnectionClose(bool all)
    {
        var id = string.Empty;
        if (!all)
        {
            var item = SelectedSource;
            if (string.IsNullOrEmpty(item?.Id))
            {
                return;
            }
            id = item.Id;
        }
        await ClashApiManager.Instance.CloseConnection(id);
        await GetClashConnections();
    }

    public async Task GetClashConnectionsTask()
    {
        var numOfExecuted = 1;
        while (true)
        {
            await Task.Delay(1000 * 5);
            numOfExecuted++;
            if (!(AutoRefresh && AppManager.Instance.ShowInTaskbar &&
                  AppManager.Instance.IsRunningCore(ECoreType.sing_box)))
            {
                continue;
            }

            if (_config.ClashUIItem.ConnectionsRefreshInterval <= 0)
            {
                continue;
            }

            if (numOfExecuted % _config.ClashUIItem.ConnectionsRefreshInterval != 0)
            {
                continue;
            }
            await GetClashConnections();
        }
    }
}
