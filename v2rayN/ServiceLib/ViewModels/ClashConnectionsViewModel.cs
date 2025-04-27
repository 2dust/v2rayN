using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class ClashConnectionsViewModel : MyReactiveObject
{
    private IObservableCollection<ClashConnectionModel> _connectionItems = new ObservableCollectionExtended<ClashConnectionModel>();
    public IObservableCollection<ClashConnectionModel> ConnectionItems => _connectionItems;

    [Reactive]
    public ClashConnectionModel SelectedSource { get; set; }

    public ReactiveCommand<Unit, Unit> ConnectionCloseCmd { get; }
    public ReactiveCommand<Unit, Unit> ConnectionCloseAllCmd { get; }

    [Reactive]
    public string HostFilter { get; set; }

    [Reactive]
    public bool AutoRefresh { get; set; }

    public ClashConnectionsViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        _updateView = updateView;
        AutoRefresh = _config.ClashUIItem.ConnectionsAutoRefresh;

        var canEditRemove = this.WhenAnyValue(
         x => x.SelectedSource,
         selectedSource => selectedSource != null && selectedSource.Id.IsNotEmpty());

        this.WhenAnyValue(
           x => x.AutoRefresh,
           y => y == true)
               .Subscribe(c => { _config.ClashUIItem.ConnectionsAutoRefresh = AutoRefresh; });
        ConnectionCloseCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ClashConnectionClose(false);
        }, canEditRemove);

        ConnectionCloseAllCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ClashConnectionClose(true);
        });

        _ = Init();
    }

    private async Task Init()
    {
        await DelayTestTask();
    }

    private async Task GetClashConnections()
    {
        var ret = await ClashApiHandler.Instance.GetClashConnectionsAsync();
        if (ret == null)
        {
            return;
        }

        _ = _updateView?.Invoke(EViewAction.DispatcherRefreshConnections, ret?.connections);
    }

    public void RefreshConnections(List<ConnectionItem>? connections)
    {
        _connectionItems.Clear();

        var dtNow = DateTime.Now;
        var lstModel = new List<ClashConnectionModel>();
        foreach (var item in connections ?? new())
        {
            var host = $"{(item.metadata.host.IsNullOrEmpty() ? item.metadata.destinationIP : item.metadata.host)}:{item.metadata.destinationPort}";
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
                Chain = $"{item.rule} , {string.Join("->", item.chains ?? new())}"
            };

            lstModel.Add(model);
        }
        if (lstModel.Count <= 0)
        {
            return;
        }

        _connectionItems.AddRange(lstModel);
    }

    public async Task ClashConnectionClose(bool all)
    {
        var id = string.Empty;
        if (!all)
        {
            var item = SelectedSource;
            if (item is null)
            {
                return;
            }
            id = item.Id;
        }
        else
        {
            _connectionItems.Clear();
        }
        await ClashApiHandler.Instance.ClashConnectionClose(id);
        await GetClashConnections();
    }

    public async Task DelayTestTask()
    {
        _ = Task.Run(async () =>
        {
            var numOfExecuted = 1;
            while (true)
            {
                await Task.Delay(1000 * 5);
                numOfExecuted++;
                if (!(AutoRefresh && _config.UiItem.ShowInTaskbar && _config.IsRunningCore(ECoreType.sing_box)))
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
        });

        await Task.CompletedTask;
    }
}
