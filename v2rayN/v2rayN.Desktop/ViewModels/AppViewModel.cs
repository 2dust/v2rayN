using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using Splat;
using System.Reactive;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.ViewModels
{
    public class AppViewModel : MyReactiveObject
    {
        public ReactiveCommand<Unit, Unit> SystemProxyClearCmd { get; }
        public ReactiveCommand<Unit, Unit> SystemProxySetCmd { get; }
        public ReactiveCommand<Unit, Unit> SystemProxyNothingCmd { get; }
        public ReactiveCommand<Unit, Unit> AddServerViaClipboardCmd { get; }
        public ReactiveCommand<Unit, Unit> SubUpdateCmd { get; }
        public ReactiveCommand<Unit, Unit> SubUpdateViaProxyCmd { get; }
        public ReactiveCommand<Unit, Unit> ExitCmd { get; }

        public AppViewModel()
        {
            _config = AppHandler.Instance.Config;

            SystemProxyClearCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetListenerType(ESysProxyType.ForcedClear);
            });
            SystemProxySetCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetListenerType(ESysProxyType.ForcedChange);
            });
            SystemProxyNothingCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetListenerType(ESysProxyType.Unchanged);
            });

            AddServerViaClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await AddServerViaClipboard();
            });

            SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await UpdateSubscriptionProcess(false);
            });
            SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await UpdateSubscriptionProcess(true);
            });

            ExitCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await Exit();
            });
        }

        private async Task SetListenerType(ESysProxyType type)
        {
            if (_config.systemProxyItem.sysProxyType == type)
            {
                return;
            }

            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.SetListenerType(type);
        }

        private async Task AddServerViaClipboard()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    var clipboardData = await AvaUtils.GetClipboardData(desktop.MainWindow);
                    var service = Locator.Current.GetService<MainWindowViewModel>();
                    if (service != null) await service.AddServerViaClipboardAsync(clipboardData);
                }
            }
        }

        private async Task UpdateSubscriptionProcess(bool blProxy)
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.UpdateSubscriptionProcess("", blProxy);
        }

        private async Task Exit()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var service = Locator.Current.GetService<MainWindowViewModel>();
                if (service != null) await service.MyAppExitAsync(false);

                desktop.Shutdown();
            }
        }
    }
}