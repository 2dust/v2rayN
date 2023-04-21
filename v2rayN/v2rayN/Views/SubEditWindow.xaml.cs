using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class SubEditWindow
    {
        public SubEditWindow(SubItem subItem)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;

            ViewModel = new SubEditViewModel(subItem, this);

            Global.SubConvertTargets.ForEach(it =>
            {
                cmbConvertTarget.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.url, v => v.txtUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.moreUrl, v => v.txtMoreUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.enabled, v => v.togEnable.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.autoUpdateInterval, v => v.txtAutoUpdateInterval.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.userAgent, v => v.txtUserAgent.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.sort, v => v.txtSort.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.filter, v => v.txtFilter.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.convertTarget, v => v.cmbConvertTarget.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}