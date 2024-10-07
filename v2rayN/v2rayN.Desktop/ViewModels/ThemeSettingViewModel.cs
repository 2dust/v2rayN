using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive.Linq;

namespace v2rayN.Desktop.ViewModels
{
    public class ThemeSettingViewModel : MyReactiveObject
    {
        [Reactive]
        public bool ColorModeDark { get; set; }

        [Reactive]
        public int CurrentFontSize { get; set; }

        [Reactive]
        public string CurrentLanguage { get; set; }

        public ThemeSettingViewModel()
        {
            _config = AppHandler.Instance.Config;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();

            BindingUI();
            RestoreUI();
        }

        private void RestoreUI()
        {
            ModifyTheme(_config.uiItem.colorModeDark);
        }

        private void BindingUI()
        {
            ColorModeDark = _config.uiItem.colorModeDark;
            CurrentFontSize = _config.uiItem.currentFontSize;
            CurrentLanguage = _config.uiItem.currentLanguage;

            this.WhenAnyValue(x => x.ColorModeDark)
                      .Subscribe(c =>
                      {
                          if (_config.uiItem.colorModeDark != ColorModeDark)
                          {
                              _config.uiItem.colorModeDark = ColorModeDark;
                              ModifyTheme(ColorModeDark);
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
                    if (Utils.IsNotEmpty(CurrentLanguage) && _config.uiItem.currentLanguage != CurrentLanguage)
                    {
                        _config.uiItem.currentLanguage = CurrentLanguage;
                        Thread.CurrentThread.CurrentUICulture = new(CurrentLanguage);
                        ConfigHandler.SaveConfig(_config);
                        _noticeHandler?.Enqueue(ResUI.NeedRebootTips);
                    }
                });
        }

        private void ModifyTheme(bool isDarkTheme)
        {
            var app = Application.Current;
            if (app is not null)
            {
                app.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            }
        }

        private void ModifyFontSize(double size)
        {
            Style buttonStyle = new(x => x.OfType<Button>());
            buttonStyle.Add(new Setter()
            {
                Property = Button.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(buttonStyle);

            Style textStyle = new(x => x.OfType<TextBox>());
            textStyle.Add(new Setter()
            {
                Property = TextBox.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(textStyle);

            Style textBlockStyle = new(x => x.OfType<TextBlock>());
            textBlockStyle.Add(new Setter()
            {
                Property = TextBlock.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(textBlockStyle);

            Style menuStyle = new(x => x.OfType<Menu>());
            menuStyle.Add(new Setter()
            {
                Property = Menu.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(menuStyle);

            Style dataStyle = new(x => x.OfType<DataGridRow>());
            dataStyle.Add(new Setter()
            {
                Property = DataGridRow.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(dataStyle);

            Style listStyle = new(x => x.OfType<ListBoxItem>());
            listStyle.Add(new Setter()
            {
                Property = ListBoxItem.FontSizeProperty,
                Value = size,
            });
            Application.Current?.Styles.Add(listStyle);
        }
    }
}