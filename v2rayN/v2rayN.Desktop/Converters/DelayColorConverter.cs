using Avalonia.Data.Converters;

namespace v2rayN.Desktop.Converters;

public class DelayColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var delay = value.ToString().ToInt();

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
