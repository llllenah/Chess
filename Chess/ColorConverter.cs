using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChessTrainer
{
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorName)
            {
                switch (colorName.ToLower())
                {
                    case "white":
                        return Brushes.White;
                    case "black":
                        return Brushes.Black;
                    default:
                        return Brushes.Gray;
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}