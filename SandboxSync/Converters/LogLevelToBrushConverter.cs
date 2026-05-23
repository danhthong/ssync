using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SandboxSync.Models;

namespace SandboxSync.Converters;

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogLevel level)
        {
            return Brushes.Gray;
        }

        return level switch
        {
            LogLevel.Debug => new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            LogLevel.Info => new SolidColorBrush(Color.FromRgb(96, 205, 255)),
            LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 185, 0)),
            LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 104, 104)),
            _ => Brushes.White
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
