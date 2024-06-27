using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Models;

namespace v2rayN.ViewModels
{
    public class ClashConnectionsViewModel : ReactiveObject
    {
        private static Config _config;

        static ClashConnectionsViewModel()
        {
            _config = LazyConfig.Instance.GetConfig();
        }

        private IObservableCollection<ClashConnectionModel> _connectionItems = new ObservableCollectionExtended<ClashConnectionModel>();

        public IObservableCollection<ClashConnectionModel> ConnectionItems => _connectionItems;

        [Reactive]
        public ClashConnectionModel SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> ConnectionCloseCmd { get; }
        public ReactiveCommand<Unit, Unit> ConnectionCloseAllCmd { get; }

        [Reactive]
        public int SortingSelected { get; set; }

        [Reactive]
        public bool AutoRefresh { get; set; }

        private int AutoRefreshInterval;

        public ClashConnectionsViewModel()
        {
            AutoRefreshInterval = 10;
            SortingSelected = _config.clashUIItem.connectionsSorting;
            AutoRefresh = _config.clashUIItem.connectionsAutoRefresh;

            var canEditRemove = this.WhenAnyValue(
             x => x.SelectedSource,
             selectedSource => selectedSource != null && !string.IsNullOrEmpty(selectedSource.id));

            this.WhenAnyValue(
              x => x.SortingSelected,
              y => y >= 0)
                  .Subscribe(c => DoSortingSelected(c));

            this.WhenAnyValue(
               x => x.AutoRefresh,
               y => y == true)
                   .Subscribe(c => { _config.clashUIItem.connectionsAutoRefresh = AutoRefresh; });

            ConnectionCloseCmd = ReactiveCommand.Create(() =>
            {
                ClashConnectionClose(false);
            }, canEditRemove);

            ConnectionCloseAllCmd = ReactiveCommand.Create(() =>
            {
                ClashConnectionClose(true);
            });

            Init();
        }

        private void DoSortingSelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (SortingSelected != _config.clashUIItem.connectionsSorting)
            {
                _config.clashUIItem.connectionsSorting = SortingSelected;
            }

            GetClashConnections();
        }

        private void Init()
        {
            Observable.Interval(TimeSpan.FromSeconds(AutoRefreshInterval))
                .Subscribe(x =>
                {
                    if (!(AutoRefresh && ClashApiHandler.Instance.ShowInTaskbar))
                    {
                        return;
                    }
                    GetClashConnections();
                });
        }

        private void GetClashConnections()
        {
            ClashApiHandler.Instance.GetClashConnections(_config, (it) =>
            {
                if (it == null)
                {
                    return;
                }

                Application.Current?.Dispatcher.Invoke((Action)(() =>
                {
                    RefreshConnections(it?.connections);
                }));
            });
        }

        private void RefreshConnections(List<ConnectionItem>? connections)
        {
            _connectionItems.Clear();

            var dtNow = DateTime.Now;
            var lstModel = new List<ClashConnectionModel>();
            foreach (var item in connections ?? [])
            {
                ClashConnectionModel model = new();

                model.id = item.id;
                model.network = item.metadata.network;
                model.type = item.metadata.type;
                model.host = $"{(string.IsNullOrEmpty(item.metadata.host) ? item.metadata.destinationIP : item.metadata.host)}:{item.metadata.destinationPort}";
                var sp = (dtNow - item.start);
                model.time = sp.TotalSeconds < 0 ? 1 : sp.TotalSeconds;
                model.upload = item.upload;
                model.download = item.download;
                model.uploadTraffic = $"{Utils.HumanFy((long)item.upload)}";
                model.downloadTraffic = $"{Utils.HumanFy((long)item.download)}";
                model.elapsed = sp.ToString(@"hh\:mm\:ss");
                model.chain = item.chains?.Count > 0 ? item.chains[0] : String.Empty;

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

        public void ClashConnectionClose(bool all)
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
            ClashApiHandler.Instance.ClashConnectionClose(id);
            GetClashConnections();
        }
    }
}