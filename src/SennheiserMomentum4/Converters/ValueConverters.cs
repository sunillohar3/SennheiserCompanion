using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SennheiserMomentum4.Converters;

public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            if (targetType.IsEnum && Enum.TryParse(targetType, parameter.ToString(), true, out var enumVal))
            {
                return enumVal;
            }
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool boolVal && boolVal;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            bool b = v == Visibility.Visible;
            return Invert ? !b : b;
        }
        return false;
    }
}
