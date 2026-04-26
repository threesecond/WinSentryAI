using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinSentryAI.Converters
{
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;

            // Try to parse to int
            if (int.TryParse(value.ToString(), out int intValue))
            {
                return intValue == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            // Try to parse to decimal (for counts that might be decimal, though unlikely for visibility)
            if (decimal.TryParse(value.ToString(), out decimal decValue))
            {
                return decValue == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed; // Default to collapsed if not a number
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
