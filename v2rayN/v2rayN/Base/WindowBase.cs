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
            var sizeItem = ConfigTextManager.Instance.GetWindowSizeItem(GetType().Name);
            if (sizeItem == null)
            {
                return;
            }

            Width = Math.Min(sizeItem.Width, SystemParameters.WorkArea.Width);
            Height = Math.Min(sizeItem.Height, SystemParameters.WorkArea.Height);

            Left = SystemParameters.WorkArea.Left + ((SystemParameters.WorkArea.Width - Width) / 2);
            Top = SystemParameters.WorkArea.Top + ((SystemParameters.WorkArea.Height - Height) / 2);
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try
        {
            ConfigTextManager.Instance.SaveWindowSizeItem(GetType().Name, Width, Height);
        }
        catch { }
    }
}
