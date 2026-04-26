using System;
using System.Globalization;
using System.Windows.Data;

namespace WinSentryAI.Converters
{
    public class StringEqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value?.ToString() == parameter?.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            (bool)value ? parameter : Binding.DoNothing;
    }
}
