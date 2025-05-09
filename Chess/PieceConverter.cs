using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChessTrainer
{
    /// <summary>
    /// Converts piece color and type to visual representation (text, fill, or stroke)
    /// </summary>
    public class PieceConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts color and type to visual representation
        /// </summary>
        /// <param name="values">Array with color as values[0] and type as values[1]</param>
        /// <param name="targetType">Target type for conversion</param>
        /// <param name="parameter">Mode parameter: "fill", "stroke", or "text"</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Converted value based on mode</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Better input validation with defensive programming
            if (values is null || values.Length < 2)
            {
                return DependencyProperty.UnsetValue;
            }

            // Check for DependencyProperty.UnsetValue
            if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            {
                return DependencyProperty.UnsetValue;
            }

            // Safer type casting with pattern matching
            if (values[0] is string pieceColor && values[1] is string pieceType && parameter is string mode)
            {
                // Normalize inputs to lowercase for consistency
                pieceColor = pieceColor.ToLower();
                pieceType = pieceType.ToLower();

                // Convert based on mode parameter
                return mode switch
                {
                    "fill" => Brushes.Black,

                    "stroke" => pieceColor == "white" ? Brushes.Black : Brushes.Transparent,

                    "text" => GetUnicodeSymbol(pieceColor, pieceType),

                    _ => DependencyProperty.UnsetValue
                };
            }

            return DependencyProperty.UnsetValue;
        }

        /// <summary>
        /// Gets the Unicode chess symbol for a piece
        /// </summary>
        /// <param name="color">Piece color ("white" or "black")</param>
        /// <param name="type">Piece type ("pawn", "rook", etc.)</param>
        /// <returns>Unicode chess symbol as string</returns>
        private string GetUnicodeSymbol(string color, string type)
        {
            return (color, type) switch
            {
                ("white", "pawn") => "♙",
                ("white", "rook") => "♖",
                ("white", "knight") => "♘",
                ("white", "bishop") => "♗",
                ("white", "queen") => "♕",
                ("white", "king") => "♔",
                ("black", "pawn") => "♟",
                ("black", "rook") => "♜",
                ("black", "knight") => "♞",
                ("black", "bishop") => "♝",
                ("black", "queen") => "♛",
                ("black", "king") => "♚",
                _ => ""
            };
        }

        /// <summary>
        /// Not implemented for this converter
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for PieceConverter");
        }
    }


}