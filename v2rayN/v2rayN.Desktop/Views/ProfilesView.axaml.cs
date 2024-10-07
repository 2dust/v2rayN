using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DialogHostAvalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class ProfilesView : ReactiveUserControl<ProfilesViewModel>
    {
        private static Config _config;
        private Window _window;

        public ProfilesView(Window window)
        {
            InitializeComponent();

            _config = AppHandler.Instance.Config;
            _window = window;

            menuSelectAll.Click += menuSelectAll_Click;
            btnAutofitColumnWidth.Click += BtnAutofitColumnWidth_Click;
            txtServerFilter.KeyDown += TxtServerFilter_KeyDown;
            menuStorageUI.Click += MenuStorageUI_Click;
            lstProfiles.KeyDown += LstProfiles_KeyDown;
            lstProfiles.SelectionChanged += lstProfiles_SelectionChanged;
            lstProfiles.DoubleTapped += LstProfiles_DoubleTapped;
            lstProfiles.LoadingRow += LstProfiles_LoadingRow;
            //if (_config.uiItem.enableDragDropSort)
            //{
            //    lstProfiles.AllowDrop = true;
            //    lstProfiles.PreviewMouseLeftButtonDown += LstProfiles_PreviewMouseLeftButtonDown;
            //    lstProfiles.MouseMove += LstProfiles_MouseMove;
            //    lstProfiles.DragEnter += LstProfiles_DragEnter;
            //    lstProfiles.Drop += LstProfiles_Drop;
            //}

            ViewModel = new ProfilesViewModel(UpdateViewHandler);
            Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(ProfilesViewModel));

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.ProfileItems, v => v.lstProfiles.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedProfile, v => v.lstProfiles.SelectedItem).DisposeWith(disposables);

                // this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstGroup.ItemsSource).DisposeWith(disposables);
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
                this.BindCommand(ViewModel, vm => vm.SetDefaultMultipleServerCmd, v => v.menuSetDefaultMultipleServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SetDefaultLoadBalanceServerCmd, v => v.menuSetDefaultLoadBalanceServer).DisposeWith(disposables);

                //servers move
                //this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.cmbMoveToGroup.ItemsSource).DisposeWith(disposables);
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

                //servers export
                this.BindCommand(ViewModel, vm => vm.Export2ClientConfigCmd, v => v.menuExport2ClientConfig).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.Export2ClientConfigClipboardCmd, v => v.menuExport2ClientConfigClipboard).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.Export2ShareUrlCmd, v => v.menuExport2ShareUrl).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.Export2ShareUrlBase64Cmd, v => v.menuExport2ShareUrlBase64).DisposeWith(disposables);
            });

            RestoreUI();
            ViewModel?.RefreshServers();
        }

        //#region Event

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.SetClipboardData:
                    if (obj is null) return false;
                    await AvaUtils.SetClipboardData(this, (string)obj);
                    break;

                case EViewAction.AdjustMainLvColWidth:
                    Dispatcher.UIThread.Post(() =>
                         AutofitColumnWidth(),
                   DispatcherPriority.Default);

                    break;

                case EViewAction.ProfilesFocus:
                    lstProfiles.Focus();
                    break;

                case EViewAction.ShowYesNo:
                    if (await UI.ShowYesNo(_window, ResUI.RemoveServer) == ButtonResult.No)
                    {
                        return false;
                    }
                    break;

                case EViewAction.SaveFileDialog:
                    if (obj is null) return false;
                    var fileName = await UI.SaveFileDialog(_window, "");
                    if (fileName.IsNullOrEmpty())
                    {
                        return false;
                    }
                    ViewModel?.Export2ClientConfigResult(fileName, (ProfileItem)obj);
                    break;

                case EViewAction.AddServerWindow:
                    if (obj is null) return false;
                    return await new AddServerWindow((ProfileItem)obj).ShowDialog<bool>(_window);

                case EViewAction.AddServer2Window:
                    if (obj is null) return false;
                    return await new AddServer2Window((ProfileItem)obj).ShowDialog<bool>(_window);

                case EViewAction.ShareServer:
                    if (obj is null) return false;
                    await ShareServer((string)obj);
                    break;

                case EViewAction.SubEditWindow:
                    if (obj is null) return false;
                    return await new SubEditWindow((SubItem)obj).ShowDialog<bool>(_window);

                case EViewAction.DispatcherSpeedTest:
                    if (obj is null) return false;
                    Dispatcher.UIThread.Post(() =>
                        ViewModel?.SetSpeedTestResult((SpeedTestResult)obj),
                    DispatcherPriority.Default);

                    break;

                case EViewAction.DispatcherRefreshServersBiz:
                    Dispatcher.UIThread.Post(() =>
                        ViewModel?.RefreshServersBiz(),
                    DispatcherPriority.Default);
                    break;
            }

            return await Task.FromResult(true);
        }

        public async Task ShareServer(string url)
        {
            if (Utils.IsNullOrEmpty(url))
            {
                return;
            }

            var dialog = new QrcodeView(url);
            await DialogHost.Show(dialog);
        }

        private void lstProfiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            List<ProfileItemModel> lst = [];
            foreach (var item in lstProfiles.SelectedItems)
            {
                lst.Add((ProfileItemModel)item);
            }
            ViewModel.SelectedProfiles = lst;
        }

        private void LstProfiles_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (_config.uiItem.doubleClick2Activate)
            {
                ViewModel?.SetDefaultServer();
            }
            else
            {
                ViewModel?.EditServerAsync(EConfigType.Custom);
            }            
        }

        private void LstProfiles_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            e.Row.Header = $" {e.Row.GetIndex() + 1}";
        }

        //private void LstProfiles_ColumnHeader_Click(object? sender, RoutedEventArgs e)
        //{
        //    var colHeader = sender as DataGridColumnHeader;
        //    if (colHeader == null || colHeader.TabIndex < 0 || colHeader.Column == null)
        //    {
        //        return;
        //    }

        //    var colName = ((MyDGTextColumn)colHeader.Column).ExName;
        //    ViewModel?.SortServer(colName);
        //}

        private void menuSelectAll_Click(object? sender, RoutedEventArgs e)
        {
            lstProfiles.SelectAll();
        }

        private void LstProfiles_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
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

        private void BtnAutofitColumnWidth_Click(object? sender, RoutedEventArgs e)
        {
            AutofitColumnWidth();
        }

        private void AutofitColumnWidth()
        {
            foreach (var it in lstProfiles.Columns)
            {
                it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }

        private void TxtServerFilter_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                ViewModel?.RefreshServers();
            }
        }
        private void MenuStorageUI_Click(object? sender, RoutedEventArgs e)
        {
            StorageUI();
        }

        //#endregion Event

        //#region UI

        private void RestoreUI()
        {
            var lvColumnItem = _config.uiItem.mainColumnItem.OrderBy(t => t.Index).ToList();
            var displayIndex = 0;
            foreach (var item in lvColumnItem)
            {
                foreach (var item2 in lstProfiles.Columns)
                {
                    if (item2.Tag == null)
                    {
                        continue;
                    }
                    if (item2.Tag.Equals(item.Name))
                    {
                        if (item.Width < 0)
                        {
                            item2.IsVisible = false;
                        }
                        else
                        {
                            item2.Width = new DataGridLength(item.Width, DataGridLengthUnitType.Pixel); ;
                            item2.DisplayIndex = displayIndex++;
                        }
                        if (item.Name.StartsWith("to"))
                        {
                            if (!_config.guiItem.enableStatistics)
                            {
                                item2.IsVisible = false;
                            }
                        }
                    }
                }
            }
        }

        private void StorageUI()
        {
            List<ColumnItem> lvColumnItem = new();
            for (int k = 0; k < lstProfiles.Columns.Count; k++)
            {
                var item2 = lstProfiles.Columns[k];
                if (item2.Tag == null)
                {
                    continue;
                }
                lvColumnItem.Add(new()
                {
                    Name = (string)item2.Tag,
                    Width = item2.IsVisible == true ? Utils.ToInt(item2.ActualWidth) : -1,
                    Index = item2.DisplayIndex
                });
            }
            _config.uiItem.mainColumnItem = lvColumnItem;
            ConfigHandler.SaveConfig(_config);
        }

        //#endregion UI

        //#region Drag and Drop

        //private Point startPoint = new();
        //private int startIndex = -1;
        //private string formatData = "ProfileItemModel";

        ///// <summary>
        ///// Helper to search up the VisualTree
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="current"></param>
        ///// <returns></returns>
        //private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        //{
        //    do
        //    {
        //        if (current is T)
        //        {
        //            return (T)current;
        //        }
        //        current = VisualTreeHelper.GetParent(current);
        //    }
        //    while (current != null);
        //    return null;
        //}

        //private void LstProfiles_PreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        //{
        //    // Get current mouse position
        //    startPoint = e.GetPosition(null);
        //}

        //private void LstProfiles_MouseMove(object? sender, MouseEventArgs e)
        //{
        //    // Get the current mouse position
        //    Point mousePos = e.GetPosition(null);
        //    Vector diff = startPoint - mousePos;

        //    if (e.LeftButton == MouseButtonState.Pressed &&
        //        (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
        //               Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        //    {
        //        // Get the dragged Item
        //        if (sender is not DataGrid listView) return;
        //        var listViewItem = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
        //        if (listViewItem == null) return;           // Abort
        //                                                    // Find the data behind the ListViewItem
        //        ProfileItemModel item = (ProfileItemModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
        //        if (item == null) return;                   // Abort
        //                                                    // Initialize the drag & drop operation
        //        startIndex = lstProfiles.SelectedIndex;
        //        DataObject dragData = new(formatData, item);
        //        DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Copy | DragDropEffects.Move);
        //    }
        //}

        //private void LstProfiles_DragEnter(object? sender, DragEventArgs e)
        //{
        //    if (!e.Data.GetDataPresent(formatData) || sender != e.Source)
        //    {
        //        e.Effects = DragDropEffects.None;
        //    }
        //}

        //private void LstProfiles_Drop(object? sender, DragEventArgs e)
        //{
        //    if (e.Data.GetDataPresent(formatData) && sender == e.Source)
        //    {
        //        // Get the drop Item destination
        //        if (sender is not DataGrid listView) return;
        //        var listViewItem = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
        //        if (listViewItem == null)
        //        {
        //            // Abort
        //            e.Effects = DragDropEffects.None;
        //            return;
        //        }
        //        // Find the data behind the Item
        //        ProfileItemModel item = (ProfileItemModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
        //        if (item == null) return;
        //        // Move item into observable collection
        //        // (this will be automatically reflected to lstView.ItemsSource)
        //        e.Effects = DragDropEffects.Move;

        //        ViewModel?.MoveServerTo(startIndex, item);

        //        startIndex = -1;
        //    }
        //}

        //#endregion Drag and Drop
    }
}