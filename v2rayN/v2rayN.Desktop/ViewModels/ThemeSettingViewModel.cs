using Semi.Avalonia;

namespace v2rayN.Desktop.ViewModels;

public partial class ThemeSettingViewModel : MyReactiveObject
{
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

        // Set Default Value
        ModifyFontSizeEx(12);

        this.WhenAnyValue(x => x.CurrentTheme)
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

        this.WhenAnyValue(x => x.CurrentFontSize)
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

        this.WhenAnyValue(x => x.CurrentLanguage)
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
        Application.Current!.Resources["AppFontSize"] = size;
        ModifyFontSizeEx(size);
    }

    private void ModifyFontSizeEx(double size)
    {
        //DataGrid
        var rowHeight = 20 + (size / 2);
        Application.Current!.Resources["DataGridRowHeight"] = rowHeight;
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
            Application.Current!.Resources["AppFontFamily"] = currentFontFamily;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ModifyFontFamily", ex);
        }
    }
}
