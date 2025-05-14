using System;

namespace ChessTrainer
{
    /// <summary>
    /// Represents a chess move with its starting and ending positions.
    /// </summary>
    public class Move
    {
        /// <summary>
        /// Gets the starting row of the move (0-7).
        /// </summary>
        public int StartRow { get; }

        /// <summary>
        /// Gets the starting column of the move (0-7).
        /// </summary>
        public int StartCol { get; }

        /// <summary>
        /// Gets the ending row of the move (0-7).
        /// </summary>
        public int EndRow { get; }

        /// <summary>
        /// Gets the ending column of the move (0-7).
        /// </summary>
        public int EndCol { get; }

        /// <summary>
        /// Gets or sets the evaluation score for this move (used by AI algorithms).
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Gets or sets any additional move flags (e.g., promotion, en passant).
        /// </summary>
        public MoveFlag Flag { get; set; }

        /// <summary>
        /// Gets or sets the captured piece information.
        /// </summary>
        public Piece? CapturedPiece { get; set; }

        /// <summary>
        /// Creates a new move.
        /// </summary>
        /// <param name="startRow">Starting row.</param>
        /// <param name="startCol">Starting column.</param>
        /// <param name="endRow">Ending row.</param>
        /// <param name="endCol">Ending column.</param>
        public Move(int startRow, int startCol, int endRow, int endCol)
        {
            StartRow = startRow;
            StartCol = startCol;
            EndRow = endRow;
            EndCol = endCol;
            Score = 0;
            Flag = MoveFlag.Normal;
        }

        /// <summary>
        /// Returns the algebraic notation for this move (e.g., "e2-e4").
        /// </summary>
        /// <returns>String representation of the move in algebraic notation.</returns>
        public string ToAlgebraicNotation()
        {
            return $"{GetSquareNotation(StartCol, StartRow)}-{GetSquareNotation(EndCol, EndRow)}";
        }

        /// <summary>
        /// Returns a string representation of this move.
        /// </summary>
        public override string ToString()
        {
            return ToAlgebraicNotation();
        }

        /// <summary>
        /// Convert a board position to algebraic notation.
        /// </summary>
        /// <param name="col">Column (0-7).</param>
        /// <param name="row">Row (0-7).</param>
        /// <returns>Square in algebraic notation (e.g., "e2").</returns>
        private string GetSquareNotation(int col, int row)
        {
            return $"{(char)('a' + col)}{8 - row}";
        }

        /// <summary>
        /// Creates a deep copy of this move.
        /// </summary>
        /// <returns>A new identical Move object.</returns>
        public Move Clone()
        {
            Move clone = new Move(StartRow, StartCol, EndRow, EndCol)
            {
                Score = Score,
                Flag = Flag
            };

            if (CapturedPiece != null)
            {
                clone.CapturedPiece = CapturedPiece.Clone();
            }

            return clone;
        }
    }

    /// <summary>
    /// Special flags for chess moves.
    /// </summary>
    public enum MoveFlag
    {
        /// <summary>Regular move.</summary>
        Normal,

        /// <summary>Pawn promotion move.</summary>
        Promotion,

        /// <summary>En passant capture move.</summary>
        EnPassantCapture,

        /// <summary>Castling move.</summary>
        Castling
    }

    ///// <summary>
    ///// Possible end states for a chess game.
    ///// </summary>
    //public enum GameEndType
    //{
    //    /// <summary>Game is not over.</summary>
    //    None,

    //    /// <summary>Game ended by checkmate.</summary>
    //    Checkmate,

    //    /// <summary>Game ended by stalemate.</summary>
    //    Stalemate,

    //    /// <summary>Game ended by king capture.</summary>
    //    KingCaptured,

    //    /// <summary>Game ended in a draw by rule.</summary>
    //    Draw,

    //    /// <summary>Game ended by resignation.</summary>
    //    Resignation,

    //    /// <summary>Game ended due to insufficient material for checkmate.</summary>
    //    InsufficientMaterial
    //}
}