using System.Windows.Controls;

namespace v2rayN.Views;

public partial class AppRoutingAppPickerWindow
{
    public AppRoutingAppPickerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            txtSearch.Focus();
            ViewModel?.RefreshProcessesCmd.Execute().Subscribe();
        };
        btnUseApp.Click += (_, _) => SelectApp();
        networkAppsGrid.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(networkAppsGrid, source) is DataGridRow)
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
            DialogResult = true;
        }
    }
}
