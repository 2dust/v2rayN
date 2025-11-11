using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class SubEditWindow : WindowBase<SubEditViewModel>
{
    public SubEditWindow()
    {
        InitializeComponent();
    }

    public SubEditWindow(SubItem subItem)
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => Close();

        ViewModel = new SubEditViewModel(subItem, UpdateViewHandler);

        cmbConvertTarget.ItemsSource = Global.SubConvertTargets;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Url, v => v.txtUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.MoreUrl, v => v.txtMoreUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnable.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.AutoUpdateInterval, v => v.txtAutoUpdateInterval.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.UserAgent, v => v.txtUserAgent.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sort, v => v.txtSort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Filter, v => v.txtFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.ConvertTarget, v => v.cmbConvertTarget.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PrevProfile, v => v.txtPrevProfile.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.NextProfile, v => v.txtNextProfile.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PreSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Memo, v => v.txtMemo.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                Close(true);
                break;
        }
        return await Task.FromResult(true);
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private async void BtnSelectPrevProfile_Click(object? sender, RoutedEventArgs e)
    {
        var selectWindow = new ProfilesSelectWindow();
        selectWindow.SetConfigTypeFilter(new[] { EConfigType.Custom, EConfigType.PolicyGroup, EConfigType.ProxyChain }, exclude: true);
        var result = await selectWindow.ShowDialog<bool?>(this);
        if (result == true)
        {
            var profile = await selectWindow.ProfileItem;
            if (profile != null)
            {
                txtPrevProfile.Text = profile.Remarks;
            }
        }
    }

    private async void BtnSelectNextProfile_Click(object? sender, RoutedEventArgs e)
    {
        var selectWindow = new ProfilesSelectWindow();
        selectWindow.SetConfigTypeFilter(new[] { EConfigType.Custom, EConfigType.PolicyGroup, EConfigType.ProxyChain }, exclude: true);
        var result = await selectWindow.ShowDialog<bool?>(this);
        if (result == true)
        {
            var profile = await selectWindow.ProfileItem;
            if (profile != null)
            {
                txtNextProfile.Text = profile.Remarks;
            }
        }
    }
}
