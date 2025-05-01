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

        public Piece?[,] GetPieces() // Зверніть увагу на ключове слово public
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

            // Перевірка правил руху для кожної фігури (як було раніше)...
            bool isMoveValidAccordingToPieceRules = false;
            switch (piece.Type)
            {
                case "pawn":
                    isMoveValidAccordingToPieceRules = IsValidPawnMove(startRow, startCol, endRow, endCol, currentPlayer, targetPiece);
                    break;
                case "rook":
                    isMoveValidAccordingToPieceRules = IsValidRookMove(startRow, startCol, endRow, endCol);
                    break;
                case "knight":
                    isMoveValidAccordingToPieceRules = IsValidKnightMove(rowDifference, colDifference);
                    break;
                case "bishop":
                    isMoveValidAccordingToPieceRules = IsValidBishopMove(startRow, startCol, endRow, endCol);
                    break;
                case "queen":
                    isMoveValidAccordingToPieceRules = IsValidQueenMove(startRow, startCol, endRow, endCol);
                    break;
                case "king":
                    isMoveValidAccordingToPieceRules = IsValidKingMove(absRowDifference, absColDifference);
                    break;
                default:
                    return false;
            }

            if (isMoveValidAccordingToPieceRules)
            {
                // **ТЕПЕР ПЕРЕВІРЯЄМО, ЧИ НЕ СТАВИТЬ ЦЕЙ ХІД ВЛАСНОГО КОРОЛЯ ПІД ШАХ**
                Board tempBoard = new Board(GetPieces()); // Створюємо копію поточної дошки
                tempBoard.MovePiece(startRow, startCol, endRow, endCol); // Виконуємо хід на копії

                // Знаходимо позицію короля поточного гравця на тимчасовій дошці
                int kingRow = -1;
                int kingCol = -1;
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        Piece? tempPiece = tempBoard.GetPiece(r, c);
                        if (tempPiece != null && tempPiece.Color == currentPlayer && tempPiece.Type == "king")
                        {
                            kingRow = r;
                            kingCol = c;
                            break;
                        }
                    }
                    if (kingRow != -1) break;
                }

                string opponentColor = (currentPlayer == "white") ? "black" : "white";
                if (kingRow != -1 && tempBoard.IsKingInCheck(kingRow, kingCol, opponentColor))
                {
                    return false; // Хід залишає власного короля під шахом
                }
                return true; // Хід легальний і не ставить власного короля під шах
            }

            return false;
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

        public bool IsKingInCheck(int kingRow, int kingCol, string attackingColor)
        {
            Console.WriteLine($"Перевірка, чи король ({kingRow}, {kingCol}) під шахом від {attackingColor}");

            // Перевірка атаки від пішаків
            if (IsPawnAttackingKing(kingRow, kingCol, attackingColor))
            {
                Console.WriteLine($"  Атакований пішаком {attackingColor}");
                return true;
            }

            // Перевірка атаки від коней
            if (IsKnightAttackingKing(kingRow, kingCol, attackingColor))
            {
                Console.WriteLine($"  Атакований конем {attackingColor}");
                return true;
            }

            // Перевірка атаки від тур
            if (IsRookAttackingKing(kingRow, kingCol, attackingColor))
            {
                Console.WriteLine($"  Атакований турою {attackingColor}");
                return true;
            }

            // Перевірка атаки від слонів
            if (IsBishopAttackingKing(kingRow, kingCol, attackingColor))
            {
                Console.WriteLine($"  Атакований слоном {attackingColor}");
                return true;
            }

            // Перевірка атаки від ферзя
            if (IsQueenAttackingKing(kingRow, kingCol, attackingColor))
            {
                Console.WriteLine($"  Атакований ферзем {attackingColor}");
                return true;
            }

            // Перевірка атаки від короля
            if (IsKingAttackingKing(kingRow, kingCol, attackingColor))
            {
                Console.WriteLine($"  Атакований королем {attackingColor} (це не повинно блокувати хід)");
                return true;
            }

            Console.WriteLine($"  Король ({kingRow}, {kingCol}) не під шахом від {attackingColor}");
            return false;
        }
        private bool IsPawnAttackingKing(int kingRow, int kingCol, string attackingColor)
        {
            int pawnRowOffset = (attackingColor == "white") ? -1 : 1;
            int[] colOffsets = { -1, 1 };

            foreach (int colOffset in colOffsets)
            {
                int pawnRow = kingRow + pawnRowOffset;
                int pawnCol = kingCol + colOffset;

                if (IsValidPosition(pawnRow, pawnCol))
                {
                    Piece? piece = GetPiece(pawnRow, pawnCol);
                    if (piece != null && piece.Color == attackingColor && piece.Type == "pawn")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsKnightAttackingKing(int kingRow, int kingCol, string attackingColor)
        {
            int[,] knightMoves = { { -2, -1 }, { -2, 1 }, { -1, -2 }, { -1, 2 }, { 1, -2 }, { 1, 2 }, { 2, -1 }, { 2, 1 } };

            for (int i = 0; i < 8; i++)
            {
                int knightRow = kingRow + knightMoves[i, 0];
                int knightCol = kingCol + knightMoves[i, 1];

                if (IsValidPosition(knightRow, knightCol))
                {
                    Piece? piece = GetPiece(knightRow, knightCol);
                    if (piece != null && piece.Color == attackingColor && piece.Type == "knight")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsRookAttackingKing(int kingRow, int kingCol, string attackingColor)
        {
            int[] rowDirections = { -1, 1, 0, 0 }; // Вгору, вниз
            int[] colDirections = { 0, 0, -1, 1 }; // Вліво, вправо

            for (int i = 0; i < 4; i++)
            {
                int currentRow = kingRow + rowDirections[i];
                int currentCol = kingCol + colDirections[i];

                while (IsValidPosition(currentRow, currentCol))
                {
                    Piece? piece = GetPiece(currentRow, currentCol);
                    if (piece != null)
                    {
                        if (piece.Color == attackingColor && piece.Type == "rook")
                        {
                            return true;
                        }
                        break; // Зустріли іншу фігуру, далі в цьому напрямку немає сенсу перевіряти
                    }
                    currentRow += rowDirections[i];
                    currentCol += colDirections[i];
                }
            }
            return false;
        }

        private bool IsBishopAttackingKing(int kingRow, int kingCol, string attackingColor)
        {
            int[] rowDirections = { -1, -1, 1, 1 }; // Діагоналі
            int[] colDirections = { -1, 1, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                int currentRow = kingRow + rowDirections[i];
                int currentCol = kingCol + colDirections[i];

                while (IsValidPosition(currentRow, currentCol))
                {
                    Piece? piece = GetPiece(currentRow, currentCol);
                    if (piece != null)
                    {
                        if (piece.Color == attackingColor && piece.Type == "bishop")
                        {
                            return true;
                        }
                        break; // Зустріли іншу фігуру
                    }
                    currentRow += rowDirections[i];
                    currentCol += colDirections[i];
                }
            }
            return false;
        }

        private bool IsQueenAttackingKing(int kingRow, int kingCol, string attackingColor)
        {
            return IsRookAttackingKing(kingRow, kingCol, attackingColor) || IsBishopAttackingKing(kingRow, kingCol, attackingColor);
        }
        private bool IsKingAttackingKing(int kingRow, int kingCol, string attackingColor)
        {
            for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (int colOffset = -1; colOffset <= 1; colOffset++)
                {
                    if (rowOffset == 0 && colOffset == 0) continue; // Не перевіряємо саму клітинку короля

                    int attackingKingRow = kingRow + rowOffset;
                    int attackingKingCol = kingCol + colOffset;

                    if (IsValidPosition(attackingKingRow, attackingKingCol))
                    {
                        Piece? piece = GetPiece(attackingKingRow, attackingKingCol);
                        if (piece != null && piece.Color == attackingColor && piece.Type == "king")
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public List<Move> GetAllPossibleMovesForPlayer(string playerColor)
        {
            List<Move> legalMoves = new List<Move>();

            // Знайдемо позицію короля поточного гравця
            int kingRow = -1;
            int kingCol = -1;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Piece? piece = GetPiece(r, c);
                    if (piece != null && piece.Color == playerColor && piece.Type == "king")
                    {
                        kingRow = r;
                        kingCol = c;
                        break;
                    }
                }
                if (kingRow != -1) break;
            }

            if (kingRow == -1)
            {
                return legalMoves; // Не знайшли короля, щось пішло не так
            }

            // Перебираємо всі фігури поточного гравця
            for (int startRow = 0; startRow < 8; startRow++)
            {
                for (int startCol = 0; startCol < 8; startCol++)
                {
                    Piece? piece = GetPiece(startRow, startCol);
                    if (piece != null && piece.Color == playerColor)
                    {
                        // Генеруємо всі можливі ходи для цієї фігури
                        for (int endRow = 0; endRow < 8; endRow++)
                        {
                            for (int endCol = 0; endCol < 8; endCol++)
                            {
                                // Перевіряємо, чи є цей хід взагалі допустимим з точки зору правил фігури
                                if (IsValidMove(startRow, startCol, endRow, endCol, playerColor))
                                {
                                    // Тепер нам потрібно перевірити, чи не залишає цей хід власного короля під шахом

                                    // Створюємо тимчасову копію дошки
                                    Piece?[,] tempPieces = CloneBoard();

                                    // Робимо хід на тимчасовій дошці
                                    Piece? movedPiece = tempPieces[startRow, startCol];
                                    tempPieces[startRow, startCol] = null;
                                    tempPieces[endRow, endCol] = movedPiece;

                                    // Знаходимо позицію короля на тимчасовій дошці (може змінитися після рокіровки, але поки її немає)
                                    int tempKingRow = kingRow;
                                    int tempKingCol = kingCol;
                                    if (piece.Type == "king")
                                    {
                                        tempKingRow = endRow;
                                        tempKingCol = endCol;
                                    }

                                    // Перевіряємо, чи не знаходиться король під шахом на тимчасовій дошці
                                    Board tempBoard = new Board(tempPieces);
                                    string opponentColor = (playerColor == "white") ? "black" : "white";
                                    if (!tempBoard.IsKingInCheck(tempKingRow, tempKingCol, opponentColor))
                                    {
                                        // Якщо хід не залишає короля під шахом, він є легальним
                                        legalMoves.Add(new Move(startRow, startCol, endRow, endCol));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return legalMoves;
        }

        // Допоміжний метод для створення копії дошки
        private Piece?[,] CloneBoard()
        {
            Piece?[,] tempPieces = new Piece[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    tempPieces[i, j] = _pieces[i, j]?.Clone(); // Припускаємо, що клас Piece має метод Clone()
                }
            }
            return tempPieces;
        }
        public int EvaluateBoard()
        {
            int whiteScore = 0;
            int blackScore = 0;

            // Оцінка вартості фігур
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

            // Перевірка на шах для обох королів
            int whiteKingRow = -1, whiteKingCol = -1;
            int blackKingRow = -1, blackKingCol = -1;

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (GetPiece(r, c)?.Color == "white" && GetPiece(r, c)?.Type == "king")
                    {
                        whiteKingRow = r;
                        whiteKingCol = c;
                    }
                    if (GetPiece(r, c)?.Color == "black" && GetPiece(r, c)?.Type == "king")
                    {
                        blackKingRow = r;
                        blackKingCol = c;
                    }
                }
            }

            if (whiteKingRow != -1 && IsKingInCheck(whiteKingRow, whiteKingCol, "black"))
            {
                whiteScore -= 500; // Значний штраф за шах білому королю
            }

            if (blackKingRow != -1 && IsKingInCheck(blackKingRow, blackKingCol, "white"))
            {
                blackScore -= 500; // Значний штраф за шах чорному королю
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

        public Board(Piece?[,] pieces)
        {
            _pieces = new Piece[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    _pieces[i, j] = pieces[i, j]?.Clone();
                }
            }
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

        public Piece Clone()
        {
            return new Piece(this.Color, this.Type);
        }
    }

}