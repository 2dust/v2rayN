using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows.Input;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class SubSettingWindow
    {
        public SubSettingWindow()
        {
            InitializeComponent();

            ViewModel = new SubSettingViewModel(this);
            lstSubscription.MouseDoubleClick += LstSubscription_MouseDoubleClick;

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

        private void LstSubscription_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.EditSub(false);
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
