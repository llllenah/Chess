using System.Collections.Generic;
using System.Linq;

namespace ChessTrainer
{
    public class Board
    {
        private Piece?[,] _pieces = new Piece[8, 8];

        public Board()
        {
            InitializeBoard();
        }

        public Piece? GetPiece(int row, int col)
        {
            if (IsValidPosition(row, col))
            {
                return _pieces[row, col];
            }
            return null;
        }

        public void SetPiece(int row, int col, Piece? piece)
        {
            if (IsValidPosition(row, col))
            {
                _pieces[row, col] = piece;
            }
        }

        public bool IsValidMove(int startRow, int startCol, int endRow, int endCol, string currentPlayer)
        {
            Piece? piece = GetPiece(startRow, startCol);

            if (piece == null || piece.Color != currentPlayer)
            {
                return false; // На клітинці немає фігури поточного гравця
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

            switch (piece.Type)
            {
                case "pawn":
                    return IsValidPawnMove(startRow, startCol, endRow, endCol, currentPlayer, targetPiece);
                case "rook":
                    return IsValidRookMove(startRow, startCol, endRow, endCol);
                case "knight":
                    return IsValidKnightMove(rowDifference, colDifference);
                case "bishop":
                    return IsValidBishopMove(startRow, startCol, endRow, endCol);
                case "queen":
                    return IsValidQueenMove(startRow, startCol, endRow, endCol);
                case "king":
                    return IsValidKingMove(absRowDifference, absColDifference);
                default:
                    return false;
            }
        }

        public void MovePiece(int startRow, int startCol, int endRow, int endCol)
        {
            Piece? pieceToMove = GetPiece(startRow, startCol);
            SetPiece(startRow, startCol, null);
            SetPiece(endRow, endCol, pieceToMove);
        }

        private bool IsValidPosition(int row, int col)
        {
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }

        private void InitializeBoard()
        {
            // Розстановка початкових фігур на дошці
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
                _pieces[6, i] = new Piece("white", "pawn");
            }
            _pieces[7, 0] = new Piece("white", "rook");
            _pieces[7, 1] = new Piece("white", "knight");
            _pieces[7, 2] = new Piece("white", "bishop");
            _pieces[7, 3] = new Piece("white", "queen");
            _pieces[7, 4] = new Piece("white", "king");
            _pieces[7, 5] = new Piece("white", "bishop");
            _pieces[7, 6] = new Piece("white", "knight");
            _pieces[7, 7] = new Piece("white", "rook");
        }

        private bool IsValidPawnMove(int startRow, int startCol, int endRow, int endCol, string currentPlayer, Piece? targetPiece)
        {
            int rowDifference = endRow - startRow;
            int colDifference = endCol - startCol;

            if (currentPlayer == "white")
            {
                if (colDifference == 0 && rowDifference == -1 && targetPiece == null)
                {
                    return true; // Хід на одну клітинку вперед
                }
                if (startRow == 6 && colDifference == 0 && rowDifference == -2 && targetPiece == null && GetPiece(startRow - 1, startCol) == null)
                {
                    return true; // Хід на дві клітинки вперед з початкової позиції
                }
                if (Math.Abs(colDifference) == 1 && rowDifference == -1 && targetPiece != null && targetPiece.Color == "black")
                {
                    return true; // Взяття по діагоналі
                }
            }
            else // currentPlayer == "black"
            {
                if (colDifference == 0 && rowDifference == 1 && targetPiece == null)
                {
                    return true; // Хід на одну клітинку вперед
                }
                if (startRow == 1 && colDifference == 0 && rowDifference == 2 && targetPiece == null && GetPiece(startRow + 1, startCol) == null)
                {
                    return true; // Хід на дві клітинки вперед з початкової позиції
                }
                if (Math.Abs(colDifference) == 1 && rowDifference == 1 && targetPiece != null && targetPiece.Color == "white")
                {
                    return true; // Взяття по діагоналі
                }
            }
            return false;
        }

        private bool IsValidRookMove(int startRow, int startCol, int endRow, int endCol)
        {
            if (startRow == endRow) // Горизонтальний рух
            {
                int step = Math.Sign(endCol - startCol);
                for (int col = startCol + step; col != endCol; col += step)
                {
                    if (GetPiece(startRow, col) != null)
                    {
                        return false; // Є фігура на шляху
                    }
                }
                return true;
            }
            if (startCol == endCol) // Вертикальний рух
            {
                int step = Math.Sign(endRow - startRow);
                for (int row = startRow + step; row != endRow; row += step)
                {
                    if (GetPiece(row, startCol) != null)
                    {
                        return false; // Є фігура на шляху
                    }
                }
                return true;
            }
            return false;
        }

        private bool IsValidKnightMove(int rowDifference, int colDifference)
        {
            return (Math.Abs(rowDifference) == 2 && Math.Abs(colDifference) == 1) || (Math.Abs(rowDifference) == 1 && Math.Abs(colDifference) == 2);
        }

        private bool IsValidBishopMove(int startRow, int startCol, int endRow, int endCol)
        {
            if (Math.Abs(endRow - startRow) != Math.Abs(endCol - startCol))
            {
                return false; // Не діагональний хід
            }

            int rowStep = Math.Sign(endRow - startRow);
            int colStep = Math.Sign(endCol - startCol);
            int row = startRow + rowStep;
            int col = startCol + colStep;

            while (row != endRow)
            {
                if (GetPiece(row, col) != null)
                {
                    return false; // Є фігура на шляху
                }
                row += rowStep;
                col += colStep;
            }
            return true;
        }

        private bool IsValidQueenMove(int startRow, int startCol, int endRow, int endCol)
        {
            return IsValidRookMove(startRow, startCol, endRow, endCol) || IsValidBishopMove(startRow, startCol, endRow, endCol);
        }

        private bool IsValidKingMove(int absRowDifference, int absColDifference)
        {
            return absRowDifference <= 1 && absColDifference <= 1;
        }
    }
        public class Piece
        {
            public string Color { get; }
            public string Type { get; }

            public Piece(string color, string type)
            {
                Color = color;
                Type = type;
            }
        }
    }