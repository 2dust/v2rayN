using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class WorkflowWindow : WindowBase<WorkflowViewModel>
{
    private bool _manualClose = false;

    public WorkflowWindow()
    {
        InitializeComponent();

        menuClose.Click += MenuClose_Click;
        Closing += WorkflowWindow_Closing;
        lstWorkflow.DoubleTapped += (_, _) => ViewModel?.EditWorkflowAsync(false);

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

            ViewModel.ShowYesNoInteraction.RegisterHandler(async interaction =>
            {
                var result = await UI.ShowYesNo(interaction.Input);
                interaction.SetOutput(result == ButtonResult.Yes);
            }).DisposeWith(disposables);

            ViewModel.EditWorkflowInteraction.RegisterHandler(async interaction =>
            {
                var workflowEditViewModel = new WorkflowEditViewModel(interaction.Input);
                var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(workflowEditViewModel);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);
        });
    }

    private void MenuClose_Click(object? sender, RoutedEventArgs e)
    {
        _manualClose = true;
        Close(ViewModel?.IsModified);
    }

    private void WorkflowWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel?.IsModified == true && !_manualClose)
        {
            MenuClose_Click(null, null);
        }
    }
}
