using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace v2rayN.Desktop.Converters
{
    public class DelayColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            _ = int.TryParse(value?.ToString(), out var delay);

            return delay switch
            {
                <= 0 => new SolidColorBrush(Colors.Red),
                <= 500 => new SolidColorBrush(Colors.Green),
                _ => new SolidColorBrush(Colors.IndianRed)
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
