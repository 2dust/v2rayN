namespace v2rayN.Views;

public partial class WorkflowWindow
{
    public WorkflowWindow()
    {
        InitializeComponent();

        lstWorkflow.MouseDoubleClick += (_, _) => ViewModel?.EditWorkflowAsync(false);
        menuClose.Click += (_, _) => Close();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.WorkflowItems, v => v.lstWorkflow.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedWorkflow, v => v.lstWorkflow.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.AddCmd, v => v.menuWorkflowAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditCmd, v => v.menuWorkflowEdit).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DeleteCmd, v => v.menuWorkflowDelete).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RunCmd, v => v.menuWorkflowRun).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.AddCmd, v => v.menuWorkflowAdd2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditCmd, v => v.menuWorkflowEdit2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DeleteCmd, v => v.menuWorkflowDelete2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RunCmd, v => v.menuWorkflowRun2).DisposeWith(disposables);

            ViewModel.ShowYesNoInteraction.RegisterHandler(interaction =>
            {
                var result = UI.ShowYesNo(interaction.Input) != MessageBoxResult.No;
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel.EditWorkflowInteraction.RegisterHandler(async interaction =>
            {
                var workflowEditViewModel = new WorkflowEditViewModel(interaction.Input);
                var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(workflowEditViewModel);
                interaction.SetOutput(result == true);
            }).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }
}
