using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChessTrainer
{
    public class PieceColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is string pieceColor && values[1] is Brush backgroundColor)
            {
                if (pieceColor.ToLower() == "white")
                {
                    if (backgroundColor == Brushes.White || backgroundColor == Brushes.LightGray)
                    {
                        return Brushes.Black;
                    }
                    else
                    {
                        return Brushes.White;
                    }
                }
                else if (pieceColor.ToLower() == "black")
                {
                    return Brushes.Black;
                }
            }
            return Brushes.Gray;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}