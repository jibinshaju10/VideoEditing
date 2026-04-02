using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FreebeeVideo.Converters;

[ValueConversion(typeof(TimeSpan), typeof(string))]
public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.ToString(@"hh\:mm\:ss\.fff");
        return "00:00:00.000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && TimeSpan.TryParseExact(s, @"hh\:mm\:ss\.fff", culture, out TimeSpan result))
            return result;
        if (value is string s2 && TimeSpan.TryParse(s2, culture, out TimeSpan result2))
            return result2;
        return DependencyProperty.UnsetValue;
    }
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is true;
        if (Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

[ValueConversion(typeof(double), typeof(string))]
public class DoubleToPercentStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? $"{d:F0}%" : "0%";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
