using ReactiveUI;
using Splat;
using System.ComponentModel;
using System.Drawing;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;
using v2rayN.ViewModels;
using Point = System.Windows.Point;
using SystemInformation = System.Windows.Forms.SystemInformation;

namespace v2rayN.Views
{
    public partial class MainWindow
    {
        private static Config _config;

        public MainWindow()
        {
            InitializeComponent();
            _config = LazyConfig.Instance.GetConfig();

            App.Current.SessionEnding += Current_SessionEnding;
            this.Closing += MainWindow_Closing;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            lstProfiles.PreviewKeyDown += LstProfiles_PreviewKeyDown;
            lstProfiles.SelectionChanged += lstProfiles_SelectionChanged;
            lstProfiles.LoadingRow += LstProfiles_LoadingRow;
            if (_config.uiItem.enableDragDropSort)
            {
                lstProfiles.AllowDrop = true;
                lstProfiles.PreviewMouseLeftButtonDown += LstProfiles_PreviewMouseLeftButtonDown;
                lstProfiles.MouseMove += LstProfiles_MouseMove;
                lstProfiles.DragEnter += LstProfiles_DragEnter;
                lstProfiles.Drop += LstProfiles_Drop;
            }

            ViewModel = new MainWindowViewModel(MainSnackbar.MessageQueue!, UpdateViewHandler);
            Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(MainWindowViewModel));

