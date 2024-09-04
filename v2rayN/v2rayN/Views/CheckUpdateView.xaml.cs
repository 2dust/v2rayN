using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Threading;

namespace v2rayN.Views
{
    public partial class CheckUpdateView
    {
        public CheckUpdateView()
        {
            InitializeComponent();

            ViewModel = new CheckUpdateViewModel(UpdateViewHandler);

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.CheckUpdateItems, v => v.lstCheckUpdates.ItemsSource).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.EnableCheckPreReleaseUpdate, v => v.togEnableCheckPreReleaseUpdate.IsChecked).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.CheckUpdateCmd, v => v.btnCheckUpdate).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.IsCheckUpdate, v => v.btnCheckUpdate.IsEnabled).DisposeWith(disposables);
            });
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.DispatcherCheckUpdate:
                    if (obj is null) return false;
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.UpdateViewResult((CheckUpdateItem)obj);
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.DispatcherCheckUpdateFinished:
                    if (obj is null) return false;
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.UpdateFinishedResult((bool)obj);
                    }), DispatcherPriority.Normal);
                    break;
            }

            return await Task.FromResult(true);
        }
    }
}