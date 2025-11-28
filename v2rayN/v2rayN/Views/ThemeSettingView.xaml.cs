using v2rayN.ViewModels;

namespace v2rayN.Views;

/// <summary>
/// ThemeSettingView.xaml
/// </summary>
public partial class ThemeSettingView
{
    public ThemeSettingView()
    {
        InitializeComponent();
        ViewModel = new ThemeSettingViewModel();

        cmbCurrentTheme.ItemsSource = Utils.GetEnumNames<ETheme>().Take(3).ToList();
        cmbCurrentFontSize.ItemsSource = Enumerable.Range(Global.MinFontSize, Global.MinFontSizeCount).ToList();
        cmbCurrentLanguage.ItemsSource = Global.Languages;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.CurrentTheme, v => v.cmbCurrentTheme.SelectedValue).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Swatches, v => v.cmbSwatches.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSwatch, v => v.cmbSwatches.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CurrentFontSize, v => v.cmbCurrentFontSize.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CurrentLanguage, v => v.cmbCurrentLanguage.Text).DisposeWith(disposables);
        });
    }
}
