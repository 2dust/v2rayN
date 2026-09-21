using Avalonia.VisualTree;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class AppRoutingAppPickerWindow : WindowBase<AppRoutingViewModel>
{
    public AppRoutingAppPickerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            txtSearch.Focus();
            ViewModel?.RefreshProcessesCmd.Execute().Subscribe();
        };
        btnCancel.Click += (_, _) => Close(false);
        btnUseApp.Click += (_, _) => SelectApp();
        networkAppsGrid.DoubleTapped += (_, e) =>
        {
            if (e.Source is Control source && (source is DataGridRow || source.FindAncestorOfType<DataGridRow>() != null))
            {
                SelectApp();
                e.Handled = true;
            }
        };
    }

    private void SelectApp()
    {
        if (ViewModel?.CanUseProcess == true)
        {
            Close(true);
        }
    }
}
