using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChessTrainer
{
    public class HighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isHighlighted && isHighlighted)
            {
                // Повертаємо напівпрозорий зелений колір для підсвітки можливих ходів
                return new SolidColorBrush(Color.FromArgb(100, 0, 255, 0));
            }

            // Якщо не підсвічено, повертаємо прозорий колір
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}