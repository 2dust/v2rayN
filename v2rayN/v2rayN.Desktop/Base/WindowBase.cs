namespace v2rayN.Desktop.Base;

public class WindowBase<TViewModel> : ReactiveWindow<TViewModel> where TViewModel : class
{
    private Border? _linuxTitleBar;
    private Control? _linuxTitleBarDragRegion;
    private Button? _linuxTitleBarCloseButton;

    public WindowBase()
    {
        if (Utils.IsLinux())
        {
            SystemDecorations = SystemDecorations.BorderOnly;
        }

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
            var scaling = (Utils.IsMacOS() ? null : VisualRoot?.RenderScaling) ?? 1.0;

            var x = workingArea.X + ((workingArea.Width - (Width * scaling)) / 2);
            var y = workingArea.Y + ((workingArea.Height - (Height * scaling)) / 2);

            Position = new PixelPoint((int)x, (int)y);
        }
        catch { }

        ConfigureLinuxTitleBar();
    }

    protected override void OnClosed(EventArgs e)
    {
        ReleaseLinuxTitleBar();
        base.OnClosed(e);
        try
        {
            ConfigHandler.SaveWindowSizeItem(AppManager.Instance.Config, GetType().Name, Width, Height);
        }
        catch { }
    }

    protected virtual void HandleLinuxTitleBarClose()
    {
        Close();
    }

    private void ConfigureLinuxTitleBar()
    {
        if (!Utils.IsLinux())
        {
            return;
        }

        _linuxTitleBar ??= this.FindControl<Border>("linuxTitleBar");
        if (_linuxTitleBar == null)
        {
            return;
        }

        _linuxTitleBar.IsVisible = true;

        _linuxTitleBarDragRegion ??= this.FindControl<Control>("linuxTitleBarDragRegion");
        if (_linuxTitleBarDragRegion != null)
        {
            _linuxTitleBarDragRegion.PointerPressed -= LinuxTitleBar_PointerPressed;
            _linuxTitleBarDragRegion.PointerPressed += LinuxTitleBar_PointerPressed;
        }

        _linuxTitleBarCloseButton ??= this.FindControl<Button>("btnLinuxClose");
        if (_linuxTitleBarCloseButton != null)
        {
            _linuxTitleBarCloseButton.Click -= LinuxTitleBarClose_Click;
            _linuxTitleBarCloseButton.Click += LinuxTitleBarClose_Click;
        }
    }

    private void ReleaseLinuxTitleBar()
    {
        if (_linuxTitleBarDragRegion != null)
        {
            _linuxTitleBarDragRegion.PointerPressed -= LinuxTitleBar_PointerPressed;
        }

        if (_linuxTitleBarCloseButton != null)
        {
            _linuxTitleBarCloseButton.Click -= LinuxTitleBarClose_Click;
        }
    }

    private void LinuxTitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed == false)
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private void LinuxTitleBarClose_Click(object? sender, RoutedEventArgs e)
    {
        HandleLinuxTitleBarClose();
    }
}
