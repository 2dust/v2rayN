using System.Windows.Controls;
using v2rayN.Base;

namespace v2rayN.Views;

/// <summary>
/// Interaction logic for ConnectionsView.xaml
/// </summary>
public partial class ClashConnectionsView
{
    private static Config _config;
    private static readonly string _tag = "ClashConnectionsView";

    public ClashConnectionsView()
    {
        InitializeComponent();
        _config = AppManager.Instance.Config;

        ViewModel = new ClashConnectionsViewModel(UpdateViewHandler);
        btnAutofitColumnWidth.Click += BtnAutofitColumnWidth_Click;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.ConnectionItems, v => v.lstConnections.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstConnections.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ConnectionCloseCmd, v => v.menuConnectionClose).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ConnectionCloseAllCmd, v => v.menuConnectionCloseAll).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.HostFilter, v => v.txtHostFilter.Text).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ConnectionCloseAllCmd, v => v.btnConnectionCloseAll).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);

            AppEvents.AppExitRequested
                .AsObservable()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => StorageUI())
                .DisposeWith(disposables);
        });

        RestoreUI();
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        return await Task.FromResult(true);
    }

    private void BtnAutofitColumnWidth_Click(object sender, RoutedEventArgs e)
    {
        AutofitColumnWidth();
    }

    private void AutofitColumnWidth()
    {
        try
        {
            foreach (var it in lstConnections.Columns)
            {
                it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void btnClose_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel?.ClashConnectionClose(false);
    }

    #region UI

    private void RestoreUI()
    {
        try
        {
            var lvColumnItem = _config.ClashUIItem?.ConnectionsColumnItem?.OrderBy(t => t.Index).ToList();
            if (lvColumnItem == null)
            {
                return;
            }

            var displayIndex = 0;
            foreach (var item in lvColumnItem)
            {
                foreach (var col in lstConnections.Columns.Cast<MyDGTextColumn>())
                {
                    if (col.ExName == item.Name)
                    {
                        if (item.Width > 0)
                        {
                            col.Width = item.Width;
                        }

                        col.DisplayIndex = displayIndex++;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private void StorageUI()
    {
        try
        {
            List<ColumnItem> lvColumnItem = new();
            foreach (var col in lstConnections.Columns.Cast<MyDGTextColumn>())
            {
                var name = col.ExName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                lvColumnItem.Add(new()
                {
                    Name = name,
                    Width = (int)col.ActualWidth,
                    Index = col.DisplayIndex
                });
            }

            _config.ClashUIItem.ConnectionsColumnItem = lvColumnItem;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    #endregion UI
}
