using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChessTrainer
{
    public class PieceConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 ||
                values[0] == DependencyProperty.UnsetValue ||
                values[1] == DependencyProperty.UnsetValue)
            {
                return DependencyProperty.UnsetValue;
            }

            string? pieceColor = values[0] as string;
            string? pieceType = values[1] as string;
            string? mode = parameter as string; // параметр: fill, stroke, text

            if (pieceColor == null || pieceType == null)
                return DependencyProperty.UnsetValue;

            pieceColor = pieceColor.ToLower();
            pieceType = pieceType.ToLower();

            switch (mode)
            {
                case "fill":
                    return Brushes.Black;

                case "stroke":
                    return pieceColor == "white" ? Brushes.Black : Brushes.Transparent;

                case "text":
                    return pieceColor switch
                    {
                        "white" => pieceType switch
                        {
                            "pawn" => "♙",
                            "rook" => "♖",
                            "knight" => "♘",
                            "bishop" => "♗",
                            "queen" => "♕",
                            "king" => "♔",
                            _ => ""
                        },
                        "black" => pieceType switch
                        {
                            "pawn" => "♟",
                            "rook" => "♜",
                            "knight" => "♞",
                            "bishop" => "♝",
                            "queen" => "♛",
                            "king" => "♚",
                            _ => ""
                        },
                        _ => ""
                    };

                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
