namespace v2rayN.Desktop.Base;

public class WindowBase<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class
{
    private bool _firstOpen = true; // 程序每次启动时窗口首次打开为 true

    public WindowBase()
    {
        // 在窗口显示后恢复大小，避免 Avalonia 12 中的初始跳变
        Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                OnWindowInitialized();

                // 程序每次启动时首次打开窗口居中
                if (_firstOpen)
                {
                    var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
                    var x = (screen.WorkingArea.Width - Width) / 2;
                    var y = (screen.WorkingArea.Height - Height) / 2;
                    Position = new PixelPoint((int)x, (int)y);
                    _firstOpen = false;
                }
            });
        };

        Loaded += (s, e) =>
        {
            if (Owner != null && !ShowInTaskbar)
            {
                CanMinimize = false;
            }
        };
    }

    private void OnWindowInitialized()
    {
        try
        {
            var sizeItem = ConfigHandler.GetWindowSizeItem(AppManager.Instance.Config, GetType().Name);
            if (sizeItem is null)
            {
                return;
            }

            if (sizeItem.Width > 0 && !Width.Equals(sizeItem.Width))
            {
                Width = sizeItem.Width;
            }

            if (sizeItem.Height > 0 && !Height.Equals(sizeItem.Height))
            {
                Height = sizeItem.Height;
            }
        }
        catch { }
    }

    protected virtual void OnLoaded(object? sender, RoutedEventArgs e)
    {
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try
        {
            ConfigHandler.SaveWindowSizeItem(
                AppManager.Instance.Config,
                GetType().Name,
                Bounds.Width,
                Bounds.Height
            );
        }
        catch { }
    }
}
