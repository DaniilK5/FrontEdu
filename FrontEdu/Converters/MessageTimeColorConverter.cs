using System.Globalization;

namespace FrontEdu.Converters
{
    public class MessageTimeColorConverter : IValueConverter
    {
        public Color FromUserColor { get; set; } = Color.FromArgb("#666666");
        public Color ToUserColor { get; set; } = Color.FromArgb("#666666");

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