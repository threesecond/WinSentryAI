using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinSentryAI.Models;

namespace WinSentryAI.Converters
{
    public class EventLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EventLevel level)
            {
                return level switch
                {
                    EventLevel.Critical => new SolidColorBrush(Colors.Red),
                    EventLevel.Error => new SolidColorBrush(Colors.OrangeRed),
                    EventLevel.Warning => new SolidColorBrush(Colors.DarkGoldenrod),
                    EventLevel.Info => new SolidColorBrush(Colors.DodgerBlue),
                    _ => new SolidColorBrush(Colors.Gray),
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}