using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileStruct.App.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        if (parameter is string invert && invert == "Invert")
            b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class FileSizeConverter : IValueConverter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB"];
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes == 0) return "0 B";
        var unitIdx = 0;
        var size = (double)bytes;
        while (size >= 1024 && unitIdx < Units.Length - 1)
        {
            size /= 1024;
            unitIdx++;
        }
        return $"{size:F1} {Units[unitIdx]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
