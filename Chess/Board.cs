using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ChessTrainer
{
    public class Board
    {
        private Piece?[,] _pieces = new Piece[8, 8];
        public event EventHandler? BoardUpdated;

        public Board()
        {
            InitializeBoard();
        }

        public Board(Piece?[,] initialPieces)
        {
            _pieces = new Piece[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    _pieces[i, j] = initialPieces[i, j]?.Clone();
                }
            }
        }

        public void SetPiece(int row, int col, Piece? piece)
        {
            if (IsValidPosition(row, col))
            {
                _pieces[row, col] = piece;
            }
        }

        public bool IsValidPosition(int row, int col)
        {
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }

        public void InitializeBoard()
        {
            // Розміщення білих фігур
            _pieces[7, 0] = new Piece("white", "rook");
            _pieces[7, 1] = new Piece("white", "knight");
            _pieces[7, 2] = new Piece("white", "bishop");
            _pieces[7, 3] = new Piece("white", "queen");
            _pieces[7, 4] = new Piece("white", "king");
            _pieces[7, 5] = new Piece("white", "bishop");
            _pieces[7, 6] = new Piece("white", "knight");
            _pieces[7, 7] = new Piece("white", "rook");
            for (int i = 0; i < 8; i++)
            {
                _pieces[6, i] = new Piece("white", "pawn");
            }

            // Розміщення чорних фігур
            _pieces[0, 0] = new Piece("black", "rook");
            _pieces[0, 1] = new Piece("black", "knight");
            _pieces[0, 2] = new Piece("black", "bishop");
            _pieces[0, 3] = new Piece("black", "queen");
            _pieces[0, 4] = new Piece("black", "king");
            _pieces[0, 5] = new Piece("black", "bishop");
            _pieces[0, 6] = new Piece("black", "knight");
            _pieces[0, 7] = new Piece("black", "rook");
            for (int i = 0; i < 8; i++)
            {
                _pieces[1, i] = new Piece("black", "pawn");
            }

            // Очищаємо порожні клітинки
            for (int row = 2; row < 6; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    _pieces[row, col] = null;
                }
            }
        }

        public void SetBoard(BoardCell[,] boardCells)
        {
            _pieces = new Piece[8, 8];
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    _pieces[row, col] = boardCells[row, col].Piece?.Clone();
                }
            }
            // Додайте цей виклик, щоб оновити внутрішній стан дошки GameLogic
            UpdateBoardState();
        }

        private void UpdateBoardState()
        {
            BoardUpdated?.Invoke(this, EventArgs.Empty);
        }


        // --- Методи для перевірки атаки ---

        public bool IsPieceAttackingSquare(int attackingRow, int attackingCol, int targetRow, int targetCol)
        {
            Piece? piece = GetPiece(attackingRow, attackingCol);
            if (piece == null) return false;

            return IsValidMoveInternal(attackingRow, attackingCol, targetRow, targetCol);
        }

        public bool IsPieceAttackingSquare(int attackingRow, int attackingCol, int targetRow, int targetCol, string attackingColor)
        {
            Piece? originalPiece = GetPiece(attackingRow, attackingCol);
            if (originalPiece == null || originalPiece.Color != attackingColor) return false;

            Piece tempPiece = new Piece(attackingColor, originalPiece.Type);

            return IsValidMoveInternal(attackingRow, attackingCol, targetRow, targetCol, tempPiece);
        }

        private bool IsValidMoveInternal(int startRow, int startCol, int endRow, int endCol, Piece? pieceToCheck = null)
        {
            Piece? piece = pieceToCheck ?? GetPiece(startRow, startCol);
            if (piece == null) return false;

            int rowDiff = Math.Abs(endRow - startRow);
            int colDiff = Math.Abs(endCol - startCol);

            switch (piece.Type)
            {
                case "pawn":
                    int direction = (piece.Color == "white") ? -1 : 1;
                    // Хід на одну клітинку вперед
                    if (endCol == startCol && endRow == startRow + direction && GetPiece(endRow, endCol) == null)
                        return true;
                    // Хід на дві клітинки вперед з початкової позиції
                    if (endCol == startCol && endRow == startRow + 2 * direction && ((piece.Color == "white" && startRow == 6) || (piece.Color == "black" && startRow == 1)) && GetPiece(startRow + direction, startCol) == null && GetPiece(endRow, endCol) == null)
                        return true;
                    // Атака по діагоналі на одну клітинку
                    if (colDiff == 1 && endRow == startRow + direction && GetPiece(endRow, endCol) != null && GetPiece(endRow, endCol)?.Color != piece.Color)
                        return true;
                    return false;
                case "rook":
                    if ((rowDiff == 0 && colDiff > 0) || (colDiff == 0 && rowDiff > 0))
                        return !IsPathBlocked(startRow, startCol, endRow, endCol);
                    return false;
                case "knight":
                    return (rowDiff == 2 && colDiff == 1) || (rowDiff == 1 && colDiff == 2);
                case "bishop":
                    if (rowDiff == colDiff && rowDiff > 0)
                        return !IsPathBlocked(startRow, startCol, endRow, endCol);
                    return false;
                case "queen":
                    if ((rowDiff == 0 && colDiff > 0) || (colDiff == 0 && rowDiff > 0) || (rowDiff == colDiff && rowDiff > 0))
                        return !IsPathBlocked(startRow, startCol, endRow, endCol);
                    return false;
                case "king":
                    return rowDiff <= 1 && colDiff <= 1;
                default:
                    return false;
            }
        }

        private bool IsPathBlocked(int startRow, int startCol, int endRow, int endCol)
        {
            int rowDir = Math.Sign(endRow - startRow);
            int colDir = Math.Sign(endCol - startCol);
            int currentRow = startRow + rowDir;
            int currentCol = startCol + colDir;

            while (currentRow != endRow || currentCol != endCol)
            {
                if (GetPiece(currentRow, currentCol) != null)
                    return true;
                currentRow += rowDir;
                currentCol += colDir;
            }
            return false;
        }

        public Piece? GetPiece(int row, int col)
        {
            if (row >= 0 && row < 8 && col >= 0 && col < 8)
            {
                return _pieces[row, col];
            }
            return null;
        }

        public bool IsKingInCheck(int kingRow, int kingCol, string attackingColor)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Piece? piece = GetPiece(r, c);
                    if (piece != null && piece.Color == attackingColor)
                    {
                        if (IsPieceAttackingSquare(r, c, kingRow, kingCol, attackingColor))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // --- Методи для ходів ---

        public void MovePiece(int startRow, int startCol, int endRow, int endCol)
        {
            Piece? pieceToMove = GetPiece(startRow, startCol);
            SetPiece(startRow, startCol, null);
            SetPiece(endRow, endCol, pieceToMove);
        }

        public Piece?[,] GetPieces()
        {
            Piece?[,] tempPieces = new Piece[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    tempPieces[i, j] = _pieces[i, j]?.Clone();
                }
            }
            return tempPieces;
        }

        public bool IsValidMove(int startRow, int startCol, int endRow, int endCol, string currentPlayer)
        {
            Piece? piece = GetPiece(startRow, startCol);

            if (piece == null || piece.Color != currentPlayer)
            {
                return false; // На клітинці немає фігури поточного гравця
            }

            if (!IsValidPosition(endRow, endCol))
            {
                return false; // Недійсна кінцева позиція
            }

            Piece? targetPiece = GetPiece(endRow, endCol);
            if (targetPiece != null && targetPiece.Color == currentPlayer)
            {
                return false; // Не можна брати власні фігури
            }

            int rowDifference = endRow - startRow;
            int colDifference = endCol - startCol;
            int absRowDifference = Math.Abs(rowDifference);
            int absColDifference = Math.Abs(colDifference);

            // Перевірка правил руху для кожної фігури
            bool isMoveValidAccordingToPieceRules = false;
            switch (piece.Type)
            {
                case "pawn":
                    int pawnMoveDir = (currentPlayer == "white") ? -1 : 1;
                    // Простий рух вперед на 1 клітинку
                    if (colDifference == 0 && rowDifference == pawnMoveDir && targetPiece == null)
                        isMoveValidAccordingToPieceRules = true;
                    // Рух вперед на 2 клітинки з початкової позиції
                    else if ((startRow == 6 && currentPlayer == "white" || startRow == 1 && currentPlayer == "black") &&
                             colDifference == 0 && rowDifference == 2 * pawnMoveDir && targetPiece == null &&
                             GetPiece(startRow + pawnMoveDir, startCol) == null)
                        isMoveValidAccordingToPieceRules = true;
                    // Взяття по діагоналі
                    else if (absColDifference == 1 && rowDifference == pawnMoveDir && targetPiece != null && targetPiece.Color != currentPlayer)
                        isMoveValidAccordingToPieceRules = true;
                    break;
                case "rook":
                    if ((rowDifference == 0 || colDifference == 0) && IsPathClear(startRow, startCol, endRow, endCol))
                        isMoveValidAccordingToPieceRules = true;
                    break;
                case "knight":
                    if ((absRowDifference == 2 && absColDifference == 1) || (absRowDifference == 1 && absColDifference == 2))
                        isMoveValidAccordingToPieceRules = true;
                    break;
                case "bishop":
                    if (absRowDifference == absColDifference && IsPathClear(startRow, startCol, endRow, endCol))
                        isMoveValidAccordingToPieceRules = true;
                    break;
                case "queen":
                    if (((rowDifference == 0 || colDifference == 0) || (absRowDifference == absColDifference)) && IsPathClear(startRow, startCol, endRow, endCol))
                        isMoveValidAccordingToPieceRules = true;
                    break;
                case "king":
                    if (absRowDifference <= 1 && absColDifference <= 1)
                        isMoveValidAccordingToPieceRules = true;
                    break;
            }

            if (isMoveValidAccordingToPieceRules)
            {
                // Перевірка, чи не ставить цей хід власного короля під шах
                Board tempBoard = new Board(GetPieces());
                tempBoard.MovePiece(startRow, startCol, endRow, endCol);
                string opponentColor = (currentPlayer == "white") ? "black" : "white";
                int kingRow = -1, kingCol = -1;
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        if (tempBoard.GetPiece(r, c)?.Type == "king" && tempBoard.GetPiece(r, c)?.Color == currentPlayer)
                        {
                            kingRow = r;
                            kingCol = c;
                            break;
                        }
                    }
                    if (kingRow != -1) break;
                }
                if (kingRow != -1 && tempBoard.IsKingInCheck(kingRow, kingCol, opponentColor))
                {
                    return false; // Хід залишає власного короля під шахом
                }

                // Додаткова перевірка для короля: чи не йде він на атаковану клітинку
                if (piece.Type == "king")
                {
                    if (IsKingInCheck(endRow, endCol, opponentColor))
                    {
                        return false; // Король не може піти на атаковану клітинку
                    }
                }

                return true;
            }

            return false;
        }


        // Add this method to your Board class

        /// <summary>
        /// Returns a list of valid destination positions for a piece at the given position
        /// </summary>
        /// <param name="row">Source row</param>
        /// <param name="col">Source column</param>
        /// <returns>List of (row, col) tuples representing valid destinations</returns>
        public List<(int, int)> GetValidMovesForPiece(int row, int col)
        {
            List<(int, int)> validMoves = new List<(int, int)>();

            // Check if there's a piece at the given position
            Piece? piece = GetPiece(row, col);
            if (piece == null) return validMoves;

            string pieceColor = piece.Color;

            // Check all cells on the board as potential destinations
            for (int targetRow = 0; targetRow < 8; targetRow++)
            {
                for (int targetCol = 0; targetCol < 8; targetCol++)
                {
                    // Skip the source cell itself
                    if (targetRow == row && targetCol == col) continue;

                    // If the move is valid according to chess rules, add it to the list
                    if (IsValidMove(row, col, targetRow, targetCol, pieceColor))
                    {
                        validMoves.Add((targetRow, targetCol));
                    }
                }
            }

            return validMoves;
        }

        /// <summary>
        /// Check if a move from (sourceRow, sourceCol) to (targetRow, targetCol) is valid for the given player color
        /// </summary>
        //private bool IsValidMove(int sourceRow, int sourceCol, int targetRow, int targetCol, string playerColor)
        //{
        //    // Get the piece at the source position
        //    Piece? sourcePiece = GetPiece(sourceRow, sourceCol);
        //    if (sourcePiece == null || sourcePiece.Color != playerColor) return false;

        //    // Get the piece at the target position
        //    Piece? targetPiece = GetPiece(targetRow, targetCol);

        //    // Can't capture own pieces
        //    if (targetPiece != null && targetPiece.Color == playerColor) return false;

        //    // Check move validity based on piece type
        //    switch (sourcePiece.Type)
        //    {
        //        case "pawn":
        //            return IsValidPawnMove(sourceRow, sourceCol, targetRow, targetCol, playerColor);
        //        case "rook":
        //            return IsValidRookMove(sourceRow, sourceCol, targetRow, targetCol);
        //        case "knight":
        //            return IsValidKnightMove(sourceRow, sourceCol, targetRow, targetCol);
        //        case "bishop":
        //            return IsValidBishopMove(sourceRow, sourceCol, targetRow, targetCol);
        //        case "queen":
        //            return IsValidQueenMove(sourceRow, sourceCol, targetRow, targetCol);
        //        case "king":
        //            return IsValidKingMove(sourceRow, sourceCol, targetRow, targetCol);
        //        default:
        //            return false;
        //    }
        //}

        // Your existing piece movement validation methods would be used here
        // Make sure they're accessible from this context
        // Examples might include:
        // private bool IsValidPawnMove(int sourceRow, int sourceCol, int targetRow, int targetCol, string playerColor)
        // private bool IsValidRookMove(int sourceRow, int sourceCol, int targetRow, int targetCol)
        // etc.
        private bool IsPathClear(int startRow, int startCol, int endRow, int endCol)
        {
            int rowDifference = endRow - startRow;
            int colDifference = endCol - startCol;
            int rowDir = Math.Sign(rowDifference);
            int colDir = Math.Sign(colDifference);
            int steps = Math.Max(Math.Abs(rowDifference), Math.Abs(colDifference));

            for (int i = 1; i < steps; i++)
            {
                if (GetPiece(startRow + i * rowDir, startCol + i * colDir) != null)
                {
                    return false; // Є фігура на шляху
                }
            }
            return true;
        }

        public List<Move> GetAllPossibleMovesForPlayer(string playerColor)
        {
            List<Move> legalMoves = new List<Move>();

            for (int startRow = 0; startRow < 8; startRow++)
            {
                for (int startCol = 0; startCol < 8; startCol++)
                {
                    Piece? piece = GetPiece(startRow, startCol);
                    if (piece != null && piece.Color == playerColor)
                    {
                        for (int endRow = 0; endRow < 8; endRow++)
                        {
                            for (int endCol = 0; endCol < 8; endCol++)
                            {
                                if (IsValidMove(startRow, startCol, endRow, endCol, playerColor))
                                {
                                    legalMoves.Add(new Move(startRow, startCol, endRow, endCol));
                                }
                            }
                        }
                    }
                }
            }
            return legalMoves;
        }

        public int EvaluateBoard()
        {
            int whiteScore = 0;
            int blackScore = 0;

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Piece? piece = GetPiece(r, c);
                    if (piece != null)
                    {
                        int value = GetPieceValue(piece.Type);
                        if (piece.Color == "white")
                        {
                            whiteScore += value;
                        }
                        else
                        {
                            blackScore += value;
                        }
                    }
                }
            }

            // Додавання невеликої оцінки за шах
            int whiteKingRow = -1, whiteKingCol = -1;
            int blackKingRow = -1, blackKingCol = -1;
            FindKingPosition("white", out whiteKingRow, out whiteKingCol);
            FindKingPosition("black", out blackKingRow, out blackKingCol);

            if (whiteKingRow != -1 && IsKingInCheck(whiteKingRow, whiteKingCol, "black"))
            {
                whiteScore -= 50; // Невелика перевага для чорних, якщо білий король під шахом
            }

            if (blackKingRow != -1 && IsKingInCheck(blackKingRow, blackKingCol, "white"))
            {
                blackScore -= 50; // Невелика перевага для білих, якщо чорний король під шахом
            }

            return whiteScore - blackScore;
        }

        private int GetPieceValue(string type)
        {
            return type switch
            {
                "pawn" => 1,
                "knight" => 3,
                "bishop" => 3,
                "rook" => 5,
                "queen" => 9,
                "king" => 1000,
                _ => 0
            };
        }

        private void FindKingPosition(string color, out int row, out int col)
        {
            row = -1;
            col = -1;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (GetPiece(r, c)?.Type == "king" && GetPiece(r, c)?.Color == color)
                    {
                        row = r;
                        col = c;
                        return;
                    }
                }
            }
        }
        public Piece?[,] GetRawPieces()
        {
            return _pieces;
        }

        public BoardCell[,] GetBoardCells()
        {
            BoardCell[,] boardCells = new BoardCell[8, 8];
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    // Determine cell color
                    Brush cellColor = (row + col) % 2 == 0 ? Brushes.White : Brushes.LightGray;
                    boardCells[row, col] = new BoardCell(row, col, cellColor, _pieces[row, col]);
                }
            }
            return boardCells;
        }
    }
}