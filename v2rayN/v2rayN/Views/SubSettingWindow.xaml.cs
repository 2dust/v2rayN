using MaterialDesignThemes.Wpf;
using ReactiveUI;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using v2rayN.Enums;
using v2rayN.Models;
using v2rayN.Resx;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class SubSettingWindow
    {
        public SubSettingWindow()
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;

            ViewModel = new SubSettingViewModel(UpdateViewHandler);
            this.Closing += SubSettingWindow_Closing;
            lstSubscription.MouseDoubleClick += LstSubscription_MouseDoubleClick;
            lstSubscription.SelectionChanged += LstSubscription_SelectionChanged;

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstSubscription.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstSubscription.SelectedItem).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SubAddCmd, v => v.menuSubAdd).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubDeleteCmd, v => v.menuSubDelete).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubEditCmd, v => v.menuSubEdit).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SubShareCmd, v => v.menuSubShare).DisposeWith(disposables);
            });
        }

        private bool UpdateViewHandler(EViewAction action, object? obj)
        {
            if (action == EViewAction.CloseWindow)
            {
                this.DialogResult = true;
            }
            else if (action == EViewAction.ShowYesNo)
            {
                if (UI.ShowYesNo(ResUI.RemoveServer) == MessageBoxResult.No)
                {
                    return false;
                }
            }
            else if (action == EViewAction.SubEditWindow)
            {
                if (obj is null) return false;
                return (new SubEditWindow((SubItem)obj)).ShowDialog() ?? false;
            }
            else if (action == EViewAction.ShareSub)
            {
                if (obj is null) return false;
                ShareSub((string)obj);
            }
            return true;
        }

        private async void ShareSub(string url)
        {
            if (Utils.IsNullOrEmpty(url))
            {
                return;
            }
            var img = QRCodeHelper.GetQRCode(url);
            var dialog = new QrcodeView()
            {
                imgQrcode = { Source = img },
                txtContent = { Text = url },
            };

            await DialogHost.Show(dialog, "SubDialog");
        }

        private void SubSettingWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (ViewModel?.IsModified == true)
            {
                this.DialogResult = true;
            }
        }

        private void LstSubscription_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.EditSub(false);
        }

        private void LstSubscription_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.SelectedSources = lstSubscription.SelectedItems.Cast<SubItem>().ToList();
        }

        private void menuClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ViewModel?.IsModified == true)
            {
                this.DialogResult = true;
            }
            else
            {
                this.Close();
            }
        }
    }
}