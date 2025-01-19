using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;

namespace v2rayN.Views
{
    public partial class SubEditWindow
    {
        public SubEditWindow(SubItem subItem)
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;

            ViewModel = new SubEditViewModel(subItem, UpdateViewHandler);

            Global.SubConvertTargets.ForEach(it =>
            {
                cmbConvertTarget.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.Url, v => v.txtUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.MoreUrl, v => v.txtMoreUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnable.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.AutoUpdateInterval, v => v.txtAutoUpdateInterval.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.UserAgent, v => v.txtUserAgent.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.Sort, v => v.txtSort.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.Filter, v => v.txtFilter.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.ConvertTarget, v => v.cmbConvertTarget.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.PrevProfile, v => v.txtPrevProfile.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.NextProfile, v => v.txtNextProfile.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.PreSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.Memo, v => v.txtMemo.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
            WindowsUtils.SetDarkBorder(this, AppHandler.Instance.Config.UiItem.CurrentTheme);
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.CloseWindow:
                    this.DialogResult = true;
                    break;
            }
            return await Task.FromResult(true);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();
        }
    }
}