namespace v2rayN.Desktop.Base;

public class WindowBase<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class
{
    private bool _firstOpen = true;

    public WindowBase()
    {
        Loaded += OnLoaded;

        Loaded += (s, e) =>
        {
            if (Owner != null && !ShowInTaskbar)
            {
                CanMinimize = false;
            }
        };
    }

    protected virtual void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var sizeItem = ConfigHandler.GetWindowSizeItem(AppManager.Instance.Config, GetType().Name);
            if (sizeItem != null)
            {
                if (sizeItem.Width > 0)
                    Width = sizeItem.Width;

                if (sizeItem.Height > 0)
                    Height = sizeItem.Height;
            }

            if (_firstOpen)
            {
                var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
                if (screen != null)
                {
                    var x = (screen.WorkingArea.Width - Width) / 2;
                    var y = (screen.WorkingArea.Height - Height) / 2;
                    Position = new PixelPoint((int)x, (int)y);
                }

                _firstOpen = false;
            }
        }
        catch { }
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
