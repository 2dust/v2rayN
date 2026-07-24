using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using v2rayN.Helper;

namespace v2rayN.Converters;

public class FlagImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string countryCode)
        {
            // Extract country code from formats like "(US) 1.2.3.4" or "US"
            var code = ExtractCountryCode(countryCode);
            return FlagImageHelper.GetFlagImage(code) ?? new BitmapImage();
        }
        return new BitmapImage();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string? ExtractCountryCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        text = text.Trim();

        // Format: "(XX) IP" - extract XX
        if (text.StartsWith("(") && text.Length >= 4)
        {
            var closeIdx = text.IndexOf(')');
            if (closeIdx == 2 || closeIdx == 3)
            {
                return text.Substring(1, closeIdx - 1).Trim();
            }
        }

        // Format: "XX" - just the code
        if (text.Length == 2)
        {
            return text;
        }

        return null;
    }
}
