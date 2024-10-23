using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Reactive.Linq;

namespace ServiceLib.ViewModels
{
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
        public int SortingSelected { get; set; }

        [Reactive]
        public bool AutoRefresh { get; set; }

        public ClashConnectionsViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;
            SortingSelected = _config.ClashUIItem.ConnectionsSorting;
            AutoRefresh = _config.ClashUIItem.ConnectionsAutoRefresh;

            var canEditRemove = this.WhenAnyValue(
             x => x.SelectedSource,
             selectedSource => selectedSource != null && Utils.IsNotEmpty(selectedSource.id));

            this.WhenAnyValue(
              x => x.SortingSelected,
              y => y >= 0)
                  .Subscribe(async c => await DoSortingSelected(c));

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

            Init();
        }

        private async Task Init()
        {
            var lastTime = DateTime.Now;

            Observable.Interval(TimeSpan.FromSeconds(5))
              .Subscribe(async x =>
              {
                  if (!(AutoRefresh && _config.UiItem.ShowInTaskbar && _config.IsRunningCore(ECoreType.sing_box)))
                  {
                      return;
                  }
                  var dtNow = DateTime.Now;
                  if (_config.ClashUIItem.ConnectionsRefreshInterval > 0)
                  {
                      if ((dtNow - lastTime).Minutes % _config.ClashUIItem.ConnectionsRefreshInterval == 0)
                      {
                          await GetClashConnections();
                          lastTime = dtNow;
                      }
                      Task.Delay(1000).Wait();
                  }
              });
        }

        private async Task DoSortingSelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (SortingSelected != _config.ClashUIItem.ConnectionsSorting)
            {
                _config.ClashUIItem.ConnectionsSorting = SortingSelected;
            }

            await GetClashConnections();
        }

        private async Task GetClashConnections()
        {
            var ret = await ClashApiHandler.Instance.GetClashConnectionsAsync(_config);
            if (ret == null)
            {
                return;
            }

            _updateView?.Invoke(EViewAction.DispatcherRefreshConnections, ret?.connections);
        }

        public void RefreshConnections(List<ConnectionItem>? connections)
        {
            _connectionItems.Clear();

            var dtNow = DateTime.Now;
            var lstModel = new List<ClashConnectionModel>();
            foreach (var item in connections ?? [])
            {
                var host = $"{(Utils.IsNullOrEmpty(item.metadata.host) ? item.metadata.destinationIP : item.metadata.host)}:{item.metadata.destinationPort}";
                if (HostFilter.IsNotEmpty() && !host.Contains(HostFilter))
                {
                    continue;
                }

                ClashConnectionModel model = new();

                model.id = item.id;
                model.network = item.metadata.network;
                model.type = item.metadata.type;
                model.host = host;
                var sp = (dtNow - item.start);
                model.time = sp.TotalSeconds < 0 ? 1 : sp.TotalSeconds;
                model.upload = item.upload;
                model.download = item.download;
                model.uploadTraffic = $"{Utils.HumanFy((long)item.upload)}";
                model.downloadTraffic = $"{Utils.HumanFy((long)item.download)}";
                model.elapsed = sp.ToString(@"hh\:mm\:ss");
                model.chain = item.chains?.Count > 0 ? item.chains[0] : string.Empty;

                lstModel.Add(model);
            }
            if (lstModel.Count <= 0) { return; }

            //sort
            switch (SortingSelected)
            {
                case 0:
                    lstModel = lstModel.OrderBy(t => t.upload / t.time).ToList();
                    break;

                case 1:
                    lstModel = lstModel.OrderBy(t => t.download / t.time).ToList();
                    break;

                case 2:
                    lstModel = lstModel.OrderBy(t => t.upload).ToList();
                    break;

                case 3:
                    lstModel = lstModel.OrderBy(t => t.download).ToList();
                    break;

                case 4:
                    lstModel = lstModel.OrderBy(t => t.time).ToList();
                    break;

                case 5:
                    lstModel = lstModel.OrderBy(t => t.host).ToList();
                    break;
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
                id = item.id;
            }
            else
            {
                _connectionItems.Clear();
            }
            await ClashApiHandler.Instance.ClashConnectionClose(id);
            await GetClashConnections();
        }
    }
}