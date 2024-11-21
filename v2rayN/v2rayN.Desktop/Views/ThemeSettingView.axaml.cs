using Avalonia;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Reactive.Disposables;
using v2rayN.Desktop.ViewModels;

namespace v2rayN.Desktop.Views
{
    /// <summary>
    /// ThemeSettingView.xaml
    /// </summary>
    public partial class ThemeSettingView : ReactiveUserControl<ThemeSettingViewModel>
    {
        public ThemeSettingView()
        {
            InitializeComponent();
            ViewModel = new ThemeSettingViewModel();

            for (int i = Global.MinFontSize; i <= Global.MinFontSize + 10; i++)
            {
                cmbCurrentFontSize.Items.Add(i);
            }

            Global.Languages.ForEach(it =>
            {
                cmbCurrentLanguage.Items.Add(it);
            });
            Global.ThemeModes.ForEach(it =>
            {
                cmbThemeMode.Items.Add(it);
            });
            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.ThemeMode, v => v.cmbThemeMode.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentFontSize, v => v.cmbCurrentFontSize.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.CurrentLanguage, v => v.cmbCurrentLanguage.SelectedValue).DisposeWith(disposables);
            });
        }
    }
}