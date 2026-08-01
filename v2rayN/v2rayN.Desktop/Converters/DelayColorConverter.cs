using Avalonia.Data.Converters;

namespace v2rayN.Desktop.Converters;

public class DelayColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var delay = value?.ToString().ToInt() ?? 0;

        return delay switch
        {
            //0 means there is no measurement: either cleared, still testing or skipped.
            //Keep the default foreground so that it is not mistaken for a failure.
            0 => AvaloniaProperty.UnsetValue,
            < 0 => new SolidColorBrush(Colors.Red),
            <= 500 => new SolidColorBrush(Colors.Green),
            _ => new SolidColorBrush(Colors.IndianRed)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
