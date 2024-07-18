﻿using System.Windows.Media;
using v2rayN.Handler;

namespace v2rayN.Converters;

public class MaterialDesignFonts
{
  public static FontFamily MyFont { get; }

  static MaterialDesignFonts()
  {
    try
    {
      var fontFamily = LazyConfig.Instance.GetConfig().uiItem.currentFontFamily;
      if (!Utils.IsNullOrEmpty(fontFamily))
      {
        var fontPath = Utils.GetFontsPath();
        MyFont = new FontFamily(new Uri(@$"file:///{fontPath}\"), $"./#{fontFamily}");
      }
    }
    catch
    {
    }
    MyFont ??= new FontFamily("Microsoft YaHei");
  }
}