using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChessTrainer
{
    public class GameLogic
    {
        private Board _board;
        private string _currentPlayer = "white";
        private bool _isComputerMode = false;
        private bool _playerPlaysBlack = false; // Додаємо можливість грати за чорних

        public event EventHandler? BoardUpdated;
        public event EventHandler? MoveMade;
        public event EventHandler? GameEnded;

        public Board Board => _board;

        // Публічна властивість для доступу до поточного гравця
        public string CurrentPlayer => _currentPlayer;

        // Властивість для керування стороною гравця
        public bool PlayerPlaysBlack
        {
            get => _playerPlaysBlack;
            set => _playerPlaysBlack = value;
        }

        public enum ComputerDifficulty
        {
            Random = 1,       // Случайные ходы
            Easy = 2,         // Minimax глубина 1
            Medium = 3,       // Alpha-Beta глубина 2
            Hard = 4,         // Alpha-Beta глубина 3 с параллельным поиском
            Expert = 5        // Alpha-Beta глубина 4 с параллельным поиском
        }
        public ComputerDifficulty CurrentDifficulty { get; set; } = ComputerDifficulty.Medium;
        public GameLogic()
        {
            _board = new Board();
            InitializeGame(); // Викликаємо метод ініціалізації при створенні об'єкта GameLogic
        }

        public void InitializeGame()
        {
            _board.InitializeBoard();
            _currentPlayer = "white"; // Шахи завжди починаються з білих
            BoardUpdated?.Invoke(this, EventArgs.Empty);

            // Якщо гравець грає за чорних і комп'ютер гратиме за білих, комп'ютер робить перший хід
            if (_isComputerMode && _playerPlaysBlack && _currentPlayer == "white")
            {
                MakeComputerMove();
            }
        }

        public void LoadGame(Piece?[,] boardState, string currentPlayer)
        {
            _board = new Board(boardState);
            _currentPlayer = currentPlayer;
            BoardUpdated?.Invoke(this, EventArgs.Empty); // Оновлюємо UI після завантаження
        }

        public ObservableCollection<BoardCell> GetCurrentBoard()
        {
            var boardCells = new ObservableCollection<BoardCell>();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    int displayRow = _playerPlaysBlack ? 7 - row : row;
                    int displayCol = _playerPlaysBlack ? 7 - col : col;

                    Piece? piece = _board.GetPiece(row, col);

                    boardCells.Add(new BoardCell(
                        row,
                        col,
                        (row + col) % 2 == 0 ? Brushes.LightGray : Brushes.White,
                        piece));
                }
            }
            return boardCells;
        }


        public bool TryMovePiece(int startRow, int startCol, int endRow, int endCol)
        {
            // Перевіряємо, чи це хід гравця, а не комп'ютера
            bool isPlayerTurn;
            if (_isComputerMode)
            {
                isPlayerTurn = (_playerPlaysBlack && _currentPlayer == "black") ||
                               (!_playerPlaysBlack && _currentPlayer == "white");

                if (!isPlayerTurn)
                {
                    return false; // Не дозволяємо гравцю ходити за комп'ютера
                }
            }

            if (_board.IsValidMove(startRow, startCol, endRow, endCol, _currentPlayer))
            {
                Piece? movedPiece = _board.GetPiece(startRow, startCol);
                Piece? capturedPiece = _board.GetPiece(endRow, endCol);
                _board.MovePiece(startRow, startCol, endRow, endCol);
                string moveNotation = GetMoveNotation(movedPiece, startCol, startRow, endCol, endRow, capturedPiece);
                MoveMade?.Invoke(this, EventArgs.Empty);

                // Перевірка на підвищення пішака
                if (movedPiece?.Type == "pawn" &&
                    ((movedPiece.Color == "white" && endRow == 0) || // Білий пішак досягає верхнього краю
                     (movedPiece.Color == "black" && endRow == 7)))  // Чорний пішак досягає нижнього краю
                {
                    // Автоматично перетворюємо на ферзя (може бути змінено на діалог вибору)
                    _board.SetPiece(endRow, endCol, new Piece(movedPiece.Color, "queen"));
                    BoardUpdated?.Invoke(this, EventArgs.Empty);
                }

                // Визначаємо колір опонента для перевірки шаху/мату
                string opponentColor = (_currentPlayer == "white") ? "black" : "white";

                // Знаходимо позицію короля опонента
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

                // Перевіряємо, чи був зроблений шах
                if (opponentKingRow != -1 && _board.IsKingInCheck(opponentKingRow, opponentKingCol, _currentPlayer))
                {
                    Console.WriteLine($"{(_currentPlayer == "white" ? "Чорному" : "Білому")} шах!");
                }

                // Перевіряємо, чи був захоплений король (гра закінчена)
                if (capturedPiece != null && capturedPiece.Type == "king")
                {
                    GameEnded?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                // Перевіряємо на мат або пат
                if (_board.GetAllPossibleMovesForPlayer(opponentColor).Count == 0)
                {
                    if (_board.IsKingInCheck(opponentKingRow, opponentKingCol, _currentPlayer))
                    {
                        // Мат
                        GameEnded?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        // Пат
                        GameEnded?.Invoke(this, EventArgs.Empty);
                    }
                    return true;
                }

                // Перемикаємо гравця і викликаємо хід комп'ютера, якщо необхідно
                SwitchPlayer();
                BoardUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Основной метод для получения лучшего хода с автоматическим выбором алгоритма
        /// </summary>
        public Move? GetBestMove(List<Move> possibleMoves, int difficulty, bool maximizingPlayer)
        {
            // Если нет возможных ходов
            if (possibleMoves.Count == 0)
                return null;

            // Для легкого уровня просто возвращаем случайный ход
            if (difficulty <= 1)
            {
                var random = new System.Random();
                return possibleMoves[random.Next(possibleMoves.Count)];
            }

            // Определяем глубину в зависимости от сложности
            int depth = difficulty;

            if (difficulty >= 2)
            {
                // Для средней сложности используем альфа-бета отсечение
                return GetBestMoveAlphaBeta(possibleMoves, depth, maximizingPlayer);
            }
            else
            {
                // Для низкой сложности используем обычный минимакс
                return GetBestMoveMinimax(possibleMoves, depth, maximizingPlayer);
            }
        }
        
        // Метод с альфа-бета отсечением
        private Move? GetBestMoveAlphaBeta(List<Move> possibleMoves, int depth, bool maximizingPlayer,
                                          int alpha = int.MinValue, int beta = int.MaxValue)
        {
            if (depth == 0 || IsGameOver())
            {
                return new Move(-1, -1, -1, -1) { Score = _board.EvaluateBoard() };
            }

            Move? bestMove = null;

            if (maximizingPlayer) // Белые максимизируют счет
            {
                int maxEval = int.MinValue;
                foreach (var move in possibleMoves)
                {
                    // Создаем копию доски для анализа
                    Board tempBoard = new Board(_board.GetPieces());
                    tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                    // Рекурсивная оценка хода
                    GameLogic tempGameLogic = new GameLogic { _board = tempBoard };
                    var nextPossibleMoves = tempBoard.GetAllPossibleMovesForPlayer("black");
                    var result = tempGameLogic.GetBestMoveAlphaBeta(nextPossibleMoves, depth - 1, false, alpha, beta);

                    if (result != null)
                    {
                        move.Score = result.Score;
                        if (result.Score > maxEval)
                        {
                            maxEval = result.Score;
                            bestMove = move;
                        }

                        // Обновляем альфа и проверяем на отсечение
                        alpha = Math.Max(alpha, maxEval);
                        if (beta <= alpha)
                            break; // Бета-отсечение
                    }
                }
            }
            else // Черные минимизируют счет
            {
                int minEval = int.MaxValue;
                foreach (var move in possibleMoves)
                {
                    Board tempBoard = new Board(_board.GetPieces());
                    tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                    GameLogic tempGameLogic = new GameLogic { _board = tempBoard };
                    var nextPossibleMoves = tempBoard.GetAllPossibleMovesForPlayer("white");
                    var result = tempGameLogic.GetBestMoveAlphaBeta(nextPossibleMoves, depth - 1, true, alpha, beta);

                    if (result != null)
                    {
                        move.Score = result.Score;
                        if (result.Score < minEval)
                        {
                            minEval = result.Score;
                            bestMove = move;
                        }

                        // Обновляем бету и проверяем на отсечение
                        beta = Math.Min(beta, minEval);
                        if (beta <= alpha)
                            break; // Альфа-отсечение
                    }
                }
            }

            return bestMove;
        }

        public void MakeComputerMove()
        {
            // Определяем цвет фигур компьютера в зависимости от того, за кого играет человек
            string computerColor = _playerPlaysBlack ? "white" : "black";

            // Проверяем, сейчас ли ход компьютера
            if ((_playerPlaysBlack && _currentPlayer == "white") ||
                (!_playerPlaysBlack && _currentPlayer == "black"))
            {
                var possibleMoves = _board.GetAllPossibleMovesForPlayer(computerColor);
                if (possibleMoves.Any())
                {
                    // Используем универсальный метод для выбора хода в зависимости от сложности
                    Move? selectedMove = GetBestMove(
                        possibleMoves,
                        (int)CurrentDifficulty,
                        computerColor == "white"
                    );

                    if (selectedMove != null)
                    {
                        Piece? movedPiece = _board.GetPiece(selectedMove.StartRow, selectedMove.StartCol);
                        Piece? capturedPiece = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);
                        string moveNotation = GetMoveNotation(movedPiece, selectedMove.StartCol, selectedMove.StartRow, selectedMove.EndCol, selectedMove.EndRow, capturedPiece);

                        _board.MovePiece(selectedMove.StartRow, selectedMove.StartCol, selectedMove.EndRow, selectedMove.EndCol);

                        // Проверка на превращение пешки компьютера
                        if (movedPiece?.Type == "pawn" &&
                            ((movedPiece.Color == "white" && selectedMove.EndRow == 0) ||
                             (movedPiece.Color == "black" && selectedMove.EndRow == 7)))
                        {
                            _board.SetPiece(selectedMove.EndRow, selectedMove.EndCol, new Piece(movedPiece.Color, "queen"));
                        }

                        MoveMade?.Invoke(this, EventArgs.Empty);
                        SwitchPlayer(); // Переключаем игрока, но БЕЗ рекурсивного вызова MakeComputerMove
                        BoardUpdated?.Invoke(this, EventArgs.Empty);

                        // Проверяем окончание игры после хода компьютера
                        CheckGameEnd(computerColor);
                    }
                }
            }
        }

        /// <summary>
        /// Дополнительные уровни сложности для расширенной версии игры
        /// </summary>
        

        private void CheckGameEnd(string lastMoveColor)
        {
            string opponentColor = (lastMoveColor == "white") ? "black" : "white";

            // Знаходимо короля опонента
            int kingRow = -1, kingCol = -1;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (_board.GetPiece(r, c)?.Type == "king" && _board.GetPiece(r, c)?.Color == opponentColor)
                    {
                        kingRow = r;
                        kingCol = c;
                        break;
                    }
                }
                if (kingRow != -1) break;
            }

            // Якщо немає можливих ходів і король під шахом - мат
            if (kingRow != -1 && _board.GetAllPossibleMovesForPlayer(opponentColor).Count == 0)
            {
                if (_board.IsKingInCheck(kingRow, kingCol, lastMoveColor))
                {
                    GameEnded?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Пат
                    GameEnded?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private Move? GetBestMoveMinimax(List<Move> possibleMoves, int depth, bool maximizingPlayer)
        {
            if (depth == 0 || IsGameOver())
            {
                return new Move(-1, -1, -1, -1) { Score = _board.EvaluateBoard() };
            }

            Move? bestMove = null;
            if (maximizingPlayer) // Білі намагаються максимізувати рахунок
            {
                int maxEval = int.MinValue;
                foreach (var move in possibleMoves)
                {
                    // Створюємо копію дошки для аналізу без зміни оригінальної
                    Board tempBoard = new Board(_board.GetPieces());
                    tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                    // Рекурсивно оцінюємо цей хід
                    GameLogic tempGameLogic = new GameLogic { _board = tempBoard };
                    var nextPossibleMoves = tempBoard.GetAllPossibleMovesForPlayer("black");
                    var result = tempGameLogic.GetBestMoveMinimax(nextPossibleMoves, depth - 1, false);

                    if (result != null)
                    {
                        move.Score = result.Score;
                        if (result.Score > maxEval)
                        {
                            maxEval = result.Score;
                            bestMove = move;
                        }
                    }
                }
            }
            else // Чорні намагаються мінімізувати рахунок
            {
                int minEval = int.MaxValue;
                foreach (var move in possibleMoves)
                {
                    Board tempBoard = new Board(_board.GetPieces());
                    tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                    GameLogic tempGameLogic = new GameLogic { _board = tempBoard };
                    var nextPossibleMoves = tempBoard.GetAllPossibleMovesForPlayer("white");
                    var result = tempGameLogic.GetBestMoveMinimax(nextPossibleMoves, depth - 1, true);

                    if (result != null)
                    {
                        move.Score = result.Score;
                        if (result.Score < minEval)
                        {
                            minEval = result.Score;
                            bestMove = move;
                        }
                    }
                }
            }

            return bestMove;
        }

        private bool IsGameOver()
        {
            string opponentColor = (_currentPlayer == "white") ? "black" : "white";
            int opponentKingRow = -1;
            int opponentKingCol = -1;

            // Знаходимо короля опонента
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

            // Перевіряємо, чи є можливі ходи
            if (opponentKingRow != -1 && _board.GetAllPossibleMovesForPlayer(opponentColor).Count == 0)
            {
                return true; // Гра закінчена (мат або пат)
            }
            return false;
        }

        public void SetComputerMode(bool isComputerMode)
        {
            _isComputerMode = isComputerMode;
            _currentPlayer = "white"; // Шахи завжди починаються з білих

        }

        public void SwitchPlayerColor()
        {
            _playerPlaysBlack = !_playerPlaysBlack;
            InitializeGame(); // Перезапускаємо гру з новим кольором
        }

        private void SwitchPlayer()
        {
            _currentPlayer = _currentPlayer == "white" ? "black" : "white";
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


    }

}