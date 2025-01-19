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
            foreach (ETheme it in Enum.GetValues(typeof(ETheme)))
            {
                if ((int)it > 2) continue;
                cmbCurrentTheme.Items.Add(it.ToString());
            }

            for (int i = Global.MinFontSize; i <= Global.MinFontSize + 10; i++)
            {
                cmbCurrentFontSize.Items.Add(i.ToString());
            }

            Global.Languages.ForEach(it =>
            {
                cmbCurrentLanguage.Items.Add(it);
            });

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
}