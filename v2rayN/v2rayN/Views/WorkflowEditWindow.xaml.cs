namespace v2rayN.Views;

public partial class WorkflowEditWindow
{
    public WorkflowEditWindow()
    {
        InitializeComponent();

        btnCancel.Click += (_, _) => Close();
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnable.IsChecked).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.Steps, v => v.lstSteps.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedStep, v => v.lstSteps.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.AddStepCmd, v => v.btnAddStep).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DeleteStepCmd, v => v.btnDeleteStep).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StepUpCmd, v => v.btnStepUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StepDownCmd, v => v.btnStepDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
