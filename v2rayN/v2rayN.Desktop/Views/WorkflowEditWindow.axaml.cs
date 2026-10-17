using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class WorkflowEditWindow : WindowBase<WorkflowEditViewModel>
{
    public WorkflowEditWindow()
    {
        InitializeComponent();

        btnCancel.Click += (_, _) => Close(false);

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.AddStepCmd, v => v.btnAddStep).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DeleteStepCmd, v => v.btnDeleteStep).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StepUpCmd, v => v.btnStepUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StepDownCmd, v => v.btnStepDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
