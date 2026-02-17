using System.Globalization;
using System.Windows.Data;

using Perch.Core;

namespace Perch.Desktop.Converters;

public sealed class PathEllipsisConverter : IValueConverter
{
    private const int DefaultMaxLength = 75;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return value ?? string.Empty;

        var maxLength = parameter is string s && int.TryParse(s, out var parsed)
            ? parsed
            : DefaultMaxLength;

        return PathDisplay.TruncateMiddle(path, maxLength);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
