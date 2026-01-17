using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using Semi.Avalonia;

namespace v2rayN.Desktop.ViewModels;

public class ThemeSettingViewModel : MyReactiveObject
{
    [Reactive] public string CurrentTheme { get; set; }

    [Reactive] public int CurrentFontSize { get; set; }

    [Reactive] public string CurrentLanguage { get; set; }

    public ThemeSettingViewModel()
    {
        Config = AppManager.Instance.Config;

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
        CurrentTheme = Config.UiItem.CurrentTheme;
        CurrentFontSize = Config.UiItem.CurrentFontSize;
        CurrentLanguage = Config.UiItem.CurrentLanguage;

        this.WhenAnyValue(x => x.CurrentTheme)
            .Where(t => t != Config.UiItem.CurrentTheme)
            .Select(c => Observable.FromAsync(async () =>
            {
                Config.UiItem.CurrentTheme = c;
                ModifyTheme();
                await ConfigHandler.SaveConfig(Config);
            }))
            .Concat()
            .Subscribe();

        this.WhenAnyValue(x => x.CurrentFontSize)
            .Where(s => s >= AppConfig.MinFontSize && s != Config.UiItem.CurrentFontSize)
            .Select(c => Observable.FromAsync(async () =>
            {
                Config.UiItem.CurrentFontSize = c;
                ModifyFontSize();
                await ConfigHandler.SaveConfig(Config);
            }))
            .Concat()
            .Subscribe();

        this.WhenAnyValue(x => x.CurrentLanguage)
            .Where(l => l.IsNotEmpty() && l != Config.UiItem.CurrentLanguage)
            .Select(c => Observable.FromAsync(async () =>
            {
                Config.UiItem.CurrentLanguage = c;
                Thread.CurrentThread.CurrentUICulture = new(c);
                await ConfigHandler.SaveConfig(Config);
                NoticeManager.Instance.Enqueue(ResUI.NeedRebootTips);
            }))
            .Concat()
            .Subscribe();
    }

    private void ModifyTheme()
    {
        var app = Application.Current;
        app?.RequestedThemeVariant = CurrentTheme switch
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
        if (size < AppConfig.MinFontSize)
        {
            return;
        }

        Style style = new(x => Selectors.Or(
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
        style.Add(new Setter()
        {
            Property = TemplatedControl.FontSizeProperty,
            Value = size,
        });
        Application.Current?.Styles.Add(style);

        ModifyFontSizeEx(size);
    }

    private static void ModifyFontSizeEx(double size)
    {
        //DataGrid
        var rowHeight = 20 + (size / 2);
        var style = new Style(x => x.OfType<DataGrid>());
        style.Add(new Setter(DataGrid.RowHeightProperty, rowHeight));
        Application.Current?.Styles.Add(style);
    }

    private static void ModifyFontFamily()
    {
        var currentFontFamily = Config.UiItem.CurrentFontFamily;
        if (currentFontFamily.IsNullOrEmpty())
        {
            return;
        }

        try
        {
            Style style = new(x => Selectors.Or(
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
            style.Add(new Setter()
            {
                Property = TemplatedControl.FontFamilyProperty,
                Value = new FontFamily(currentFontFamily),
            });
            Application.Current?.Styles.Add(style);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ModifyFontFamily", ex);
        }
    }
}
