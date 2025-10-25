using MaterialDesignColors;
using MaterialDesignColors.ColorManipulation;
using MaterialDesignThemes.Wpf;

namespace v2rayN.ViewModels;

public partial class ThemeSettingViewModel : MyReactiveObject
{
    private readonly PaletteHelper _paletteHelper = new();

    public IObservableCollection<Swatch> Swatches { get; } = new ObservableCollectionExtended<Swatch>();

    [Reactive]
    private Swatch _selectedSwatch;

    [Reactive] private string _currentTheme;

    [Reactive] private int _currentFontSize;

    [Reactive] private string _currentLanguage;

    public ThemeSettingViewModel()
    {
        _config = AppManager.Instance.Config;

        RegisterSystemColorSet(_config, Application.Current.MainWindow, ModifyTheme);

        BindingUI();
        RestoreUI();
    }

    private void RestoreUI()
    {
        ModifyTheme();
        ModifyFontSize();
        if (!_config.UiItem.ColorPrimaryName.IsNullOrEmpty())
        {
            var swatch = new SwatchesProvider().Swatches.FirstOrDefault(t => t.Name == _config.UiItem.ColorPrimaryName);
            if (swatch != null
               && swatch.ExemplarHue != null
               && swatch.ExemplarHue?.Color != null)
            {
                ChangePrimaryColor(swatch.ExemplarHue.Color);
            }
        }
    }

    private void BindingUI()
    {
        Swatches.AddRange(new SwatchesProvider().Swatches);
        if (!_config.UiItem.ColorPrimaryName.IsNullOrEmpty())
        {
            SelectedSwatch = Swatches.FirstOrDefault(t => t.Name == _config.UiItem.ColorPrimaryName);
        }
        CurrentTheme = _config.UiItem.CurrentTheme;
        CurrentFontSize = _config.UiItem.CurrentFontSize;
        CurrentLanguage = _config.UiItem.CurrentLanguage;

        this.WhenAnyValue(
                x => x.CurrentTheme,
                y => y != null && !y.IsNullOrEmpty())
            .Subscribe(c =>
             {
                 if (_config.UiItem.CurrentTheme != CurrentTheme)
                 {
                     _config.UiItem.CurrentTheme = CurrentTheme;
                     ModifyTheme();
                     ConfigHandler.SaveConfig(_config);
                 }
             });

        this.WhenAnyValue(
          x => x.SelectedSwatch,
          y => y != null && !y.Name.IsNullOrEmpty())
             .Subscribe(c =>
             {
                 if (SelectedSwatch == null
                 || SelectedSwatch.Name.IsNullOrEmpty()
                 || SelectedSwatch.ExemplarHue == null
                 || SelectedSwatch.ExemplarHue?.Color == null)
                 {
                     return;
                 }
                 if (_config.UiItem.ColorPrimaryName != SelectedSwatch?.Name)
                 {
                     _config.UiItem.ColorPrimaryName = SelectedSwatch?.Name;
                     ChangePrimaryColor(SelectedSwatch.ExemplarHue.Color);
                     ConfigHandler.SaveConfig(_config);
                 }
             });

        this.WhenAnyValue(
           x => x.CurrentFontSize,
           y => y > 0)
              .Subscribe(c =>
              {
                  if (_config.UiItem.CurrentFontSize != CurrentFontSize)
                  {
                      _config.UiItem.CurrentFontSize = CurrentFontSize;
                      ModifyFontSize();
                      ConfigHandler.SaveConfig(_config);
                  }
              });

        this.WhenAnyValue(
         x => x.CurrentLanguage,
         y => y != null && !y.IsNullOrEmpty())
            .Subscribe(c =>
            {
                if (CurrentLanguage.IsNotEmpty() && _config.UiItem.CurrentLanguage != CurrentLanguage)
                {
                    _config.UiItem.CurrentLanguage = CurrentLanguage;
                    Thread.CurrentThread.CurrentUICulture = new(CurrentLanguage);
                    ConfigHandler.SaveConfig(_config);
                    NoticeManager.Instance.Enqueue(ResUI.NeedRebootTips);
                }
            });
    }

    public void ModifyTheme()
    {
        var baseTheme = CurrentTheme switch
        {
            nameof(ETheme.Dark) => BaseTheme.Dark,
            nameof(ETheme.Light) => BaseTheme.Light,
            _ => BaseTheme.Inherit,
        };

        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(baseTheme);
        _paletteHelper.SetTheme(theme);

        WindowsUtils.SetDarkBorder(Application.Current.MainWindow, CurrentTheme);
    }

    private void ModifyFontSize()
    {
        double size = (long)CurrentFontSize;
        if (size < Global.MinFontSize)
            return;

        Application.Current.Resources["StdFontSize"] = size;
        Application.Current.Resources["StdFontSize1"] = size + 1;
        Application.Current.Resources["StdFontSize-1"] = size - 1;
    }

    public void ChangePrimaryColor(System.Windows.Media.Color color)
    {
        var theme = _paletteHelper.GetTheme();

        theme.PrimaryLight = new ColorPair(color.Lighten());
        theme.PrimaryMid = new ColorPair(color);
        theme.PrimaryDark = new ColorPair(color.Darken());

        _paletteHelper.SetTheme(theme);
    }

    public void RegisterSystemColorSet(Config config, Window window, Action updateFunc)
    {
        var helper = new WindowInteropHelper(window);
        var hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
        hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (config.UiItem.CurrentTheme == nameof(ETheme.FollowSystem))
            {
                const int WM_SETTINGCHANGE = 0x001A;
                if (msg == WM_SETTINGCHANGE)
                {
                    if (wParam == IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
                    {
                        updateFunc?.Invoke();
                    }
                }
            }

            return IntPtr.Zero;
        });
    }
}
