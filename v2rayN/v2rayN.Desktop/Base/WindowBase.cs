using Avalonia;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;

namespace v2rayN.Desktop.Base;

public class WindowBase<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class
{
    public WindowBase()
    {
        Loaded += OnLoaded;
    }

    private void ReactiveWindowBase_Closed(object? sender, EventArgs e)
    {
        throw new NotImplementedException();
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

            Width = sizeItem.Width;
            Height = sizeItem.Height;

            var workingArea = (Screens.ScreenFromWindow(this) ?? Screens.Primary).WorkingArea;
            var scaling = (Utils.IsOSX() ? null : VisualRoot?.RenderScaling) ?? 1.0;

            var x = workingArea.X + ((workingArea.Width - (Width * scaling)) / 2);
            var y = workingArea.Y + ((workingArea.Height - (Height * scaling)) / 2);

            Position = new PixelPoint((int)x, (int)y);
        }
        catch { }
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
