namespace v2rayN.Base;

public class WindowBase<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class
{
    public WindowBase()
    {
        Loaded += OnLoaded;
    }

    protected virtual void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var sizeItem = ConfigHandler.GetWindowSizeItem(AppManager.Instance.Config, GetType().Name);
            if (sizeItem == null)
            {
                return;
            }

            Width = Math.Min(sizeItem.Width, SystemParameters.WorkArea.Width);
            Height = Math.Min(sizeItem.Height, SystemParameters.WorkArea.Height);

            if (sizeItem.Left != null && sizeItem.Top != null && IsOnScreen(sizeItem.Left.Value, sizeItem.Top.Value))
            {
                Left = sizeItem.Left.Value;
                Top = sizeItem.Top.Value;
            }
            else
            {
                Left = SystemParameters.WorkArea.Left + ((SystemParameters.WorkArea.Width - Width) / 2);
                Top = SystemParameters.WorkArea.Top + ((SystemParameters.WorkArea.Height - Height) / 2);
            }
        }
        catch { }
    }

    private static bool IsOnScreen(double left, double top)
    {
        return left >= SystemParameters.VirtualScreenLeft
            && top >= SystemParameters.VirtualScreenTop
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try
        {
            if (WindowState == WindowState.Normal)
            {
                ConfigHandler.SaveWindowSizeItem(AppManager.Instance.Config, GetType().Name, Width, Height, Left, Top);
            }
        }
        catch { }
    }
}
