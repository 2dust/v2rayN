using ReactiveUI;
using System.Reactive.Disposables;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    /// <summary>
    /// ThemeSettingView.xaml
    /// </summary>
    public partial class ThemeSettingView
    {
        public ThemeSettingView()
        {
            InitializeComponent();
            ViewModel = new ThemeSettingViewModel();

            for (int i = Global.MinFontSize; i <= Global.MinFontSize + 8; i++)
            {
                cmbCurrentFontSize.Items.Add(i.ToString());
            }

            Global.Languages.ForEach(it =>
            {
                cmbCurrentLanguage.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.ColorModeDark, v => v.togDarkMode.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.FollowSystemTheme, v => v.followSystemTheme.IsChecked).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.Swatches, v => v.cmbSwatches.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSwatch, v => v.cmbSwatches.SelectedItem).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentFontSize, v => v.cmbCurrentFontSize.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentLanguage, v => v.cmbCurrentLanguage.Text).DisposeWith(disposables);
            });
        }
    }
}