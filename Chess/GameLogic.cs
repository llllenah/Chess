using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChessTrainer
{
    public class GameLogic
    {
        private Board _board;
        private string _currentPlayer = "white";
        private bool _isComputerMode = false;
        private int _computerDifficulty = 1; // 1: Легкий (випадковий), 2: Середній (один хід вперед)
        public event EventHandler? BoardUpdated;
        public event EventHandler? MoveMade;
        public event EventHandler? GameEnded;
        public Board Board
        {
            get { return _board; }
        }
        public GameLogic()
        {
            _board = new Board();
        }
        public int ComputerDifficulty
        {
            get { return _computerDifficulty; }
            set { _computerDifficulty = value; }
        }

        public ObservableCollection<BoardCell> GetCurrentBoard()
        {
            var boardCells = new ObservableCollection<BoardCell>();
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Piece? piece = _board.GetPiece(row, col);
                    boardCells.Add(new BoardCell(row, col, (row + col) % 2 == 0 ? Brushes.LightGray : Brushes.White, piece)); // Передаємо об'єкт Piece
                }
            }
            return boardCells;
        }

        public bool TryMovePiece(int startRow, int startCol, int endRow, int endCol)
        {

            if (_board.IsValidMove(startRow, startCol, endRow, endCol, _currentPlayer))
            {
                
                Piece? movedPiece = _board.GetPiece(startRow, startCol);
                Piece? capturedPiece = _board.GetPiece(endRow, endCol);
                _board.MovePiece(startRow, startCol, endRow, endCol);
                string moveNotation = GetMoveNotation(movedPiece, startCol, startRow, endCol, endRow, capturedPiece);
                MoveMade?.Invoke(this, EventArgs.Empty);
                // Перевірка на підвищення пішака
                if (movedPiece?.Type == "pawn" &&
                    ((movedPiece.Color == "white" && startRow == 1 && endRow == 0) || // Білий пішак з 2-ї на 8-у
                     (movedPiece.Color == "black" && startRow == 6 && endRow == 7))) // Чорний пішак з 7-ї на 1-у
                {
                    // Тут потрібно запропонувати гравцеві вибрати фігуру для підвищення
                    // Для прикладу, автоматично підвищуємо до ферзя
                    string promotionPieceType = "queen"; // За замовчуванням - ферзь

                    _board.SetPiece(endRow, endCol, new Piece(movedPiece.Color, promotionPieceType));

                    // Оновити UI після підвищення
                    BoardUpdated?.Invoke(this, EventArgs.Empty);
                }

                string opponentColor = (_currentPlayer == "white") ? "black" : "white";
                int opponentKingRow = -1;
                int opponentKingCol = -1;
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        if (_board.GetPiece(r, c)?.Color == opponentColor && _board.GetPiece(r, c)?.Type == "king")
                        {
                            opponentKingRow = r;
                            opponentKingCol = c;
                            break;
                        }
                    }
                    if (opponentKingRow != -1) break;
                }

                if (opponentKingRow != -1 && _board.IsKingInCheck(opponentKingRow, opponentKingCol, _currentPlayer))
                {
                    Console.WriteLine($"{(_currentPlayer == "white" ? "Чорному" : "Білому")} шах!");
                }

                if (capturedPiece != null && capturedPiece.Type == "king")
                {
                    GameEnded?.Invoke(this, EventArgs.Empty); // Використовуємо EventArgs.Empty
                    return true; // Гра закінчена
                }

                if (_board.GetAllPossibleMovesForPlayer(opponentColor).Count == 0)
                {
                    if (_board.IsKingInCheck(opponentKingRow, opponentKingCol, _currentPlayer))
                    {
                        GameEnded?.Invoke(this, EventArgs.Empty); // Використовуємо EventArgs.Empty
                    }
                    else
                    {
                        GameEnded?.Invoke(this, EventArgs.Empty); // Використовуємо EventArgs.Empty
                    }
                    return true; // Гра закінчена через мат або пат
                }

                SwitchPlayer();
                BoardUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            return false;
        }

        private void MakeComputerMove()
        {
            var possibleMoves = _board.GetAllPossibleMovesForPlayer("black");
            if (possibleMoves.Any())
            {
                Move? selectedMove = null;
                var random = new System.Random();

                if (_computerDifficulty == 1) // Легкий рівень: випадковий хід
                {
                    selectedMove = possibleMoves[random.Next(possibleMoves.Count)];
                }
                else if (_computerDifficulty == 2) // Середній рівень: Minimax глибини 1
                {
                    selectedMove = GetBestMoveMinimax(possibleMoves, 1, false); // false тому що оцінюємо з точки зору білих на наступному ході
                }
                // Для складного рівня потрібно буде реалізувати Minimax з більшою глибиною

                if (selectedMove != null)
                {
                    Piece? movedPiece = _board.GetPiece(selectedMove.StartRow, selectedMove.StartCol);
                    Piece? capturedPiece = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);
                    string moveNotation = GetMoveNotation(movedPiece, selectedMove.StartCol, selectedMove.StartRow, selectedMove.EndCol, selectedMove.EndRow, capturedPiece);
                    _board.MovePiece(selectedMove.StartRow, selectedMove.StartCol, selectedMove.EndRow, selectedMove.EndCol);
                    MoveMade?.Invoke(this, EventArgs.Empty);
                    SwitchPlayer();
                    BoardUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private Move? GetBestMoveMinimax(System.Collections.Generic.List<Move> possibleMoves, int depth, bool maximizingPlayer)
        {
            if (depth == 0 || IsGameOver())
            {
                return new Move(-1, -1, -1, -1) { Score = _board.EvaluateBoard() };
            }

            if (maximizingPlayer) // Білі намагаються максимізувати рахунок
            {
                int maxEval = int.MinValue;
                Move? bestMove = null;
                foreach (var move in possibleMoves)
                {
                    Board tempBoard = new Board(_board.GetPieces());
                    tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);
                    GameLogic tempGameLogic = new GameLogic { _board = tempBoard }; // Створюємо тимчасову GameLogic для оцінки
                    var nextPossibleMoves = tempBoard.GetAllPossibleMovesForPlayer("white");
                    var result = tempGameLogic.GetBestMoveMinimax(nextPossibleMoves, depth - 1, false);
                    if (result != null)
                    {
                        if (result.Score > maxEval)
                        {
                            maxEval = result.Score;
                            bestMove = move;
                        }
                    }
                }
                return bestMove;
            }
            else // Чорні (комп'ютер) намагаються мінімізувати рахунок
            {
                int minEval = int.MaxValue;
                Move? bestMove = null;
                foreach (var move in possibleMoves)
                {
                    Board tempBoard = new Board(_board.GetPieces());
                    tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);
                    GameLogic tempGameLogic = new GameLogic { _board = tempBoard }; // Створюємо тимчасову GameLogic для оцінки
                    var nextPossibleMoves = tempBoard.GetAllPossibleMovesForPlayer("black");
                    var result = tempGameLogic.GetBestMoveMinimax(nextPossibleMoves, depth - 1, true);
                    if (result != null)
                    {
                        if (result.Score < minEval)
                        {
                            minEval = result.Score;
                            bestMove = move;
                        }
                    }
                }
                return bestMove;
            }
        }

        private bool IsGameOver()
        {
            string opponentColor = (_currentPlayer == "white") ? "black" : "white";
            int opponentKingRow = -1;
            int opponentKingCol = -1;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (_board.GetPiece(r, c)?.Color == opponentColor && _board.GetPiece(r, c)?.Type == "king")
                    {
                        opponentKingRow = r;
                        opponentKingCol = c;
                        break;
                    }
                }
                if (opponentKingRow != -1) break;
            }

            if (opponentKingRow != -1 && _board.GetAllPossibleMovesForPlayer(opponentColor).Count == 0)
            {
                return true;
            }
            return false;
        }

        public string GetCurrentPlayer()
        {
            return _currentPlayer;
        }

        public void SetComputerMode(bool isComputerMode)
        {
            _isComputerMode = isComputerMode;
            _currentPlayer = "white"; // Починає завжди білий гравець
            if (_isComputerMode && _currentPlayer == "black")
            {
                MakeComputerMove(); // Викликаємо синхронно
            }
        }

        private void SwitchPlayer()
        {
            _currentPlayer = _currentPlayer == "white" ? "black" : "white";
            if (_isComputerMode && _currentPlayer == "black")
            {
                MakeComputerMove(); // Викликаємо синхронно
            }
        }


        private string GetMoveNotation(Piece? piece, int startCol, int startRow, int endCol, int endRow, Piece? capturedPiece)
        {
            if (piece == null) return "";

            string pieceNotation = piece.Type switch
            {
                "knight" => "N",
                "bishop" => "B",
                "rook" => "R",
                "queen" => "Q",
                "king" => "K",
                _ => ""
            };

            string capture = capturedPiece != null ? "x" : "";
            string startFile = ((char)('a' + startCol)).ToString();
            string startRank = (8 - startRow).ToString();
            string endFile = ((char)('a' + endCol)).ToString();
            string endRank = (8 - endRow).ToString();

            if (piece.Type == "pawn" && capture != "")
            {
                return $"{startFile}{capture}{endFile}{endRank}";
            }

            return $"{pieceNotation}{startFile}{startRank}{capture}{endFile}{endRank}";
        }


        private string? GetPieceSymbol(Piece? piece)
        {
            if (piece == null) return null;
            return piece.Color == "white" ? GetWhiteSymbol(piece.Type) : GetBlackSymbol(piece.Type);
        }

        private string GetWhiteSymbol(string type)
        {
            return type switch
            {
                "pawn" => "♙",
                "rook" => "♖",
                "knight" => "♘",
                "bishop" => "♗",
                "queen" => "♕",
                "king" => "♔",
                _ => ""
            };
        }

        private string GetBlackSymbol(string type)
        {
            return type switch
            {
                "pawn" => "♟",
                "rook" => "♜",
                "knight" => "♞",
                "bishop" => "♝",
                "queen" => "♛",
                "king" => "♚",
                _ => ""
            };
        }
    }
}