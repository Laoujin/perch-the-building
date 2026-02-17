using System.Globalization;
using System.Windows.Data;

namespace Perch.Desktop.Converters;

public sealed class BooleanToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "\u25BC" : "\u25B6";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
