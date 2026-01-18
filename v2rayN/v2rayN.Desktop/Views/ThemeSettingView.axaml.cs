using v2rayN.Desktop.ViewModels;

namespace v2rayN.Desktop.Views;

/// <summary>
/// ThemeSettingView.xaml
/// </summary>
public partial class ThemeSettingView : ReactiveUserControl<ThemeSettingViewModel>
{
    public ThemeSettingView()
    {
        InitializeComponent();
        ViewModel = new ThemeSettingViewModel();

        cmbCurrentTheme.ItemsSource = Utils.GetEnumNames<ETheme>();
        cmbCurrentFontSize.ItemsSource = Enumerable.Range(AppConfig.MinFontSize, AppConfig.MinFontSizeCount).ToList();
        cmbCurrentLanguage.ItemsSource = AppConfig.Languages;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.CurrentTheme, v => v.cmbCurrentTheme.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CurrentFontSize, v => v.cmbCurrentFontSize.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CurrentLanguage, v => v.cmbCurrentLanguage.SelectedValue).DisposeWith(disposables);
        });
    }
}
