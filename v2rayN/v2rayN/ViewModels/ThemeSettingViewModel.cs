using DynamicData;
using DynamicData.Binding;
using MaterialDesignColors;
using MaterialDesignColors.ColorManipulation;
using MaterialDesignThemes.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using v2rayN.Base;
using v2rayN.Handler;

namespace v2rayN.ViewModels
{
    public class ThemeSettingViewModel : MyReactiveObject
    {
        private readonly PaletteHelper _paletteHelper = new();

        [Reactive]
        public bool ColorModeDark { get; set; }

        private IObservableCollection<Swatch> _swatches = new ObservableCollectionExtended<Swatch>();
        public IObservableCollection<Swatch> Swatches => _swatches;

        [Reactive]
        public Swatch SelectedSwatch { get; set; }

        [Reactive]
        public int CurrentFontSize { get; set; }

        [Reactive]
        public bool FollowSystemTheme { get; set; }

        [Reactive]
        public string CurrentLanguage { get; set; }

        public ThemeSettingViewModel()
        {
            _config = LazyConfig.Instance.Config;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            RegisterSystemColorSet(_config, Application.Current.MainWindow, (bool bl) => { ModifyTheme(bl); });

            BindingUI();
            RestoreUI();
        }

        private void RestoreUI()
        {
            if (FollowSystemTheme)
            {
                ModifyTheme(!WindowsUtils.IsLightTheme());
            }
            else
            {
                ModifyTheme(_config.uiItem.colorModeDark);
            }

            if (!_config.uiItem.colorPrimaryName.IsNullOrEmpty())
            {
                var swatch = new SwatchesProvider().Swatches.FirstOrDefault(t => t.Name == _config.uiItem.colorPrimaryName);
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
            ColorModeDark = _config.uiItem.colorModeDark;
            FollowSystemTheme = _config.uiItem.followSystemTheme;
            _swatches.AddRange(new SwatchesProvider().Swatches);
            if (!_config.uiItem.colorPrimaryName.IsNullOrEmpty())
            {
                SelectedSwatch = _swatches.FirstOrDefault(t => t.Name == _config.uiItem.colorPrimaryName);
            }
            CurrentFontSize = _config.uiItem.currentFontSize;
            CurrentLanguage = _config.uiItem.currentLanguage;

            this.WhenAnyValue(
                  x => x.ColorModeDark,
                  y => y == true)
                      .Subscribe(c =>
                      {
                          if (_config.uiItem.colorModeDark != ColorModeDark)
                          {
                              _config.uiItem.colorModeDark = ColorModeDark;
                              ModifyTheme(ColorModeDark);
                              ConfigHandler.SaveConfig(_config);
                          }
                      });

            this.WhenAnyValue(x => x.FollowSystemTheme,
                y => y == true)
                    .Subscribe(c =>
                    {
                        if (_config.uiItem.followSystemTheme != FollowSystemTheme)
                        {
                            _config.uiItem.followSystemTheme = FollowSystemTheme;
                            ConfigHandler.SaveConfig(_config);
                            if (FollowSystemTheme)
                            {
                                ModifyTheme(!WindowsUtils.IsLightTheme());
                            }
                            else
                            {
                                ModifyTheme(ColorModeDark);
                            }
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
                     if (_config.uiItem.colorPrimaryName != SelectedSwatch?.Name)
                     {
                         _config.uiItem.colorPrimaryName = SelectedSwatch?.Name;
                         ChangePrimaryColor(SelectedSwatch.ExemplarHue.Color);
                         ConfigHandler.SaveConfig(_config);
                     }
                 });

            this.WhenAnyValue(
               x => x.CurrentFontSize,
               y => y > 0)
                  .Subscribe(c =>
                  {
                      if (CurrentFontSize >= Global.MinFontSize)
                      {
                          _config.uiItem.currentFontSize = CurrentFontSize;
                          double size = (long)CurrentFontSize;
                          Application.Current.Resources["StdFontSize"] = size;
                          Application.Current.Resources["StdFontSize1"] = size + 1;
                          Application.Current.Resources["StdFontSize2"] = size + 2;
                          Application.Current.Resources["StdFontSizeMsg"] = size - 1;
                          Application.Current.Resources["StdFontSize-1"] = size - 1;

                          ConfigHandler.SaveConfig(_config);
                      }
                  });

            this.WhenAnyValue(
             x => x.CurrentLanguage,
             y => y != null && !y.IsNullOrEmpty())
                .Subscribe(c =>
                {
                    if (!Utils.IsNullOrEmpty(CurrentLanguage) && _config.uiItem.currentLanguage != CurrentLanguage)
                    {
                        _config.uiItem.currentLanguage = CurrentLanguage;
                        Thread.CurrentThread.CurrentUICulture = new(CurrentLanguage);
                        ConfigHandler.SaveConfig(_config);
                        _noticeHandler?.Enqueue(ResUI.NeedRebootTips);
                    }
                });
        }

        public void ModifyTheme(bool isDarkTheme)
        {
            var theme = _paletteHelper.GetTheme();

            theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(theme);
            WindowsUtils.SetDarkBorder(Application.Current.MainWindow, isDarkTheme);
        }

        public void ChangePrimaryColor(System.Windows.Media.Color color)
        {
            var theme = _paletteHelper.GetTheme();

            theme.PrimaryLight = new ColorPair(color.Lighten());
            theme.PrimaryMid = new ColorPair(color);
            theme.PrimaryDark = new ColorPair(color.Darken());

            _paletteHelper.SetTheme(theme);
        }

        public void RegisterSystemColorSet(Config config, Window window, Action<bool> update)
        {
            var helper = new WindowInteropHelper(window);
            var hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
            hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (config.uiItem.followSystemTheme)
                {
                    const int WM_SETTINGCHANGE = 0x001A;
                    if (msg == WM_SETTINGCHANGE)
                    {
                        if (wParam == IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
                        {
                            update(!WindowsUtils.IsLightTheme());
                        }
                    }
                }

                return IntPtr.Zero;
            });
        }
    }
}