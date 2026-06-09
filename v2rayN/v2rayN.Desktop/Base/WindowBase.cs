namespace v2rayN.Desktop.Base;

public class WindowBase<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class
{
    public WindowBase()
    {
        Initialized += OnWindowInitialized;
        Loaded += OnLoaded;
        Loaded += (s, e) =>
        {
            if (Owner != null && !ShowInTaskbar)
            {
                CanMinimize = false;
                CanMaximize = false;
            }
        };
    }

    private void ReactiveWindowBase_Closed(object? sender, EventArgs e)
    {
        throw new NotImplementedException();
    }

    private void OnWindowInitialized(object? sender, EventArgs e)
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
            ConfigHandler.SaveWindowSizeItem(AppManager.Instance.Config, GetType().Name, Width, Height);
        }
        catch { }
    }
}
