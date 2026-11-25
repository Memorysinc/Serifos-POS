using System;
using System.Globalization;
using System.Windows.Data;

namespace FirewallControl.App.Views;

/// <summary>
/// Converts an integer property (e.g., ListaPrecios 1..7 or FormaPago 1..2) to bool
/// against a XAML ConverterParameter for native RadioButton two-way binding.
/// </summary>
public sealed class IntEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter != null && int.TryParse(parameter.ToString(), out int parsed))
            return parsed;

        return Binding.DoNothing;
    }
}
