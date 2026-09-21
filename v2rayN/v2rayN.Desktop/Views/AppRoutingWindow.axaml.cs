using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;
using v2rayN.Desktop.Manager;

namespace v2rayN.Desktop.Views;

public partial class AppRoutingWindow : WindowBase<AppRoutingViewModel>
{
    public AppRoutingWindow()
    {
        InitializeComponent();
        btnClose.Click += (_, _) => Close();

        this.WhenActivated(disposables =>
        {
            DataContext = ViewModel;

            ViewModel.BrowseExecutable.RegisterHandler(async interaction =>
            {
                interaction.SetOutput(await UI.OpenFileDialog(new Avalonia.Platform.Storage.FilePickerFileType("Executable") { Patterns = ["*.exe"] }));
            }).DisposeWith(disposables);
            ViewModel.PickProcess.RegisterHandler(async interaction =>
            {
                var picker = new AppRoutingAppPickerWindow { ViewModel = ViewModel, DataContext = ViewModel };
                interaction.SetOutput(await picker.ShowDialog<bool>(this) ? ViewModel.SelectedProcess : null);
            }).DisposeWith(disposables);
        });
    }
}
