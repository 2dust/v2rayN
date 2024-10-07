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
        public ReactiveCommand<Unit, Unit> AddServerViaClipboardCmd { get; }
        public ReactiveCommand<Unit, Unit> SubUpdateCmd { get; }
        public ReactiveCommand<Unit, Unit> SubUpdateViaProxyCmd { get; }
        public ReactiveCommand<Unit, Unit> ExitCmd { get; }

        public AppViewModel()
        {
            _config = AppHandler.Instance.Config;

            AddServerViaClipboardCmd = ReactiveCommand.Create(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var clipboardData = AvaUtils.GetClipboardData(desktop.MainWindow).Result;
                    Locator.Current.GetService<MainWindowViewModel>()?.AddServerViaClipboardAsync(clipboardData);
                }
            });

            SubUpdateCmd = ReactiveCommand.Create(() =>
            {
                Locator.Current.GetService<MainWindowViewModel>()?.UpdateSubscriptionProcess("", false);
            });
            SubUpdateViaProxyCmd = ReactiveCommand.Create(() =>
            {
                Locator.Current.GetService<MainWindowViewModel>()?.UpdateSubscriptionProcess("", true);
            });

            ExitCmd = ReactiveCommand.Create(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    Locator.Current.GetService<MainWindowViewModel>()?.MyAppExitAsync(false);

                    desktop.Shutdown();
                }
            });
        }
    }
}