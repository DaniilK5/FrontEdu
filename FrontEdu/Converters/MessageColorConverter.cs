using System.Globalization;

namespace FrontEdu.Converters
{
    public class MessageColorConverter : IValueConverter
    {
        public Color FromUserColor { get; set; } = Colors.White;
        public Color ToUserColor { get; set; } = Color.FromArgb("#E3F2FD");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFromCurrentUser)
            {
                return isFromCurrentUser ? ToUserColor : FromUserColor;
            }
            return FromUserColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}