using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;

namespace v2rayN.Desktop.Views;

public partial class CheckUpdateView : ReactiveUserControl<CheckUpdateViewModel>
{
    public CheckUpdateView()
    {
        InitializeComponent();

        ViewModel = new CheckUpdateViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.CheckUpdateModels, v => v.lstCheckUpdates.ItemsSource).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.EnableCheckPreReleaseUpdate, v => v.togEnableCheckPreReleaseUpdate.IsChecked).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CheckUpdateCmd, v => v.btnCheckUpdate).DisposeWith(disposables);
        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.DispatcherCheckUpdate:
                if (obj is null)
                    return false;
                Dispatcher.UIThread.Post(() =>
                    ViewModel?.UpdateViewResult((CheckUpdateModel)obj),
                DispatcherPriority.Default);
                break;

            case EViewAction.DispatcherCheckUpdateFinished:
                if (obj is null)
                    return false;
                Dispatcher.UIThread.Post(() =>
                    ViewModel?.UpdateFinishedResult((bool)obj),
                DispatcherPriority.Default);
                break;
        }

        return await Task.FromResult(true);
    }
}
