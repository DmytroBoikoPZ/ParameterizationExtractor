using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Quipu.ParameterizationExtractor.Desktop.Converters;

/// <summary>
/// Two-way converter for binding <see cref="System.Windows.Controls.RadioButton.IsChecked"/>
/// to an enum value. <c>ConverterParameter</c> is the enum member name (string).
/// Convert: returns true when the bound enum equals the parameter member.
/// ConvertBack: when IsChecked switches to true, returns the parsed enum value (so the source
/// switches groups); switching to false returns <see cref="Binding.DoNothing"/>.
/// </summary>
internal sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string memberName) return false;
        return string.Equals(value.ToString(), memberName, StringComparison.Ordinal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string memberName && targetType.IsEnum)
        {
            return Enum.Parse(targetType, memberName);
        }
        return Binding.DoNothing;
    }
}
