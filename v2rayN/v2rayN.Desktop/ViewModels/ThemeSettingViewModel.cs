using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Semi.Avalonia;

namespace v2rayN.Desktop.ViewModels
{
    public class ThemeSettingViewModel : MyReactiveObject
    {
        [Reactive] public string CurrentTheme { get; set; }

        [Reactive] public int CurrentFontSize { get; set; }

        [Reactive] public string CurrentLanguage { get; set; }

        public ThemeSettingViewModel()
        {
            _config = AppHandler.Instance.Config;

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

            this.WhenAnyValue(x => x.CurrentTheme)
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
                    x => x.CurrentFontSize,
                    y => y > 0)
                .Subscribe(c =>
                {
                    if (_config.UiItem.CurrentFontSize != CurrentFontSize && CurrentFontSize >= Global.MinFontSize)
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
                    if (Utils.IsNotEmpty(CurrentLanguage) && _config.UiItem.CurrentLanguage != CurrentLanguage)
                    {
                        _config.UiItem.CurrentLanguage = CurrentLanguage;
                        Thread.CurrentThread.CurrentUICulture = new(CurrentLanguage);
                        ConfigHandler.SaveConfig(_config);
                        NoticeHandler.Instance.Enqueue(ResUI.NeedRebootTips);
                    }
                });
        }

        private void ModifyTheme()
        {
            var app = Application.Current;
            if (app is not null)
            {
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
        }

        private void ModifyFontSize()
        {
            double size = CurrentFontSize;
            if (size < Global.MinFontSize)
                return;

            Style style = new(x => Selectors.Or(
                x.OfType<Button>(),
                x.OfType<TextBox>(),
                x.OfType<TextBlock>(),
                x.OfType<Menu>(),
                x.OfType<ContextMenu>(),
                x.OfType<DataGridRow>(),
                x.OfType<ListBoxItem>(),
                x.OfType<HeaderedContentControl>()
            ));
            style.Add(new Setter()
            {
                Property = TemplatedControl.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(style);

            ModifyFontSizeEx(size);
        }

        private void ModifyFontSizeEx(double size)
        {
            //DataGrid
            var rowHeight = 20 + (size / 2);
            var style = new Style(x => x.OfType<DataGrid>());
            style.Add(new Setter(DataGrid.RowHeightProperty, rowHeight));
            Application.Current?.Styles.Add(style);
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
                Style style = new(x => Selectors.Or(
                    x.OfType<Button>(),
                    x.OfType<TextBox>(),
                    x.OfType<TextBlock>(),
                    x.OfType<Menu>(),
                    x.OfType<ContextMenu>(),
                    x.OfType<DataGridRow>(),
                    x.OfType<ListBoxItem>(),
                    x.OfType<HeaderedContentControl>(),
                    x.OfType<WindowNotificationManager>()
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
}
