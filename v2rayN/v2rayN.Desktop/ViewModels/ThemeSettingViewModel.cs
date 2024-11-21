using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive.Linq;

namespace v2rayN.Desktop.ViewModels
{
    public class ThemeSettingViewModel : MyReactiveObject
    {
        [Reactive] public string ThemeMode { get; set; }

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
            ModifyTheme(_config.UiItem.ThemeMode);
            ModifyFontFamily();
        }

        private void BindingUI()
        {
            ThemeMode = _config.UiItem.ThemeMode;
            CurrentFontSize = _config.UiItem.CurrentFontSize;
            CurrentLanguage = _config.UiItem.CurrentLanguage;

            this.WhenAnyValue(x => x.ThemeMode)
                .Subscribe(c =>
                {
                    if (_config.UiItem.ThemeMode != ThemeMode)
                    {
                        _config.UiItem.ThemeMode = ThemeMode;
                        ModifyTheme(ThemeMode);
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
                        _config.UiItem.CurrentFontSize = CurrentFontSize;
                        double size = CurrentFontSize;
                        ModifyFontSize(size);

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

        private void ModifyTheme(string themeMode)
        {
            var app = Application.Current;
            if (app is not null)
            {
                ThemeVariant tv = ThemeVariant.Default;
                switch (themeMode)
                {
                    case "dark": tv = ThemeVariant.Dark; break;
                    case "light": tv = ThemeVariant.Light; break;
                    default: tv = ThemeVariant.Default; break;
                }
                app.RequestedThemeVariant = tv;
            }
        }

        private void ModifyFontSize(double size)
        {
            Style style = new(x => Selectors.Or(
                x.OfType<Button>(),
                x.OfType<TextBox>(),
                x.OfType<TextBlock>(),
                x.OfType<Menu>(),
                x.OfType<ContextMenu>(),
                x.OfType<DataGridRow>(),
                x.OfType<ListBoxItem>()
            ));
            style.Add(new Setter()
            {
                Property = TemplatedControl.FontSizeProperty,
                Value = size,
            });
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