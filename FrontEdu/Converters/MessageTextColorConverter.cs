using System.Globalization;

namespace FrontEdu.Converters
{
    public class MessageTextColorConverter : IValueConverter
    {
        public Color FromUserColor { get; set; } = Colors.Black;
        public Color ToUserColor { get; set; } = Colors.Black;

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