            Global.Languages.ForEach(it =>
            {
                cmbCurrentLanguage.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.ProfileItems, v => v.lstProfiles.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedProfile, v => v.lstProfiles.SelectedItem).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstGroup.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSub, v => v.lstGroup.SelectedItem).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.ServerFilter, v => v.txtServerFilter.Text).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddSubCmd, v => v.btnAddSub).DisposeWith(disposables);

                //servers
                this.BindCommand(ViewModel, vm => vm.AddVmessServerCmd, v => v.menuAddVmessServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddVlessServerCmd, v => v.menuAddVlessServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddShadowsocksServerCmd, v => v.menuAddShadowsocksServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddSocksServerCmd, v => v.menuAddSocksServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddTrojanServerCmd, v => v.menuAddTrojanServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddCustomServerCmd, v => v.menuAddCustomServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan).DisposeWith(disposables);

                //servers delete
                this.BindCommand(ViewModel, vm => vm.EditServerCmd, v => v.menuEditServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RemoveServerCmd, v => v.menuRemoveServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RemoveDuplicateServerCmd, v => v.menuRemoveDuplicateServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CopyServerCmd, v => v.menuCopyServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SetDefaultServerCmd, v => v.menuSetDefaultServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ShareServerCmd, v => v.menuShareServer).DisposeWith(disposables);

                //servers move
                this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.cmbMoveToGroup.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedMoveToGroup, v => v.cmbMoveToGroup.SelectedItem).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

                //servers ping 
                this.BindCommand(ViewModel, vm => vm.MixedTestServerCmd, v => v.menuMixedTestServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.PingServerCmd, v => v.menuPingServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.TcpingServerCmd, v => v.menuTcpingServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RealPingServerCmd, v => v.menuRealPingServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SpeedServerCmd, v => v.menuSpeedServer).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SortServerResultCmd, v => v.menuSortServerResult).DisposeWith(disposables);

                //servers export
                this.BindCommand(ViewModel, vm => vm.Export2ClientConfigCmd, v => v.menuExport2ClientConfig).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.Export2ServerConfigCmd, v => v.menuExport2ServerConfig).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.Export2ShareUrlCmd, v => v.menuExport2ShareUrl).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.Export2SubContentCmd, v => v.menuExport2SubContent).DisposeWith(disposables);

                //sub
                this.BindCommand(ViewModel, vm => vm.SubSettingCmd, v => v.menuSubSetting).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubGroupUpdateCmd, v => v.menuSubGroupUpdate).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubGroupUpdateViaProxyCmd, v => v.menuSubGroupUpdateViaProxy).DisposeWith(disposables);

                //setting
                this.BindCommand(ViewModel, vm => vm.OptionSettingCmd, v => v.menuOptionSetting).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingSettingCmd, v => v.menuRoutingSetting).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.GlobalHotkeySettingCmd, v => v.menuGlobalHotkeySetting).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ClearServerStatisticsCmd, v => v.menuClearServerStatistics).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportOldGuiConfigCmd, v => v.menuImportOldGuiConfig).DisposeWith(disposables);

                //checkupdate
                this.BindCommand(ViewModel, vm => vm.CheckUpdateNCmd, v => v.menuCheckUpdateN).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateV2flyCoreCmd, v => v.menuCheckUpdateV2flyCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateSagerNetCoreCmd, v => v.menuCheckUpdateSagerNetCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateXrayCoreCmd, v => v.menuCheckUpdateXrayCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateClashCoreCmd, v => v.menuCheckUpdateClashCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateClashMetaCoreCmd, v => v.menuCheckUpdateClashMetaCore).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateGeoCmd, v => v.menuCheckUpdateGeo).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);

                //system proxy
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxyClear, v => v.menuSystemProxyClear2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxySet, v => v.menuSystemProxySet2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxyNothing, v => v.menuSystemProxyNothing2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlSystemProxyPac, v => v.menuSystemProxyPac2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, vmToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxyClearCmd, v => v.menuSystemProxyClear).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxySetCmd, v => v.menuSystemProxySet).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxyPacCmd, v => v.menuSystemProxyPac).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SystemProxyNothingCmd, v => v.menuSystemProxyNothing).DisposeWith(disposables);

                //routings and servers
                this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.cmbRoutings.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings.SelectedItem).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.menuRoutings.Visibility).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.Servers, v => v.cmbServers.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedServer, v => v.cmbServers.SelectedItem).DisposeWith(disposables);

                //tray menu
                this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy2).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.NotifyIcon, v => v.tbNotify.Icon).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.NotifyLeftClickCmd, v => v.tbNotify.LeftClickCommand).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.AppIcon, v => v.Icon).DisposeWith(disposables);

                //status bar
                this.OneWayBind(ViewModel, vm => vm.InboundDisplay, v => v.txtInboundDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.InboundLanDisplay, v => v.txtInboundLanDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningServerDisplay, v => v.txtRunningServerDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningInfoDisplay, v => v.txtRunningInfoDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.SpeedProxyDisplay, v => v.txtSpeedProxyDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.SpeedDirectDisplay, v => v.txtSpeedDirectDisplay.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableTun, v => v.togEnableTun.IsChecked).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SystemProxySelected, v => v.cmbSystemProxy.SelectedIndex).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.cmbRoutings2.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings2.SelectedItem).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.cmbRoutings2.Visibility).DisposeWith(disposables);

                //UI
                this.Bind(ViewModel, vm => vm.ColorModeDark, v => v.togDarkMode.IsChecked).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.Swatches, v => v.cmbSwatches.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSwatch, v => v.cmbSwatches.SelectedItem).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentLanguage, v => v.cmbCurrentLanguage.Text).DisposeWith(disposables);
            });

            RestoreUI();
            AddHelpMenuItem();

            var IsAdministrator = Utils.IsAdministrator();
            this.Title = $"{Utils.GetVersion()} - {(IsAdministrator ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";

            spEnableTun.Visibility = IsAdministrator ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Event 

        private void UpdateViewHandler(string action)
        {
            if (action == "AdjustMainLvColWidth")
            {
                Application.Current.Dispatcher.Invoke((Action)(() =>
                {
                    foreach (var it in lstProfiles.Columns)
                    {
                        it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
                    }
                }));
            }
            else if (action == "ProfilesFocus")
            {
                lstProfiles.Focus();
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            ViewModel?.ShowHideWindow(false);
        }

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            tbNotify.Dispose();
            StorageUI();
            ViewModel?.MyAppExit(false);
        }

        private void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Utils.SaveLog("Current_SessionEnding");
            StorageUI();
            ViewModel?.MyAppExit(true);
        }

        private void lstProfiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.SelectedProfiles = lstProfiles.SelectedItems.Cast<ProfileItemModel>().ToList();
        }
        private void LstProfiles_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            //if (e.Row.GetIndex() == 0)
            //{
            //    lstProfiles.Focus();
            //}

            e.Row.Header = e.Row.GetIndex() + 1;
        }

        private void LstProfiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_config.uiItem.doubleClick2Activate)
            {
                ViewModel?.SetDefaultServer();
            }
            else
            {
                ViewModel?.EditServer(false, EConfigType.Custom);
            }
        }

        private void LstProfiles_ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var colHeader = sender as DataGridColumnHeader;
            if (colHeader == null || colHeader.TabIndex < 0 || colHeader.Column == null)
            {
                return;
            }

            //find index
            var index = lstProfiles.Columns.IndexOf(colHeader.Column);
            if (index < 0)
            {
                index = colHeader.TabIndex;
            }

            if (index == 0)
            {
                foreach (var it in lstProfiles.Columns)
                {
                    //it.MinWidth = it.ActualWidth;
                    it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
                }
                return;
            }

            ViewModel?.SortServer(index);
        }

        private void menuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lstProfiles.SelectAll();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.V)
                {
                    ViewModel?.AddServerViaClipboard();
                }
                else if (e.Key == Key.P)
                {
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Ping);
                }
                else if (e.Key == Key.O)
                {
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Tcping);
                }
                else if (e.Key == Key.R)
                {
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Realping);
                }
                else if (e.Key == Key.S)
                {
                    _ = ViewModel?.ScanScreenTaskAsync();
                }
                else if (e.Key == Key.T)
                {
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Speedtest);
                }
                else if (e.Key == Key.E)
                {
                    ViewModel?.ServerSpeedtest(ESpeedActionType.Mixedtest);
                }
            }
            else
            {
                if (e.Key == Key.F5)
                {
                    ViewModel?.Reload();
                }
            }
        }

        private void LstProfiles_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.A)
                {
                    menuSelectAll_Click(null, null);
                }
                else if (e.Key == Key.C)
                {
                    ViewModel?.Export2ShareUrl();
                }
                else if (e.Key == Key.D)
                {
                    ViewModel?.EditServer(false, EConfigType.Custom);
                }
                else if (e.Key == Key.F)
                {
                    ViewModel?.ShareServer();
                }
            }
            else
            {
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    ViewModel?.SetDefaultServer();
                }
                else if (e.Key == Key.Delete)
                {
                    ViewModel?.RemoveServer();
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
            }
        }


        private void menuClose_Click(object sender, RoutedEventArgs e)
        {
            StorageUI();
            ViewModel?.ShowHideWindow(false);
        }
        private void menuPromotion_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
        }

        private void txtRunningInfoDisplay_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.TestServerAvailability();
        }
        private void menuSettingsSetUWP_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart(Utils.GetBinPath("EnableLoopback.exe"));
        }
        #endregion

        #region UI

        private void RestoreUI()
        {
            if (_config.uiItem.mainWidth > 0 && _config.uiItem.mainHeight > 0)
            {
                Width = _config.uiItem.mainWidth;
                Height = _config.uiItem.mainHeight;
            }

            IntPtr hWnd = new WindowInteropHelper(this).EnsureHandle();
            Graphics g = Graphics.FromHwnd(hWnd);
            if (Width > SystemInformation.WorkingArea.Width * 96 / g.DpiX)
            {
                Width = SystemInformation.WorkingArea.Width * 96 / g.DpiX;
            }
            if (Height > SystemInformation.WorkingArea.Height * 96 / g.DpiY)
            {
                Height = SystemInformation.WorkingArea.Height * 96 / g.DpiY;
            }
            if (_config.uiItem.mainGirdHeight1 > 0 && _config.uiItem.mainGirdHeight2 > 0)
            {
                gridMain.RowDefinitions[0].Height = new GridLength(_config.uiItem.mainGirdHeight1, GridUnitType.Star);
                gridMain.RowDefinitions[2].Height = new GridLength(_config.uiItem.mainGirdHeight2, GridUnitType.Star);
            }

            for (int k = 0; k < lstProfiles.Columns.Count; k++)
            {
                var width = ConfigHandler.GetformMainLvColWidth(ref _config, ((EServerColName)k).ToString(), Convert.ToInt32(lstProfiles.Columns[k].Width.Value));
                lstProfiles.Columns[k].Width = width;
            }
            if (!_config.enableStatistics)
            {
                colTodayUp.Visibility = Visibility.Hidden;
                colTodayDown.Visibility = Visibility.Hidden;
                colTotalUp.Visibility = Visibility.Hidden;
                colTotalDown.Visibility = Visibility.Hidden;
            }
        }
        private void StorageUI()
        {
            _config.uiItem.mainWidth = this.Width;
            _config.uiItem.mainHeight = this.Height;

            for (int k = 0; k < lstProfiles.Columns.Count; k++)
            {
                ConfigHandler.AddformMainLvColWidth(ref _config, ((EServerColName)k).ToString(), Convert.ToInt32(lstProfiles.Columns[k].ActualWidth));
            }
            _config.uiItem.mainGirdHeight1 = Math.Ceiling(gridMain.RowDefinitions[0].ActualHeight + 0.1);
            _config.uiItem.mainGirdHeight2 = Math.Ceiling(gridMain.RowDefinitions[2].ActualHeight + 0.1);
        }

        private void AddHelpMenuItem()
        {
            var coreInfos = LazyConfig.Instance.GetCoreInfos();
            foreach (var it in coreInfos)
            {
                var item = new MenuItem()
                {
                    Tag = it.coreUrl.Replace(@"/releases", ""),
                    Header = string.Format(Resx.ResUI.menuWebsiteItem, it.coreType.ToString().Replace("_", " "))
                };
                item.Click += MenuItem_Click;
                menuHelp.Items.Add(item);
            }
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                MenuItem item = (MenuItem)sender;
                Utils.ProcessStart(item.Tag.ToString());
            }
        }


        #endregion
        #region Drag and Drop

        private Point startPoint = new Point();
        private int startIndex = -1;
        private string formatData = "ProfileItemModel";

        /// <summary>
        /// Helper to search up the VisualTree
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="current"></param>
        /// <returns></returns>
        private static T? FindAnchestor<T>(DependencyObject current) where T : DependencyObject
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
                var listView = sender as DataGrid;
                if (listView == null) return;
                var listViewItem = FindAnchestor<DataGridRow>((DependencyObject)e.OriginalSource);
                if (listViewItem == null) return;           // Abort
                                                            // Find the data behind the ListViewItem
                ProfileItemModel item = (ProfileItemModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                if (item == null) return;                   // Abort
                                                            // Initialize the drag & drop operation
                startIndex = lstProfiles.SelectedIndex;
                DataObject dragData = new DataObject(formatData, item);
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
                var listView = sender as DataGrid;
                if (listView == null) return;
                var listViewItem = FindAnchestor<DataGridRow>((DependencyObject)e.OriginalSource);
                if (listViewItem == null)
                {
                    // Abort
                    e.Effects = DragDropEffects.None;
                    return;
                }
                // Find the data behind the Item
                ProfileItemModel item = (ProfileItemModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                if (item == null) return;
                // Move item into observable collection 
                // (this will be automatically reflected to lstView.ItemsSource)
                e.Effects = DragDropEffects.Move;

                ViewModel?.MoveServerTo(startIndex, item);

                startIndex = -1;
            }
        }

        #endregion


    }
}
