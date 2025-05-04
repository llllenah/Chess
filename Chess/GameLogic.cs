using System;
using System.Collections.Generic;
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
        private int _computerDifficulty = 1;
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

        public int ComputerDifficulty
        {
            get => _computerDifficulty;
            set => _computerDifficulty = value;
        }

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

        public void MakeComputerMove()
        {
            // Визначаємо колір фігур комп'ютера в залежності від того, за кого грає людина
            string computerColor = _playerPlaysBlack ? "white" : "black";

            // Перевіряємо, чи зараз хід комп'ютера
            if ((_playerPlaysBlack && _currentPlayer == "white") ||
                (!_playerPlaysBlack && _currentPlayer == "black"))
            {
                var possibleMoves = _board.GetAllPossibleMovesForPlayer(computerColor);
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
                        selectedMove = GetBestMoveMinimax(possibleMoves, 1, computerColor == "white");
                    }
                    else if (_computerDifficulty == 3) // Складний рівень: Minimax з більшою глибиною
                    {
                        selectedMove = GetBestMoveMinimax(possibleMoves, 2, computerColor == "white");
                    }

                    if (selectedMove != null)
                    {
                        Piece? movedPiece = _board.GetPiece(selectedMove.StartRow, selectedMove.StartCol);
                        Piece? capturedPiece = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);
                        string moveNotation = GetMoveNotation(movedPiece, selectedMove.StartCol, selectedMove.StartRow, selectedMove.EndCol, selectedMove.EndRow, capturedPiece);
                        _board.MovePiece(selectedMove.StartRow, selectedMove.StartCol, selectedMove.EndRow, selectedMove.EndCol);

                        // Перевірка на підвищення пішака комп'ютера
                        if (movedPiece?.Type == "pawn" &&
                            ((movedPiece.Color == "white" && selectedMove.EndRow == 0) ||
                             (movedPiece.Color == "black" && selectedMove.EndRow == 7)))
                        {
                            _board.SetPiece(selectedMove.EndRow, selectedMove.EndCol, new Piece(movedPiece.Color, "queen"));
                        }

                        MoveMade?.Invoke(this, EventArgs.Empty);
                        SwitchPlayer(); // Перемикаємо гравця, але БЕЗ рекурсивного виклику MakeComputerMove
                        BoardUpdated?.Invoke(this, EventArgs.Empty);

                        // Перевіряємо на закінчення гри після ходу комп'ютера
                        CheckGameEnd(computerColor);
                    }
                }
            }
        }

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

            // !!! ВАЖЛИВА ЗМІНА: перший хід комп'ютера тепер викликатиметься з MainWindow
            // щоб уникнути рекурсивних викликів
        }

        public void SwitchPlayerColor()
        {
            _playerPlaysBlack = !_playerPlaysBlack;
            InitializeGame(); // Перезапускаємо гру з новим кольором
        }

        private void SwitchPlayer()
        {
            _currentPlayer = _currentPlayer == "white" ? "black" : "white";

            // !!! ВИДАЛІТЬ АБО ЗАКОМЕНТУЙТЕ ЦЮ ЧАСТИНУ !!!
            // Весь блок нижче повинен бути видалений або закоментований
            /*
            // Викликаємо комп'ютерний хід, якщо увімкнений комп'ютерний режим і зараз хід комп'ютера
            if (_isComputerMode)
            {
                bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                     (!_playerPlaysBlack && _currentPlayer == "black");

                if (isComputerTurn)
                {
                    MakeComputerMove();
                }
            }
            */
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

        public string? GetPieceSymbol(Piece? piece)
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