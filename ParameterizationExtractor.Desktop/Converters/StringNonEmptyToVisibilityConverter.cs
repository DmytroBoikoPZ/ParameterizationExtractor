using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Quipu.ParameterizationExtractor.Desktop.Converters;

internal sealed class StringNonEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
