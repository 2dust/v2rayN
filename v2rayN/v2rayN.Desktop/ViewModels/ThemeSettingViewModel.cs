using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using Semi.Avalonia;

namespace v2rayN.Desktop.ViewModels;

public partial class ThemeSettingViewModel : MyReactiveObject
{
    private Style? _fontSizeStyle;
    private Style? _fontSizeExStyle;
    private Style? _fontFamilyStyle;

    [Reactive] public partial string CurrentTheme { get; set; }

    [Reactive] public partial int CurrentFontSize { get; set; }

    [Reactive] public partial string CurrentLanguage { get; set; }

    public ThemeSettingViewModel()
    {
        _config = AppManager.Instance.Config;

        BindingUI();
        RestoreUI();
    }

    private void RestoreUI()
    {
        ModifyTheme();
        ModifyFontFamily();
        ModifyFontSize();
    }

    private void BindingUI()
    {
        CurrentTheme = _config.UiItem.CurrentTheme;
        CurrentFontSize = _config.UiItem.CurrentFontSize;
        CurrentLanguage = _config.UiItem.CurrentLanguage;

        this.WhenAnyValueUnsafe(x => x.CurrentTheme)
            .Where(y => y.IsNotEmpty())
            .SubscribeAsync(async _ =>
            {
                if (_config.UiItem.CurrentTheme != CurrentTheme)
                {
                    _config.UiItem.CurrentTheme = CurrentTheme;
                    ModifyTheme();
                    await ConfigHandler.SaveConfig(_config);
                }
            });

        this.WhenAnyValueUnsafe(x => x.CurrentFontSize)
            .Where(y => y > 0)
            .SubscribeAsync(async _ =>
            {
                if (_config.UiItem.CurrentFontSize != CurrentFontSize && CurrentFontSize >= Global.MinFontSize)
                {
                    _config.UiItem.CurrentFontSize = CurrentFontSize;
                    ModifyFontSize();
                    await ConfigHandler.SaveConfig(_config);
                }
            });

        this.WhenAnyValueUnsafe(x => x.CurrentLanguage)
            .Where(y => !y.IsNullOrEmpty())
            .SubscribeAsync(async _ =>
            {
                if (CurrentLanguage.IsNotEmpty() && _config.UiItem.CurrentLanguage != CurrentLanguage)
                {
                    _config.UiItem.CurrentLanguage = CurrentLanguage;
                    Thread.CurrentThread.CurrentUICulture = new(CurrentLanguage);
                    await ConfigHandler.SaveConfig(_config);
                    NoticeManager.Instance.Enqueue(ResUI.NeedRebootTips);
                }
            });
    }

    private void ModifyTheme()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }
        app.RequestedThemeVariant = CurrentTheme switch
        {
            nameof(ETheme.Dark) => ThemeVariant.Dark,
            nameof(ETheme.Light) => ThemeVariant.Light,
            nameof(ETheme.Aquatic) => SemiTheme.Aquatic,
            nameof(ETheme.Desert) => SemiTheme.Desert,
            nameof(ETheme.Dusk) => SemiTheme.Dusk,
            nameof(ETheme.NightSky) => SemiTheme.NightSky,
            _ => ThemeVariant.Default,
        };
    }

    private void ModifyFontSize()
    {
        double size = CurrentFontSize;
        if (size < Global.MinFontSize)
        {
            return;
        }

        var style = new Style(x => Selectors.Or(
            x.OfType<Button>(),
            x.OfType<TextBox>(),
            x.OfType<TextBlock>(),
            x.OfType<SelectableTextBlock>(),
            x.OfType<Menu>(),
            x.OfType<ContextMenu>(),
            x.OfType<DataGridRow>(),
            x.OfType<ListBoxItem>(),
            x.OfType<HeaderedContentControl>(),
            x.OfType<TextEditor>()
        ));
        style.Add(new Setter
        {
            Property = TemplatedControl.FontSizeProperty,
            Value = size,
        });
        ReplaceStyle(ref _fontSizeStyle, style);

        ModifyFontSizeEx(size);
    }

    private void ModifyFontSizeEx(double size)
    {
        //DataGrid
        var rowHeight = 20 + (size / 2);

        var style = new Style(x => x.OfType<DataGrid>());
        style.Add(new Setter(DataGrid.RowHeightProperty, rowHeight));
        ReplaceStyle(ref _fontSizeExStyle, style);
    }

    private void ModifyFontFamily()
    {
        var currentFontFamily = _config.UiItem.CurrentFontFamily;
        if (currentFontFamily.IsNullOrEmpty())
        {
            return;
        }

        try
        {
            var style = new Style(x => Selectors.Or(
                x.OfType<Button>(),
                x.OfType<TextBox>(),
                x.OfType<TextBlock>(),
                x.OfType<SelectableTextBlock>(),
                x.OfType<Menu>(),
                x.OfType<ContextMenu>(),
                x.OfType<DataGridRow>(),
                x.OfType<ListBoxItem>(),
                x.OfType<HeaderedContentControl>(),
                x.OfType<WindowNotificationManager>(),
                x.OfType<TextEditor>()
            ));
            style.Add(new Setter
            {
                Property = TemplatedControl.FontFamilyProperty,
                Value = new FontFamily(currentFontFamily),
            });
            ReplaceStyle(ref _fontFamilyStyle, style);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ModifyFontFamily", ex);
        }
    }

    private static void ReplaceStyle(ref Style? oldStyle, Style newStyle)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (oldStyle is not null)
        {
            app.Styles.Remove(oldStyle);
        }

        oldStyle = newStyle;
        app.Styles.Add(newStyle);
    }
}
