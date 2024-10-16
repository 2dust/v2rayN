using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class StatusBarView : ReactiveUserControl<StatusBarViewModel>
    {
        private static Config _config;

        public StatusBarView()
        {
            InitializeComponent();

            _config = AppHandler.Instance.Config;
            //ViewModel = new StatusBarViewModel(UpdateViewHandler);
            //Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(StatusBarViewModel));
            ViewModel = Locator.Current.GetService<StatusBarViewModel>();
            ViewModel?.Init(UpdateViewHandler);

            txtRunningServerDisplay.Tapped += TxtRunningServerDisplay_Tapped;
            txtRunningInfoDisplay.Tapped += TxtRunningServerDisplay_Tapped;

            this.WhenActivated(disposables =>
            {
                //status bar
                this.OneWayBind(ViewModel, vm => vm.InboundDisplay, v => v.txtInboundDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.InboundLanDisplay, v => v.txtInboundLanDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningServerDisplay, v => v.txtRunningServerDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.RunningInfoDisplay, v => v.txtRunningInfoDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.SpeedProxyDisplay, v => v.txtSpeedProxyDisplay.Text).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.SpeedDirectDisplay, v => v.txtSpeedDirectDisplay.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.EnableTun, v => v.togEnableTun.IsChecked).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SystemProxySelected, v => v.cmbSystemProxy.SelectedIndex).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings2.SelectedItem).DisposeWith(disposables);
            });

            spEnableTun.IsVisible = (Utils.IsWindows() || AppHandler.Instance.IsAdministrator);
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.DispatcherServerAvailability:
                    if (obj is null) return false;
                    Dispatcher.UIThread.Post(() =>
                        ViewModel?.TestServerAvailabilityResult((string)obj),
                    DispatcherPriority.Default);
                    break;

                case EViewAction.DispatcherRefreshServersBiz:
                    Dispatcher.UIThread.Post(() =>
                        ViewModel?.RefreshServersBiz(),
                    DispatcherPriority.Default);
                    break;

                case EViewAction.DispatcherRefreshIcon:
                    Dispatcher.UIThread.Post(() =>
                    {
                        RefreshIcon();
                    },
                    DispatcherPriority.Default);
                    break;
            }
            return await Task.FromResult(true);
        }

        private void RefreshIcon()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow.Icon = AvaUtils.GetAppIcon(_config.systemProxyItem.sysProxyType);
            }
        }

        private void TxtRunningServerDisplay_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            ViewModel?.TestServerAvailability();
        }
    }
}