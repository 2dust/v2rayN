using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class AddServer2Window
    {
        public AddServer2Window(ProfileItem profileItem)
        {
            InitializeComponent();

            this.MaxWidth = SystemParameters.WorkArea.Width;
            this.MaxHeight = SystemParameters.WorkArea.Height;

            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;
            ViewModel = new AddServer2ViewModel(profileItem, this);

            foreach (ECoreType it in Enum.GetValues(typeof(ECoreType)))
            {
                if (it == ECoreType.v2rayN)
                    continue;
                cmbCoreType.Items.Add(it.ToString());
            }
            cmbCoreType.Items.Add(string.Empty);

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.address, v => v.txtAddress.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.coreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.displayLog, v => v.togDisplayLog.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.preSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.BrowseServerCmd, v => v.btnBrowse).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.EditServerCmd, v => v.btnEdit).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.SaveServerCmd, v => v.btnSave).DisposeWith(disposables);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();
        }

        private void btnCancel_Click(object sender, System.Windows.RoutedEventArgs e)
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