using MaterialDesignThemes.Wpf;

namespace v2rayN.Views;

public partial class SubSettingWindow
{
    public SubSettingWindow()
    {
        InitializeComponent();

        Closing += SubSettingWindow_Closing;
        lstSubscription.MouseDoubleClick += LstSubscription_MouseDoubleClick;
        lstSubscription.SelectionChanged += LstSubscription_SelectionChanged;
        menuClose.Click += menuClose_Click;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstSubscription.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstSubscription.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SubAddCmd, v => v.menuSubAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubDeleteCmd, v => v.menuSubDelete).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubEditCmd, v => v.menuSubEdit).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubShareCmd, v => v.menuSubShare).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SubAddCmd, v => v.menuSubAdd2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubDeleteCmd, v => v.menuSubDelete2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubEditCmd, v => v.menuSubEdit2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubShareCmd, v => v.menuSubShare2).DisposeWith(disposables);

            ViewModel.ShowYesNoInteraction.RegisterHandler(interaction =>
            {
                var message = interaction.Input;
                var result = UI.ShowYesNo(message) != MessageBoxResult.No;
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel.ShareSubInteraction.RegisterHandler(async interaction =>
            {
                var url = interaction.Input;
                if (url.IsNullOrEmpty())
                {
                    interaction.SetOutput(Unit.Default);
                    return;
                }
                await ShareSub(url);
                interaction.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private async Task ShareSub(string url)
    {
        if (url.IsNullOrEmpty())
        {
            return;
        }
        var img = QRCodeWindowsUtils.GetQRCode(url);
        var dialog = new QrcodeView()
        {
            imgQrcode = { Source = img },
            txtContent = { Text = url },
        };

        await DialogHost.Show(dialog, "SubDialog");
    }

    private void SubSettingWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            DialogResult = true;
        }
    }

    private void LstSubscription_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.EditSubAsync(false);
    }

    private void LstSubscription_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedSources = lstSubscription.SelectedItems.Cast<SubItem>().ToList();
        }
    }

    private void menuClose_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            DialogResult = true;
        }
        else
        {
            Close();
        }
    }
}
