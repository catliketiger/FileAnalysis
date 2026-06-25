using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FileStruct.App.Converters;

public class ConfidenceToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double confidence || confidence < 0)
            return Brushes.Gray;

        return confidence switch
        {
            >= 0.8 => Brushes.Green,
            >= 0.5 => Brushes.Orange,
            >= 0.0 => Brushes.Red,
            _ => Brushes.Gray,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ConfidenceToPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double confidence || confidence < 0)
            return "N/A";
        return $"{confidence:P0}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
