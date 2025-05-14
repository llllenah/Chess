using System;

namespace ChessTrainer
{
    /// <summary>
    /// Represents a chess piece with its color and type.
    /// </summary>
    public class Piece
    {
        /// <summary>
        /// Gets the color of the piece ("white" or "black").
        /// </summary>
        public string Color { get; }

        /// <summary>
        /// Gets the type of the piece ("pawn", "knight", "bishop", "rook", "queen", or "king").
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Creates a new chess piece.
        /// </summary>
        /// <param name="color">The color of the piece ("white" or "black").</param>
        /// <param name="type">The type of the piece ("pawn", "knight", "bishop", "rook", "queen", or "king").</param>
        public Piece(string color, string type)
        {
            Color = color.ToLower(); // Ensure "white" or "black"
            Type = type.ToLower();   // Ensure lowercase type name
        }

        /// <summary>
        /// Creates a deep copy of this piece.
        /// </summary>
        /// <returns>A new Piece instance with the same color and type.</returns>
        public Piece Clone()
        {
            return new Piece(Color, Type);
        }

        /// <summary>
        /// Returns a string representation of the piece (e.g., "white pawn").
        /// </summary>
        public override string ToString()
        {
            return $"{Color} {Type}";
        }

        /// <summary>
        /// Gets the Unicode character representation of this piece for display.
        /// </summary>
        /// <returns>Unicode chess symbol as string.</returns>
        public string GetUnicodeSymbol()
        {
            return (Color, Type) switch
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
                _ => "?"
            };
        }

        /// <summary>
        /// Gets the material value of the piece for evaluation.
        /// </summary>
        /// <returns>Numerical value of the piece.</returns>
        public int GetValue()
        {
            return Type switch
            {
                "pawn" => 1,
                "knight" => 3,
                "bishop" => 3,
                "rook" => 5,
                "queen" => 9,
                "king" => 100,
                _ => 0
            };
        }

        /// <summary>
        /// Gets localized name for this piece type.
        /// </summary>
        /// <returns>Localized piece type name.</returns>
        public string GetLocalizedTypeName()
        {
            return Type switch
            {
                "pawn" => "Pawn",
                "rook" => "Rook",
                "knight" => "Knight",
                "bishop" => "Bishop",
                "queen" => "Queen",
                "king" => "King",
                _ => Type
            };
        }

        /// <summary>
        /// Gets localized name for this piece color.
        /// </summary>
        /// <returns>Localized piece color name.</returns>
        public string GetLocalizedColorName()
        {
            return Color == "white" ? "White" : "Black";
        }

        /// <summary>
        /// Gets the complete localized name of this piece.
        /// </summary>
        /// <returns>Fully localized piece name.</returns>
        public string GetLocalizedName()
        {
            return $"{GetLocalizedColorName()} {GetLocalizedTypeName()}";
        }
    }
}