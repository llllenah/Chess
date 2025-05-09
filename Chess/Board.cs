using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessTrainer
{
    /// <summary>
    /// Represents a chess board and implements core chess rules
    /// </summary>
    public class Board
    {
        #region Fields and Events

        /// <summary>
        /// The internal 8x8 representation of pieces on the board
        /// </summary>
        private Piece?[,] _pieces = new Piece[8, 8];

        /// <summary>
        /// Tracks pieces that have moved (for castling)
        /// </summary>
        private HashSet<string> _movedPieces = new HashSet<string>();

        /// <summary>
        /// Event fired when the board state is updated
        /// </summary>
        public event EventHandler? BoardUpdated;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new board with standard starting position
        /// </summary>
        public Board()
        {
            InitializeBoard();
        }

        /// <summary>
        /// Creates a new board with the given piece arrangement
        /// </summary>
        /// <param name="initialPieces">Initial piece arrangement</param>
        public Board(Piece?[,] initialPieces)
        {
            _pieces = new Piece[8, 8];
            ClonePieces(initialPieces, _pieces);
            _movedPieces.Clear();
        }

        #endregion

        #region Board Setup Methods

        /// <summary>
        /// Initializes the board with the standard chess starting position
        /// </summary>
        public void InitializeBoard()
        {
            // Clear the board
            ClearBoard();

            // Set up white pieces
            SetupPieces("white");

            // Set up black pieces
            SetupPieces("black");

            // Reset moved pieces tracking
            _movedPieces.Clear();

            // Notify that the board has been updated
            OnBoardUpdated();
        }

        /// <summary>
        /// Sets up pieces of a specific color in their starting positions
        /// </summary>
        /// <param name="color">Color of pieces to set up ("white" or "black")</param>
        private void SetupPieces(string color)
        {
            int backRank = color == "white" ? 7 : 0;
            int pawnRank = color == "white" ? 6 : 1;

            // Place back rank pieces
            _pieces[backRank, 0] = new Piece(color, "rook");
            _pieces[backRank, 1] = new Piece(color, "knight");
            _pieces[backRank, 2] = new Piece(color, "bishop");
            _pieces[backRank, 3] = new Piece(color, "queen");
            _pieces[backRank, 4] = new Piece(color, "king");
            _pieces[backRank, 5] = new Piece(color, "bishop");
            _pieces[backRank, 6] = new Piece(color, "knight");
            _pieces[backRank, 7] = new Piece(color, "rook");

            // Place pawns
            for (int col = 0; col < 8; col++)
            {
                _pieces[pawnRank, col] = new Piece(color, "pawn");
            }
        }

        /// <summary>
        /// Clears all pieces from the board
        /// </summary>
        public void ClearBoard()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    _pieces[row, col] = null;
                }
            }

            _movedPieces.Clear();
            OnBoardUpdated();
        }

        /// <summary>
        /// Sets a piece at the specified position
        /// </summary>
        /// <param name="row">Row (0-7)</param>
        /// <param name="col">Column (0-7)</param>
        /// <param name="piece">Piece to set (or null to clear)</param>
        public void SetPiece(int row, int col, Piece? piece)
        {
            if (IsValidPosition(row, col))
            {
                _pieces[row, col] = piece?.Clone();
                OnBoardUpdated();
            }
        }

        #endregion

        #region Board Access Methods

        /// <summary>
        /// Gets the piece at the specified position
        /// </summary>
        /// <param name="row">Row (0-7)</param>
        /// <param name="col">Column (0-7)</param>
        /// <returns>The piece at the position, or null if empty or invalid</returns>
        public Piece? GetPiece(int row, int col)
        {
            if (IsValidPosition(row, col))
            {
                return _pieces[row, col];
            }
            return null;
        }

        /// <summary>
        /// Gets a copy of the current board state
        /// </summary>
        /// <returns>A deep copy of the pieces array</returns>
        public Piece?[,] GetPieces()
        {
            Piece?[,] copy = new Piece[8, 8];
            ClonePieces(_pieces, copy);
            return copy;
        }

        /// <summary>
        /// Finds the position of a king of the specified color
        /// </summary>
        /// <param name="color">King color to find ("white" or "black")</param>
        /// <param name="row">Output parameter for the king's row</param>
        /// <param name="col">Output parameter for the king's column</param>
        /// <returns>True if the king was found, false otherwise</returns>
        public bool FindKingPosition(string color, out int row, out int col)
        {
            for (row = 0; row < 8; row++)
            {
                for (col = 0; col < 8; col++)
                {
                    Piece? piece = GetPiece(row, col);
                    if (piece?.Type == "king" && piece.Color == color)
                    {
                        return true;
                    }
                }
            }

            // King not found
            row = -1;
            col = -1;
            return false;
        }

        #endregion

        #region Movement Methods

        /// <summary>
        /// Moves a piece from one position to another without validation
        /// </summary>
        /// <param name="startRow">Starting row</param>
        /// <param name="startCol">Starting column</param>
        /// <param name="endRow">Ending row</param>
        /// <param name="endCol">Ending column</param>
        /// <returns>The captured piece, if any</returns>
        public Piece? MovePiece(int startRow, int startCol, int endRow, int endCol)
        {
            Piece? capturedPiece = GetPiece(endRow, endCol);
            Piece? movedPiece = GetPiece(startRow, startCol);

            if (movedPiece != null)
            {
                // Track piece movement (for castling rules)
                string pieceId = $"{movedPiece.Color}_{movedPiece.Type}_{startRow}_{startCol}";
                _movedPieces.Add(pieceId);

                // Handle castling
                if (movedPiece.Type == "king" && Math.Abs(endCol - startCol) == 2)
                {
                    // Move the king
                    _pieces[endRow, endCol] = movedPiece;
                    _pieces[startRow, startCol] = null;

                    // Move the appropriate rook
                    if (endCol > startCol) // Kingside castle
                    {
                        // Move rook from h-file to f-file
                        Piece? rook = GetPiece(startRow, 7);
                        _pieces[startRow, 5] = rook; // Place rook on f-file
                        _pieces[startRow, 7] = null; // Remove rook from h-file

                        // Mark rook as moved
                        if (rook != null)
                        {
                            string rookId = $"{rook.Color}_{rook.Type}_{startRow}_{7}";
                            _movedPieces.Add(rookId);
                        }
                    }
                    else // Queenside castle
                    {
                        // Move rook from a-file to d-file
                        Piece? rook = GetPiece(startRow, 0);
                        _pieces[startRow, 3] = rook; // Place rook on d-file
                        _pieces[startRow, 0] = null; // Remove rook from a-file

                        // Mark rook as moved
                        if (rook != null)
                        {
                            string rookId = $"{rook.Color}_{rook.Type}_{startRow}_{0}";
                            _movedPieces.Add(rookId);
                        }
                    }
                }
                else
                {
                    // Standard move
                    _pieces[endRow, endCol] = movedPiece;
                    _pieces[startRow, startCol] = null;
                }

                OnBoardUpdated();
            }

            return capturedPiece;
        }

        /// <summary>
        /// Checks if a move is valid according to chess rules
        /// </summary>
        /// <param name="startRow">Starting row</param>
        /// <param name="startCol">Starting column</param>
        /// <param name="endRow">Ending row</param>
        /// <param name="endCol">Ending column</param>
        /// <param name="currentPlayer">Current player's color ("white" or "black")</param>
        /// <returns>True if the move is valid, false otherwise</returns>
        public bool IsValidMove(int startRow, int startCol, int endRow, int endCol, string currentPlayer)
        {
            // Basic validation checks
            if (!IsValidPosition(startRow, startCol) || !IsValidPosition(endRow, endCol))
                return false;

            Piece? piece = GetPiece(startRow, startCol);

            // Must be moving a piece of the current player's color
            if (piece == null || piece.Color != currentPlayer)
                return false;

            // Cannot capture own pieces
            Piece? targetPiece = GetPiece(endRow, endCol);
            if (targetPiece != null && targetPiece.Color == currentPlayer)
                return false;

            // Check piece-specific movement rules
            if (!IsValidMoveForPiece(startRow, startCol, endRow, endCol, piece))
                return false;

            // Check if this move would leave the king in check
            if (MoveWouldLeaveKingInCheck(startRow, startCol, endRow, endCol, currentPlayer))
                return false;

            return true;
        }

        /// <summary>
        /// Checks if a move follows the movement rules for the specific piece type
        /// </summary>
        private bool IsValidMoveForPiece(int startRow, int startCol, int endRow, int endCol, Piece piece)
        {
            int rowDiff = endRow - startRow;
            int colDiff = endCol - startCol;
            int absRowDiff = Math.Abs(rowDiff);
            int absColDiff = Math.Abs(colDiff);

            switch (piece.Type)
            {
                case "pawn":
                    return IsValidPawnMove(startRow, startCol, endRow, endCol, piece.Color);

                case "rook":
                    return IsValidRookMove(startRow, startCol, endRow, endCol);

                case "knight":
                    return IsValidKnightMove(absRowDiff, absColDiff);

                case "bishop":
                    return IsValidBishopMove(startRow, startCol, endRow, endCol, absRowDiff, absColDiff);

                case "queen":
                    return IsValidQueenMove(startRow, startCol, endRow, endCol, absRowDiff, absColDiff);

                case "king":
                    return IsValidKingMove(startRow, startCol, endRow, endCol, absRowDiff, absColDiff);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks if a pawn move is valid
        /// </summary>
        private bool IsValidPawnMove(int startRow, int startCol, int endRow, int endCol, string color)
        {
            int direction = (color == "white") ? -1 : 1; // White moves up, black moves down
            int colDiff = endCol - startCol;
            int rowDiff = endRow - startRow;
            int absColDiff = Math.Abs(colDiff);

            // Normal forward movement (one square)
            if (colDiff == 0 && rowDiff == direction && GetPiece(endRow, endCol) == null)
                return true;

            // Initial two-square move
            if (colDiff == 0 && rowDiff == 2 * direction &&
                ((color == "white" && startRow == 6) || (color == "black" && startRow == 1)) &&
                GetPiece(startRow + direction, startCol) == null &&
                GetPiece(endRow, endCol) == null)
                return true;

            // Capture move (diagonal)
            if (absColDiff == 1 && rowDiff == direction)
            {
                // Regular capture
                Piece? targetPiece = GetPiece(endRow, endCol);
                if (targetPiece != null && targetPiece.Color != color)
                    return true;

            }

            return false;
        }

        /// <summary>
        /// Checks if a rook move is valid
        /// </summary>
        private bool IsValidRookMove(int startRow, int startCol, int endRow, int endCol)
        {
            // Rooks move horizontally or vertically
            if ((startRow == endRow || startCol == endCol) &&
                !IsPathBlocked(startRow, startCol, endRow, endCol))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a knight move is valid
        /// </summary>
        private bool IsValidKnightMove(int absRowDiff, int absColDiff)
        {
            // Knights move in an L-shape: 2 squares in one direction and 1 square perpendicular
            return (absRowDiff == 2 && absColDiff == 1) || (absRowDiff == 1 && absColDiff == 2);
        }

        /// <summary>
        /// Checks if a bishop move is valid
        /// </summary>
        private bool IsValidBishopMove(int startRow, int startCol, int endRow, int endCol, int absRowDiff, int absColDiff)
        {
            // Bishops move diagonally
            if (absRowDiff == absColDiff && !IsPathBlocked(startRow, startCol, endRow, endCol))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a queen move is valid
        /// </summary>
        private bool IsValidQueenMove(int startRow, int startCol, int endRow, int endCol, int absRowDiff, int absColDiff)
        {
            // Queens can move like a rook or a bishop
            if (((startRow == endRow || startCol == endCol) || (absRowDiff == absColDiff)) &&
                !IsPathBlocked(startRow, startCol, endRow, endCol))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a king move is valid
        /// </summary>
        private bool IsValidKingMove(int startRow, int startCol, int endRow, int endCol, int absRowDiff, int absColDiff)
        {
            // Normal king move (one square in any direction)
            if (absRowDiff <= 1 && absColDiff <= 1)
                return true;

            // Check for castling
            if (absRowDiff == 0 && absColDiff == 2)
            {
                return IsValidCastling(startRow, startCol, endRow, endCol);
            }

            return false;
        }

        /// <summary>
        /// Checks if a castling move is valid
        /// </summary>
        private bool IsValidCastling(int startRow, int startCol, int endRow, int endCol)
        {
            Piece? king = GetPiece(startRow, startCol);
            if (king == null || king.Type != "king")
                return false;

            string kingColor = king.Color;
            string kingId = $"{kingColor}_king_{startRow}_{startCol}";

            // King must not have moved
            if (_movedPieces.Contains(kingId))
                return false;

            // King must be in starting position
            if (startCol != 4 || (kingColor == "white" && startRow != 7) || (kingColor == "black" && startRow != 0))
                return false;

            // King must not be in check
            string opponentColor = kingColor == "white" ? "black" : "white";
            if (IsPositionUnderAttack(startRow, startCol, opponentColor))
                return false;

            // Check if castling kingside (to the right)
            if (endCol == 6)
            {
                // Check if rook is in place
                Piece? rook = GetPiece(startRow, 7);
                if (rook == null || rook.Type != "rook" || rook.Color != kingColor)
                    return false;

                // Rook must not have moved
                string rookId = $"{kingColor}_rook_{startRow}_7";
                if (_movedPieces.Contains(rookId))
                    return false;

                // Path must be clear
                if (GetPiece(startRow, 5) != null || GetPiece(startRow, 6) != null)
                    return false;

                // King must not pass through or end on a square that is under attack
                if (IsPositionUnderAttack(startRow, 5, opponentColor) ||
                    IsPositionUnderAttack(startRow, 6, opponentColor))
                    return false;

                return true;
            }
            // Check if castling queenside (to the left)
            else if (endCol == 2)
            {
                // Check if rook is in place
                Piece? rook = GetPiece(startRow, 0);
                if (rook == null || rook.Type != "rook" || rook.Color != kingColor)
                    return false;

                // Rook must not have moved
                string rookId = $"{kingColor}_rook_{startRow}_0";
                if (_movedPieces.Contains(rookId))
                    return false;

                // Path must be clear
                if (GetPiece(startRow, 1) != null || GetPiece(startRow, 2) != null || GetPiece(startRow, 3) != null)
                    return false;

                // King must not pass through or end on a square that is under attack
                if (IsPositionUnderAttack(startRow, 3, opponentColor) ||
                    IsPositionUnderAttack(startRow, 2, opponentColor))
                    return false;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if there are any pieces blocking the path between two positions
        /// </summary>
        private bool IsPathBlocked(int startRow, int startCol, int endRow, int endCol)
        {
            int rowDir = startRow == endRow ? 0 : Math.Sign(endRow - startRow);
            int colDir = startCol == endCol ? 0 : Math.Sign(endCol - startCol);

            int row = startRow + rowDir;
            int col = startCol + colDir;

            while (row != endRow || col != endCol)
            {
                if (GetPiece(row, col) != null)
                    return true; // Path is blocked

                row += rowDir;
                col += colDir;
            }

            return false; // Path is clear
        }

        /// <summary>
        /// Checks if a move would leave the player's king in check
        /// </summary>
        private bool MoveWouldLeaveKingInCheck(int startRow, int startCol, int endRow, int endCol, string currentPlayer)
        {
            // Create a temporary board to test the move
            Board tempBoard = new Board(GetPieces());

            // Execute the move on the temporary board
            tempBoard.MovePiece(startRow, startCol, endRow, endCol);

            // Find the king's position after the move
            string opponentColor = currentPlayer == "white" ? "black" : "white";

            // If we're moving the king, the king's new position is the destination
            Piece? piece = GetPiece(startRow, startCol);
            bool isKingMove = piece?.Type == "king";

            int kingRow = isKingMove ? endRow : -1;
            int kingCol = isKingMove ? endCol : -1;

            // If not moving the king, find the king's position
            if (!isKingMove)
            {
                tempBoard.FindKingPosition(currentPlayer, out kingRow, out kingCol);
            }

            // If king not found (unlikely but possible in custom positions), treat as invalid move
            if (kingRow == -1)
                return true;

            // Check if any opponent's piece can attack the king's position
            return tempBoard.IsPositionUnderAttack(kingRow, kingCol, opponentColor);
        }

        /// <summary>
        /// Checks if a position is under attack by pieces of the specified color
        /// </summary>
        /// <param name="row">Target row</param>
        /// <param name="col">Target column</param>
        /// <param name="attackingColor">Color of attacking pieces</param>
        /// <returns>True if the position is under attack, false otherwise</returns>
        public bool IsPositionUnderAttack(int row, int col, string attackingColor)
        {
            // Check attacks from all directions

            // Check diagonal attacks (bishop, queen, king, pawn)
            if (IsDiagonallyAttacked(row, col, attackingColor))
                return true;

            // Check horizontal/vertical attacks (rook, queen, king)
            if (IsOrthogonallyAttacked(row, col, attackingColor))
                return true;

            // Check knight attacks
            if (IsKnightAttacked(row, col, attackingColor))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a position is under attack diagonally
        /// </summary>
        private bool IsDiagonallyAttacked(int row, int col, string attackingColor)
        {
            // Check each of the four diagonal directions
            int[][] directions = new int[][]
            {
                new int[] { -1, -1 }, // Up-left
                new int[] { -1, 1 },  // Up-right
                new int[] { 1, -1 },  // Down-left
                new int[] { 1, 1 }    // Down-right
            };

            foreach (int[] dir in directions)
            {
                // Check pawn attacks (only one step diagonally)
                int pawnDir = attackingColor == "white" ? 1 : -1;
                if (dir[0] == pawnDir)
                {
                    int pawnRow = row + dir[0];
                    int pawnCol = col + dir[1];

                    if (IsValidPosition(pawnRow, pawnCol))
                    {
                        Piece? piece = GetPiece(pawnRow, pawnCol);
                        if (piece?.Type == "pawn" && piece.Color == attackingColor)
                            return true;
                    }
                }

                // Check long-range diagonal attacks (bishop, queen)
                for (int dist = 1; dist < 8; dist++)
                {
                    int targetRow = row + dir[0] * dist;
                    int targetCol = col + dir[1] * dist;

                    if (!IsValidPosition(targetRow, targetCol))
                        break;

                    Piece? piece = GetPiece(targetRow, targetCol);

                    if (piece != null)
                    {
                        if (piece.Color == attackingColor &&
                            (piece.Type == "bishop" || piece.Type == "queen" ||
                             (piece.Type == "king" && dist == 1)))
                            return true;

                        break; // Blocked by a piece
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a position is under attack horizontally or vertically
        /// </summary>
        private bool IsOrthogonallyAttacked(int row, int col, string attackingColor)
        {
            // Check each of the four orthogonal directions
            int[][] directions = new int[][]
            {
                new int[] { -1, 0 }, // Up
                new int[] { 1, 0 },  // Down
                new int[] { 0, -1 }, // Left
                new int[] { 0, 1 }   // Right
            };

            foreach (int[] dir in directions)
            {
                for (int dist = 1; dist < 8; dist++)
                {
                    int targetRow = row + dir[0] * dist;
                    int targetCol = col + dir[1] * dist;

                    if (!IsValidPosition(targetRow, targetCol))
                        break;

                    Piece? piece = GetPiece(targetRow, targetCol);

                    if (piece != null)
                    {
                        if (piece.Color == attackingColor &&
                            (piece.Type == "rook" || piece.Type == "queen" ||
                             (piece.Type == "king" && dist == 1)))
                            return true;

                        break; // Blocked by a piece
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a position is under attack by knights
        /// </summary>
        private bool IsKnightAttacked(int row, int col, string attackingColor)
        {
            // All possible knight moves
            int[][] knightMoves = new int[][]
            {
                new int[] { -2, -1 }, new int[] { -2, 1 },
                new int[] { -1, -2 }, new int[] { -1, 2 },
                new int[] { 1, -2 }, new int[] { 1, 2 },
                new int[] { 2, -1 }, new int[] { 2, 1 }
            };

            foreach (int[] move in knightMoves)
            {
                int targetRow = row + move[0];
                int targetCol = col + move[1];

                if (IsValidPosition(targetRow, targetCol))
                {
                    Piece? piece = GetPiece(targetRow, targetCol);
                    if (piece?.Type == "knight" && piece.Color == attackingColor)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the king of the specified color is in check
        /// </summary>
        /// <param name="color">Color of the king to check</param>
        /// <returns>True if the king is in check, false otherwise</returns>
        public bool IsKingInCheck(string color)
        {
            if (FindKingPosition(color, out int kingRow, out int kingCol))
            {
                string opponentColor = color == "white" ? "black" : "white";
                return IsPositionUnderAttack(kingRow, kingCol, opponentColor);
            }

            return false; // King not found
        }

        /// <summary>
        /// Gets all possible valid moves for a piece at the given position
        /// </summary>
        /// <param name="row">The row of the piece</param>
        /// <param name="col">The column of the piece</param>
        /// <returns>List of valid destination positions as (row, col) tuples</returns>
        public List<(int, int)> GetValidMovesForPiece(int row, int col)
        {
            List<(int, int)> validMoves = new List<(int, int)>();

            Piece? piece = GetPiece(row, col);
            if (piece == null) return validMoves;

            string pieceColor = piece.Color;

            // Try all possible destinations
            for (int targetRow = 0; targetRow < 8; targetRow++)
            {
                for (int targetCol = 0; targetCol < 8; targetCol++)
                {
                    // Skip the current position
                    if (targetRow == row && targetCol == col)
                        continue;

                    // Check if move is valid
                    if (IsValidMove(row, col, targetRow, targetCol, pieceColor))
                    {
                        validMoves.Add((targetRow, targetCol));
                    }
                }
            }

            return validMoves;
        }

        /// <summary>
        /// Gets all possible moves for a player
        /// </summary>
        /// <param name="playerColor">Color of the player to get moves for</param>
        /// <returns>List of all valid moves</returns>
        public List<Move> GetAllPossibleMovesForPlayer(string playerColor)
        {
            List<Move> allMoves = new List<Move>();

            // Check moves for each piece
            for (int startRow = 0; startRow < 8; startRow++)
            {
                for (int startCol = 0; startCol < 8; startCol++)
                {
                    Piece? piece = GetPiece(startRow, startCol);
                    if (piece != null && piece.Color == playerColor)
                    {
                        // For each valid destination
                        foreach (var (endRow, endCol) in GetValidMovesForPiece(startRow, startCol))
                        {
                            Move move = new Move(startRow, startCol, endRow, endCol);
                            allMoves.Add(move);
                        }
                    }
                }
            }

            return allMoves;
        }

        /// <summary>
        /// Checks the board for checkmate or stalemate
        /// </summary>
        /// <param name="playerColor">Color of the player to check</param>
        /// <returns>GameEndType enum indicating the result</returns>
        public GameEndType CheckForGameEnd(string playerColor)
        {
            // Check if player has any legal moves
            List<Move> possibleMoves = GetAllPossibleMovesForPlayer(playerColor);

            // If there are possible moves, game continues
            if (possibleMoves.Count > 0)
                return GameEndType.None;

            // No moves - either checkmate or stalemate
            if (IsKingInCheck(playerColor))
                return GameEndType.Checkmate;
            else
                return GameEndType.Stalemate;
        }

        /// <summary>
        /// Evaluates the current board position (positive for white advantage)
        /// </summary>
        /// <returns>Numerical score of the position</returns>
        public int EvaluatePosition()
        {
            int whiteScore = 0;
            int blackScore = 0;

            // Material evaluation
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Piece? piece = GetPiece(row, col);
                    if (piece != null)
                    {
                        int value = piece.GetValue();
                        if (piece.Color == "white")
                            whiteScore += value;
                        else
                            blackScore += value;
                    }
                }
            }

            // Bonus for check
            if (IsKingInCheck("black"))
                whiteScore += 10;

            if (IsKingInCheck("white"))
                blackScore += 10;

            // Return final score (positive = white advantage)
            return whiteScore - blackScore;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if a position is within the bounds of the board
        /// </summary>
        /// <param name="row">Row to check</param>
        /// <param name="col">Column to check</param>
        /// <returns>True if position is valid, false otherwise</returns>
        public bool IsValidPosition(int row, int col)
        {
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }

        /// <summary>
        /// Deep copies pieces from source to destination
        /// </summary>
        private void ClonePieces(Piece?[,] source, Piece?[,] destination)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    destination[row, col] = source[row, col]?.Clone();
                }
            }
        }

        /// <summary>
        /// Raises the BoardUpdated event
        /// </summary>
        protected virtual void OnBoardUpdated()
        {
            BoardUpdated?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// Possible end states for a chess game
    /// </summary>
    public enum GameEndType
    {
        None,
        Checkmate,
        Stalemate,
        KingCaptured,
        Draw,
        Resignation
    }
}