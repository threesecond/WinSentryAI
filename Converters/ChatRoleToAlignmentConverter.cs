using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinSentryAI.Models;

namespace WinSentryAI.Converters
{
    public class ChatRoleToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatRole role)
            {
                return role == ChatRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }
            return HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
