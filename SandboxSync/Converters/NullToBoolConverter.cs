using System.Globalization;
using System.Windows.Data;

namespace SandboxSync.Converters;

public sealed class NullToBoolConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null && (value is not string s || !string.IsNullOrWhiteSpace(s));
        return Invert ? !hasValue : hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
