using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using ReactiveUI;
using ServiceLib.Manager;
using Splat;
using v2rayN.Base;
using Point = System.Windows.Point;

namespace v2rayN.Views;

public partial class ProfilesView
{
    private static Config _config;

    public ProfilesView()
    {
        InitializeComponent();
        lstGroup.MaxHeight = Math.Floor(SystemParameters.WorkArea.Height * 0.20 / 40) * 40;

        _config = AppManager.Instance.Config;

        btnAutofitColumnWidth.Click += BtnAutofitColumnWidth_Click;
        txtServerFilter.PreviewKeyDown += TxtServerFilter_PreviewKeyDown;
        lstProfiles.PreviewKeyDown += LstProfiles_PreviewKeyDown;
        lstProfiles.SelectionChanged += lstProfiles_SelectionChanged;
        lstProfiles.LoadingRow += LstProfiles_LoadingRow;
        menuSelectAll.Click += menuSelectAll_Click;

        if (_config.UiItem.EnableDragDropSort)
        {
            lstProfiles.AllowDrop = true;
            lstProfiles.PreviewMouseLeftButtonDown += LstProfiles_PreviewMouseLeftButtonDown;
            lstProfiles.MouseMove += LstProfiles_MouseMove;
            lstProfiles.DragEnter += LstProfiles_DragEnter;
            lstProfiles.Drop += LstProfiles_Drop;
        }

        ViewModel = new ProfilesViewModel(UpdateViewHandler);
        Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(ProfilesViewModel));

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.ProfileItems, v => v.lstProfiles.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedProfile, v => v.lstProfiles.SelectedItem).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstGroup.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSub, v => v.lstGroup.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ServerFilter, v => v.txtServerFilter.Text).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddSubCmd, v => v.btnAddSub).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditSubCmd, v => v.btnEditSub).DisposeWith(disposables);

            //servers delete
            this.BindCommand(ViewModel, vm => vm.EditServerCmd, v => v.menuEditServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RemoveServerCmd, v => v.menuRemoveServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RemoveDuplicateServerCmd, v => v.menuRemoveDuplicateServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CopyServerCmd, v => v.menuCopyServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetDefaultServerCmd, v => v.menuSetDefaultServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ShareServerCmd, v => v.menuShareServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetDefaultMultipleServerXrayRandomCmd, v => v.menuSetDefaultMultipleServerXrayRandom).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetDefaultMultipleServerXrayRoundRobinCmd, v => v.menuSetDefaultMultipleServerXrayRoundRobin).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetDefaultMultipleServerXrayLeastPingCmd, v => v.menuSetDefaultMultipleServerXrayLeastPing).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetDefaultMultipleServerXrayLeastLoadCmd, v => v.menuSetDefaultMultipleServerXrayLeastLoad).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetDefaultMultipleServerSingBoxLeastPingCmd, v => v.menuSetDefaultMultipleServerSingBoxLeastPing).DisposeWith(disposables);

            //servers move
            this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.cmbMoveToGroup.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedMoveToGroup, v => v.cmbMoveToGroup.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

            //servers ping
            this.BindCommand(ViewModel, vm => vm.MixedTestServerCmd, v => v.menuMixedTestServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.TcpingServerCmd, v => v.menuTcpingServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RealPingServerCmd, v => v.menuRealPingServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SpeedServerCmd, v => v.menuSpeedServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SortServerResultCmd, v => v.menuSortServerResult).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RemoveInvalidServerResultCmd, v => v.menuRemoveInvalidServerResult).DisposeWith(disposables);

            //servers export
            this.BindCommand(ViewModel, vm => vm.Export2ClientConfigCmd, v => v.menuExport2ClientConfig).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.Export2ClientConfigClipboardCmd, v => v.menuExport2ClientConfigClipboard).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.Export2ShareUrlCmd, v => v.menuExport2ShareUrl).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.Export2ShareUrlBase64Cmd, v => v.menuExport2ShareUrlBase64).DisposeWith(disposables);
        });

        RestoreUI();
        ViewModel?.RefreshServers();
        MessageBus.Current.Listen<string>(EMsgCommand.AppExit.ToString()).Subscribe(StorageUI);
    }

    #region Event

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.SetClipboardData:
                if (obj is null)
                    return false;
                WindowsUtils.SetClipboardData((string)obj);
                break;

            case EViewAction.AdjustMainLvColWidth:
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    AutofitColumnWidth();
                }), DispatcherPriority.Normal);
                break;

            case EViewAction.ProfilesFocus:
                lstProfiles.Focus();
                break;

            case EViewAction.ShowYesNo:
                if (UI.ShowYesNo(ResUI.RemoveServer) == MessageBoxResult.No)
                {
                    return false;
                }
                break;

            case EViewAction.SaveFileDialog:
                if (obj is null)
                    return false;
                if (UI.SaveFileDialog(out string fileName, "Config|*.json") != true)
                {
                    return false;
                }
                ViewModel?.Export2ClientConfigResult(fileName, (ProfileItem)obj);
                break;

            case EViewAction.AddServerWindow:
                if (obj is null)
                    return false;
                return (new AddServerWindow((ProfileItem)obj)).ShowDialog() ?? false;

            case EViewAction.AddServer2Window:
                if (obj is null)
                    return false;
                return (new AddServer2Window((ProfileItem)obj)).ShowDialog() ?? false;

            case EViewAction.ShareServer:
                if (obj is null)
                    return false;
                ShareServer((string)obj);
                break;

            case EViewAction.SubEditWindow:
                if (obj is null)
                    return false;
                return (new SubEditWindow((SubItem)obj)).ShowDialog() ?? false;

            case EViewAction.DispatcherSpeedTest:
                if (obj is null)
                    return false;
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    ViewModel?.SetSpeedTestResult((SpeedTestResult)obj);
                }), DispatcherPriority.Normal);
                break;

            case EViewAction.DispatcherRefreshServersBiz:
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    _ = RefreshServersBiz();
                }), DispatcherPriority.Normal);
                break;
        }

        return await Task.FromResult(true);
    }

    public async void ShareServer(string url)
    {
        var img = QRCodeUtils.GetQRCode(url);
        var dialog = new QrcodeView()
        {
            imgQrcode = { Source = img },
            txtContent = { Text = url },
        };

        await DialogHost.Show(dialog, "RootDialog");
    }

    public async Task RefreshServersBiz()
    {
        if (ViewModel != null)
        {
            await ViewModel.RefreshServersBiz();
        }

        if (lstProfiles.SelectedIndex > 0)
        {
            lstProfiles.ScrollIntoView(lstProfiles.SelectedItem, null);
        }
    }

    private void lstProfiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedProfiles = lstProfiles.SelectedItems.Cast<ProfileItemModel>().ToList();
        }
    }

    private void LstProfiles_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Header = $" {e.Row.GetIndex() + 1}";
    }

    private void LstProfiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_config.UiItem.DoubleClick2Activate)
        {
            ViewModel?.SetDefaultServer();
        }
        else
        {
            ViewModel?.EditServerAsync(EConfigType.Custom);
        }
    }

    private void LstProfiles_ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        var colHeader = sender as DataGridColumnHeader;
        if (colHeader == null || colHeader.TabIndex < 0 || colHeader.Column == null)
        {
            return;
        }

        var colName = ((MyDGTextColumn)colHeader.Column).ExName;
        ViewModel?.SortServer(colName);
    }

    private void menuSelectAll_Click(object sender, RoutedEventArgs e)
    {
        lstProfiles.SelectAll();
    }

    private void LstProfiles_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            switch (e.Key)
            {
                case Key.A:
                    menuSelectAll_Click(null, null);
                    break;

                case Key.C:
                    ViewModel?.Export2ShareUrlAsync(false);
                    break;

                case Key.D:
                    ViewModel?.EditServerAsync(EConfigType.Custom);
                    break;

                case Key.F:
                    ViewModel?.ShareServerAsync();
                    break;

                case Key.O:
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Tcping);
                    break;

                case Key.R:
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Realping);
                    break;

                case Key.T:
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Speedtest);
                    break;

                case Key.E:
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Mixedtest);
                    break;
            }
        }
        else
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                ViewModel?.SetDefaultServer();
            }
            else if (e.Key == Key.Delete)
            {
                ViewModel?.RemoveServerAsync();
            }
            else if (e.Key == Key.T)
            {
                ViewModel?.MoveServer(EMove.Top);
            }
            else if (e.Key == Key.U)
            {
                ViewModel?.MoveServer(EMove.Up);
            }
            else if (e.Key == Key.D)
            {
                ViewModel?.MoveServer(EMove.Down);
            }
            else if (e.Key == Key.B)
            {
                ViewModel?.MoveServer(EMove.Bottom);
            }
            else if (e.Key == Key.Escape)
            {
                ViewModel?.ServerSpeedtestStop();
            }
        }
    }

    private void BtnAutofitColumnWidth_Click(object sender, RoutedEventArgs e)
    {
        AutofitColumnWidth();
    }

    private void AutofitColumnWidth()
    {
        try
        {
            foreach (var it in lstProfiles.Columns)
            {
                it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ProfilesView", ex);
        }
    }

    private void TxtServerFilter_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            ViewModel?.RefreshServers();
        }
    }

    #endregion Event

    #region UI

    private void RestoreUI()
    {
        var lvColumnItem = _config.UiItem.MainColumnItem.OrderBy(t => t.Index).ToList();
        var displayIndex = 0;
        foreach (var item in lvColumnItem)
        {
            foreach (MyDGTextColumn item2 in lstProfiles.Columns)
            {
                if (item2.ExName == item.Name)
                {
                    if (item.Width < 0)
                    {
                        item2.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        item2.Width = item.Width;
                        item2.DisplayIndex = displayIndex++;
                    }
                    if (item.Name.ToLower().StartsWith("to"))
                    {
                        item2.Visibility = _config.GuiItem.EnableStatistics ? Visibility.Visible : Visibility.Hidden;
                    }
                }
            }
        }
    }

    private void StorageUI(string? n = null)
    {
        List<ColumnItem> lvColumnItem = new();
        foreach (var t in lstProfiles.Columns)
        {
            var item2 = (MyDGTextColumn)t;
            lvColumnItem.Add(new()
            {
                Name = item2.ExName,
                Width = (int)(item2.Visibility == Visibility.Visible ? item2.ActualWidth : -1),
                Index = item2.DisplayIndex
            });
        }
        _config.UiItem.MainColumnItem = lvColumnItem;
    }

    #endregion UI

    #region Drag and Drop

    private Point startPoint = new();
    private int startIndex = -1;
    private string formatData = "ProfileItemModel";

    /// <summary>
    /// Helper to search up the VisualTree
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="current"></param>
    /// <returns></returns>
    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        do
        {
            if (current is T)
            {
                return (T)current;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        while (current != null);
        return null;
    }

    private void LstProfiles_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Get current mouse position
        startPoint = e.GetPosition(null);
    }

    private void LstProfiles_MouseMove(object sender, MouseEventArgs e)
    {
        // Get the current mouse position
        Point mousePos = e.GetPosition(null);
        Vector diff = startPoint - mousePos;

        if (e.LeftButton == MouseButtonState.Pressed &&
            (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            // Get the dragged Item
            if (sender is not DataGrid listView)
                return;
            var listViewItem = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
            if (listViewItem == null)
                return;           // Abort
                                  // Find the data behind the ListViewItem
            ProfileItemModel item = (ProfileItemModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
            if (item == null)
                return;                   // Abort
                                          // Initialize the drag & drop operation
            startIndex = lstProfiles.SelectedIndex;
            DataObject dragData = new(formatData, item);
            DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }

    private void LstProfiles_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(formatData) || sender != e.Source)
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void LstProfiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(formatData) && sender == e.Source)
        {
            // Get the drop Item destination
            if (sender is not DataGrid listView)
                return;
            var listViewItem = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
            if (listViewItem == null)
            {
                // Abort
                e.Effects = DragDropEffects.None;
                return;
            }
            // Find the data behind the Item
            ProfileItemModel item = (ProfileItemModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
            if (item == null)
                return;
            // Move item into observable collection
            // (this will be automatically reflected to lstView.ItemsSource)
            e.Effects = DragDropEffects.Move;

            ViewModel?.MoveServerTo(startIndex, item);

            startIndex = -1;
        }
    }

    #endregion Drag and Drop
}
