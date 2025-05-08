using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ChessTrainer
{
    /// <summary>
    /// Manages the core game logic for the chess application
    /// </summary>
    public class GameLogic
    {
        #region Fields

        /// <summary>
        /// The chess board
        /// </summary>
        private Board _board;

        /// <summary>
        /// Current player's color ("white" or "black")
        /// </summary>
        private string _currentPlayer = "white";

        /// <summary>
        /// Flag indicating whether computer opponent is enabled
        /// </summary>
        private bool _isComputerMode = false;

        /// <summary>
        /// Flag indicating whether the human player is playing as black
        /// </summary>
        private bool _playerPlaysBlack = false;

        /// <summary>
        /// Random number generator for AI decisions
        /// </summary>
        private readonly Random _random = new Random();

        /// <summary>
        /// Action to display pawn promotion dialog
        /// </summary>
        private Func<string, string> _showPromotionDialog;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the chess board
        /// </summary>
        public Board Board => _board;

        /// <summary>
        /// Gets the current player's color
        /// </summary>
        public string CurrentPlayer => _currentPlayer;

        /// <summary>
        /// Gets or sets whether the human player is playing as black
        /// </summary>
        public bool PlayerPlaysBlack
        {
            get => _playerPlaysBlack;
            set => _playerPlaysBlack = value;
        }

        /// <summary>
        /// Difficulty levels for the computer opponent
        /// </summary>
        public enum ComputerDifficulty
        {
            Random = 1,
            Easy = 2,
            Medium = 3,
            Hard = 4,
            Expert = 5
        }

        /// <summary>
        /// Gets or sets the current difficulty level
        /// </summary>
        public ComputerDifficulty CurrentDifficulty { get; set; } = ComputerDifficulty.Medium;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when the board is updated
        /// </summary>
        public event EventHandler? BoardUpdated;

        /// <summary>
        /// Event raised when a move is made
        /// </summary>
        public event EventHandler<MoveEventArgs>? MoveMade;

        /// <summary>
        /// Event raised when the game ends
        /// </summary>
        public event EventHandler<GameEndEventArgs>? GameEnded;

        /// <summary>
        /// Event raised when a pawn needs promotion
        /// </summary>
        public event EventHandler<PawnPromotionEventArgs>? PawnPromotion;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new GameLogic instance
        /// </summary>
        public GameLogic()
        {
            _board = new Board();
            InitializeGame();

            // Default promotion to queen if no handler is set
            _showPromotionDialog = (color) => "queen";
        }

        /// <summary>
        /// Sets the callback for pawn promotion dialog
        /// </summary>
        /// <param name="promotionCallback">Function that takes color and returns piece type</param>
        public void SetPromotionDialogCallback(Func<string, string> promotionCallback)
        {
            _showPromotionDialog = promotionCallback;
        }

        #endregion

        #region Game State Management

        /// <summary>
        /// Initializes a new game
        /// </summary>
        public void InitializeGame()
        {
            _board.InitializeBoard();
            _currentPlayer = "white"; // Chess always starts with white

            OnBoardUpdated();

            // If computer plays white, make first move
            if (_isComputerMode && _playerPlaysBlack && _currentPlayer == "white")
            {
                MakeComputerMove();
            }
        }

        /// <summary>
        /// Loads a game from a board position
        /// </summary>
        /// <param name="boardState">The board state to load</param>
        /// <param name="currentPlayer">The player to move</param>
        public void LoadGame(Piece?[,] boardState, string currentPlayer)
        {
            _board = new Board(boardState);
            _currentPlayer = currentPlayer;

            OnBoardUpdated();
        }

        /// <summary>
        /// Gets the current board state as UI cells
        /// </summary>
        /// <returns>Collection of BoardCell objects</returns>
        public ObservableCollection<BoardCell> GetCurrentBoard()
        {
            var boardCells = new ObservableCollection<BoardCell>();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    // Handle board orientation based on player color
                    int displayRow = row;
                    int displayCol = col;

                    Piece? piece = _board.GetPiece(row, col);

                    // Create a board cell with appropriate color
                    boardCells.Add(new BoardCell(
                        row,
                        col,
                        (row + col) % 2 == 0 ? Brushes.LightGray : Brushes.White,
                        piece));
                }
            }

            return boardCells;
        }

        /// <summary>
        /// Sets the game mode (computer opponent or two players)
        /// </summary>
        /// <param name="isComputerMode">True for computer opponent, false for two players</param>
        public void SetComputerMode(bool isComputerMode)
        {
            _isComputerMode = isComputerMode;
            _currentPlayer = "white"; // Reset to white's turn
        }

        /// <summary>
        /// Checks if it's currently the human player's turn
        /// </summary>
        /// <returns>True if it's the human player's turn, false if it's the computer's turn</returns>
        public bool IsPlayerTurn()
        {
            if (!_isComputerMode)
                return true; // В режиме двух игроков всегда разрешаем ходы

            // В режиме с компьютером, проверяем соответствует ли текущий игрок цвету человека
            bool isPlayerTurn = (_playerPlaysBlack && _currentPlayer == "black") ||
                               (!_playerPlaysBlack && _currentPlayer == "white");

            return isPlayerTurn;
        }

        /// <summary>
        /// Switches the player's color and resets the game
        /// </summary>
        public void SwitchPlayerColor()
        {
            _playerPlaysBlack = !_playerPlaysBlack;
            InitializeGame();
        }

        #endregion

        #region Move Handling

        /// <summary>
        /// Attempts to move a piece
        /// </summary>
        /// <param name="startRow">Starting row</param>
        /// <param name="startCol">Starting column</param>
        /// <param name="endRow">Ending row</param>
        /// <param name="endCol">Ending column</param>
        /// <returns>True if the move was valid and executed</returns>
        public bool TryMovePiece(int startRow, int startCol, int endRow, int endCol)
        {
            // Проверяем, чей сейчас ход в компьютерном режиме
            if (!IsPlayerTurn())
                return false;

            // Проверяем, что это ход текущего игрока
            Piece? piece = _board.GetPiece(startRow, startCol);
            if (piece == null || piece.Color != _currentPlayer)
                return false;

            // Валидируем и выполняем ход
            if (_board.IsValidMove(startRow, startCol, endRow, endCol, _currentPlayer))
            {
                // Получаем перемещенную и взятую фигуры
                Piece? movedPiece = _board.GetPiece(startRow, startCol);
                Piece? capturedPiece = _board.GetPiece(endRow, endCol);

                // Выполняем ход
                _board.MovePiece(startRow, startCol, endRow, endCol);

                // Создаем информацию о ходе
                string moveNotation = GetMoveNotation(movedPiece, startRow, startCol, endRow, endCol, capturedPiece);

                // Обрабатываем продвижение пешки
                bool wasPromoted = false;
                if (IsPawnPromotion(endRow, endCol))
                {
                    wasPromoted = HandlePawnPromotion(endRow, endCol);
                    if (!wasPromoted)
                    {
                        // Если продвижение было отменено, отменяем ход
                        _board.MovePiece(endRow, endCol, startRow, startCol);
                        if (capturedPiece != null)
                        {
                            _board.SetPiece(endRow, endCol, capturedPiece);
                        }
                        return false;
                    }
                }
                // Уведомляем о выполненном ходе
                OnMoveMade(new MoveEventArgs(startRow, startCol, endRow, endCol, moveNotation));

                // Проверяем, не закончилась ли игра взятием короля
                if (capturedPiece?.Type == "king")
                {
                    OnGameEnded(new GameEndEventArgs(GameEndType.KingCaptured, _currentPlayer));
                    return true;
                }

                // Переключаем игроков
                SwitchPlayer();

                // Проверяем шах и мат для нового текущего игрока
                GameEndType endType = _board.CheckForGameEnd(_currentPlayer);
                if (endType != GameEndType.None)
                {
                    // Победитель - игрок, который только что сделал ход
                    string winner = _currentPlayer == "white" ? "black" : "white";
                    OnGameEnded(new GameEndEventArgs(endType, winner));
                    return true;
                }

                // Обновляем доску
                OnBoardUpdated();

                // Если режим игры с компьютером и сейчас ход компьютера, выполняем ход
                if (_isComputerMode && !IsPlayerTurn())
                {
                    // Делаем ход компьютера асинхронно, чтобы не замораживать UI
                    Task.Run(() =>
                    {
                        // Небольшая задержка для лучшего пользовательского опыта
                        System.Threading.Thread.Sleep(500);

                        // Выполняем ход компьютера
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MakeComputerMove();
                        });
                    });
                }

                return true;
            }

            return false;
        }
        /// <summary>
        /// Checks if a pawn needs promotion
        /// </summary>
        /// <param name="row">Pawn's row position</param>
        /// <param name="col">Pawn's column position</param>
        /// <returns>True if pawn should be promoted</returns>
        private bool IsPawnPromotion(int row, int col)
        {
            Piece? piece = _board.GetPiece(row, col);

            return piece?.Type == "pawn" &&
                 ((piece.Color == "white" && row == 0) ||
                  (piece.Color == "black" && row == 7));
        }

        /// <summary>
        /// Handles pawn promotion with dialog or automatic promotion
        /// </summary>
        /// <param name="row">Pawn's row position</param>
        /// <param name="col">Pawn's column position</param>
        /// <returns>True if promotion was successful, false if cancelled</returns>
        private bool HandlePawnPromotion(int row, int col)
        {
            Piece? pawn = _board.GetPiece(row, col);

            if (pawn?.Type == "pawn")
            {
                // Get pawn color
                string pawnColor = pawn.Color;

                // For computer, automatically promote to queen
                if (_isComputerMode && !IsPlayerTurn())
                {
                    _board.SetPiece(row, col, new Piece(pawnColor, "queen"));
                    OnBoardUpdated();
                    return true;
                }

                // Raise the event for UI to show promotion dialog
                var args = new PawnPromotionEventArgs(row, col, pawnColor);
                OnPawnPromotion(args);

                // If user cancelled promotion, return false
                if (args.IsCancelled)
                    return false;

                // Get piece type from dialog or default
                string pieceType = string.IsNullOrEmpty(args.PromotionPiece)
                    ? _showPromotionDialog(pawnColor)
                    : args.PromotionPiece;

                // Set the new piece
                _board.SetPiece(row, col, new Piece(pawnColor, pieceType));
                OnBoardUpdated();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Switches to the next player's turn
        /// </summary>
        private void SwitchPlayer()
        {
            _currentPlayer = _currentPlayer == "white" ? "black" : "white";
        }

        /// <summary>
        /// Generates chess notation for a move
        /// </summary>
        /// <param name="piece">The piece that moved</param>
        /// <param name="startRow">Starting row</param>
        /// <param name="startCol">Starting column</param>
        /// <param name="endRow">Ending row</param>
        /// <param name="endCol">Ending column</param>
        /// <param name="capturedPiece">Piece that was captured, if any</param>
        /// <returns>Algebraic notation for the move</returns>
        private string GetMoveNotation(Piece? piece, int startRow, int startCol, int endRow, int endCol, Piece? capturedPiece)
        {
            if (piece == null) return "";

            string pieceSymbol = piece.Type switch
            {
                "pawn" => "",
                "knight" => "N",
                "bishop" => "B",
                "rook" => "R",
                "queen" => "Q",
                "king" => "K",
                _ => ""
            };

            string capture = capturedPiece != null ? "x" : "";
            string startSquare = $"{(char)('a' + startCol)}{8 - startRow}";
            string endSquare = $"{(char)('a' + endCol)}{8 - endRow}";

            // Special case for pawn captures
            if (piece.Type == "pawn" && capture != "")
            {
                return $"{(char)('a' + startCol)}{capture}{endSquare}";
            }

            return $"{pieceSymbol}{startSquare}{capture}{endSquare}";
        }

        /// <summary>
        /// Makes a move for the computer player
        /// </summary>
        public void MakeComputerMove()
        {
            // Определяем цвет компьютера
            string computerColor = _playerPlaysBlack ? "white" : "black";

            // Проверяем, действительно ли сейчас ход компьютера
            if ((_playerPlaysBlack && _currentPlayer == "white") ||
                (!_playerPlaysBlack && _currentPlayer == "black"))
            {
                // Получаем все возможные ходы
                var possibleMoves = _board.GetAllPossibleMovesForPlayer(computerColor);

                if (possibleMoves.Count > 0)
                {
                    // Выбираем лучший ход на основе сложности
                    Move? selectedMove = GetBestMove(possibleMoves, (int)CurrentDifficulty, computerColor == "white");

                    if (selectedMove != null)
                    {
                        // Выполняем ход
                        Piece? movedPiece = _board.GetPiece(selectedMove.StartRow, selectedMove.StartCol);
                        Piece? capturedPiece = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);

                        _board.MovePiece(selectedMove.StartRow, selectedMove.StartCol, selectedMove.EndRow, selectedMove.EndCol);

                        // Обрабатываем продвижение пешки (компьютер всегда превращает в ферзя)
                        if (IsPawnPromotion(selectedMove.EndRow, selectedMove.EndCol))
                        {
                            Piece? pawn = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);
                            if (pawn != null)
                            {
                                _board.SetPiece(selectedMove.EndRow, selectedMove.EndCol, new Piece(pawn.Color, "queen"));
                            }
                        }

                        // Создаем нотацию хода
                        string moveNotation = GetMoveNotation(
                            movedPiece,
                            selectedMove.StartRow,
                            selectedMove.StartCol,
                            selectedMove.EndRow,
                            selectedMove.EndCol,
                            capturedPiece
                        );

                        // Уведомляем о выполненном ходе
                        OnMoveMade(new MoveEventArgs(
                            selectedMove.StartRow,
                            selectedMove.StartCol,
                            selectedMove.EndRow,
                            selectedMove.EndCol,
                            moveNotation
                        ));

                        // Проверяем взятие короля
                        if (capturedPiece?.Type == "king")
                        {
                            OnGameEnded(new GameEndEventArgs(GameEndType.KingCaptured, computerColor));
                            return;
                        }

                        // Переключаем игроков
                        SwitchPlayer();

                        // Проверяем шах и мат
                        GameEndType endType = _board.CheckForGameEnd(_currentPlayer);
                        if (endType != GameEndType.None)
                        {
                            string winner = _currentPlayer == "white" ? "black" : "white";
                            OnGameEnded(new GameEndEventArgs(endType, winner));
                            return;
                        }

                        // Обновляем доску
                        OnBoardUpdated();
                    }
                }
            }
        }
        #endregion

        #region AI Methods

        /// <summary>
        /// Gets the best move for the computer player
        /// </summary>
        /// <param name="possibleMoves">List of possible moves</param>
        /// <param name="difficulty">Difficulty level (1-5)</param>
        /// <param name="maximizingPlayer">True if the AI is playing white</param>
        /// <returns>The best move</returns>
        public Move? GetBestMove(List<Move> possibleMoves, int difficulty, bool maximizingPlayer)
        {
            // If no moves are available
            if (possibleMoves.Count == 0)
                return null;

            // For Random difficulty, just return a random move
            if (difficulty <= 1)
            {
                return possibleMoves[_random.Next(possibleMoves.Count)];
            }

            // Determine search depth based on difficulty
            int depth = difficulty;

            // For higher difficulties, use alpha-beta pruning with parallelization
            if (difficulty >= 4)
            {
                return GetBestMoveParallel(possibleMoves, depth, maximizingPlayer);
            }
            // For medium difficulty, use regular alpha-beta
            else if (difficulty >= 2)
            {
                return GetBestMoveAlphaBeta(possibleMoves, depth, maximizingPlayer);
            }
            // Fallback to minimax
            else
            {
                return GetBestMoveMinimax(possibleMoves, depth, maximizingPlayer);
            }
        }

        /// <summary>
        /// Gets the best move using the minimax algorithm
        /// </summary>
        /// <param name="possibleMoves">List of possible moves</param>
        /// <param name="depth">Search depth</param>
        /// <param name="maximizingPlayer">True if AI is white (maximizing), false if black (minimizing)</param>
        /// <returns>Best move according to the minimax algorithm</returns>
        private Move? GetBestMoveMinimax(List<Move> possibleMoves, int depth, bool maximizingPlayer)
        {
            if (possibleMoves.Count == 0 || depth == 0)
                return null;

            Move? bestMove = null;
            int bestScore = maximizingPlayer ? int.MinValue : int.MaxValue;

            foreach (var move in possibleMoves)
            {
                // Create a temporary board to test the move
                Board tempBoard = new Board(_board.GetPieces());
                tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                // Calculate score recursively
                int score;

                if (depth == 1)
                {
                    score = tempBoard.EvaluatePosition();
                }
                else
                {
                    string nextPlayerColor = maximizingPlayer ? "black" : "white";
                    var nextMoves = tempBoard.GetAllPossibleMovesForPlayer(nextPlayerColor);

                    var result = GetBestMoveMinimax(nextMoves, depth - 1, !maximizingPlayer);

                    if (result == null)
                    {
                        score = tempBoard.EvaluatePosition();
                    }
                    else
                    {
                        score = result.Score;
                    }
                }

                move.Score = score;

                // Update best move
                if (maximizingPlayer)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
                else
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
            }

            return bestMove;
        }

        /// <summary>
        /// Gets the best move using alpha-beta pruning
        /// </summary>
        /// <param name="possibleMoves">List of possible moves</param>
        /// <param name="depth">Search depth</param>
        /// <param name="maximizingPlayer">True if AI is white (maximizing), false if black (minimizing)</param>
        /// <param name="alpha">Alpha value for pruning</param>
        /// <param name="beta">Beta value for pruning</param>
        /// <returns>Best move according to alpha-beta pruning</returns>
        private Move? GetBestMoveAlphaBeta(List<Move> possibleMoves, int depth, bool maximizingPlayer,
                                          int alpha = int.MinValue, int beta = int.MaxValue)
        {
            if (possibleMoves.Count == 0 || depth == 0)
                return null;

            Move? bestMove = null;

            foreach (var move in possibleMoves)
            {
                // Create a temporary board to test the move
                Board tempBoard = new Board(_board.GetPieces());
                tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                // Calculate score recursively
                int score;

                if (depth == 1)
                {
                    score = tempBoard.EvaluatePosition();
                }
                else
                {
                    string nextPlayerColor = maximizingPlayer ? "black" : "white";
                    var nextMoves = tempBoard.GetAllPossibleMovesForPlayer(nextPlayerColor);

                    var result = GetBestMoveAlphaBeta(nextMoves, depth - 1, !maximizingPlayer, alpha, beta);

                    if (result == null)
                    {
                        score = tempBoard.EvaluatePosition();
                    }
                    else
                    {
                        score = result.Score;
                    }
                }

                move.Score = score;

                // Update best move and alpha/beta values
                if (maximizingPlayer)
                {
                    if (score > alpha)
                    {
                        alpha = score;
                        bestMove = move;
                    }

                    if (beta <= alpha)
                        break; // Beta cutoff
                }
                else
                {
                    if (score < beta)
                    {
                        beta = score;
                        bestMove = move;
                    }

                    if (beta <= alpha)
                        break; // Alpha cutoff
                }
            }

            return bestMove ?? possibleMoves.FirstOrDefault();
        }

        /// <summary>
        /// Gets the best move using parallel alpha-beta pruning
        /// </summary>
        /// <param name="possibleMoves">List of possible moves</param>
        /// <param name="depth">Search depth</param>
        /// <param name="maximizingPlayer">True if AI is white (maximizing), false if black (minimizing)</param>
        /// <returns>Best move according to parallel alpha-beta pruning</returns>
        private Move? GetBestMoveParallel(List<Move> possibleMoves, int depth, bool maximizingPlayer)
        {
            if (possibleMoves.Count == 0 || depth == 0)
                return null;

            // Evaluate all top-level moves in parallel
            List<Move> evaluatedMoves = new List<Move>();

            Parallel.ForEach(possibleMoves, move =>
            {
                // Create a clone of the move to avoid race conditions
                Move evaluatedMove = move.Clone();

                // Create a temporary board to test the move
                Board tempBoard = new Board(_board.GetPieces());
                tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                // Calculate score recursively (non-parallel for deeper levels)
                int score;

                if (depth == 1)
                {
                    score = tempBoard.EvaluatePosition();
                }
                else
                {
                    string nextPlayerColor = maximizingPlayer ? "black" : "white";
                    var nextMoves = tempBoard.GetAllPossibleMovesForPlayer(nextPlayerColor);

                    // Use regular alpha-beta for deeper levels
                    var result = GetBestMoveAlphaBeta(nextMoves, depth - 1, !maximizingPlayer);

                    if (result == null)
                    {
                        score = tempBoard.EvaluatePosition();
                    }
                    else
                    {
                        score = result.Score;
                    }
                }

                evaluatedMove.Score = score;

                lock (evaluatedMoves)
                {
                    evaluatedMoves.Add(evaluatedMove);
                }
            });

            // Select the best move
            if (evaluatedMoves.Count == 0)
                return possibleMoves.FirstOrDefault();

            if (maximizingPlayer)
            {
                return evaluatedMoves.OrderByDescending(m => m.Score).FirstOrDefault();
            }
            else
            {
                return evaluatedMoves.OrderBy(m => m.Score).FirstOrDefault();
            }
        }

        #endregion

        #region Event Methods

        /// <summary>
        /// Raises the BoardUpdated event
        /// </summary>
        protected virtual void OnBoardUpdated()
        {
            BoardUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the MoveMade event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnMoveMade(MoveEventArgs e)
        {
            MoveMade?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the GameEnded event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnGameEnded(GameEndEventArgs e)
        {
            GameEnded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the PawnPromotion event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnPawnPromotion(PawnPromotionEventArgs e)
        {
            PawnPromotion?.Invoke(this, e);
        }

        #endregion
    }

    #region Event Argument Classes

    /// <summary>
    /// Event arguments for moves
    /// </summary>
    public class MoveEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the starting row
        /// </summary>
        public int StartRow { get; }

        /// <summary>
        /// Gets the starting column
        /// </summary>
        public int StartCol { get; }

        /// <summary>
        /// Gets the ending row
        /// </summary>
        public int EndRow { get; }

        /// <summary>
        /// Gets the ending column
        /// </summary>
        public int EndCol { get; }

        /// <summary>
        /// Gets the move notation
        /// </summary>
        public string MoveNotation { get; }

        /// <summary>
        /// Creates a new MoveEventArgs instance
        /// </summary>
        /// <param name="startRow">Starting row</param>
        /// <param name="startCol">Starting column</param>
        /// <param name="endRow">Ending row</param>
        /// <param name="endCol">Ending column</param>
        /// <param name="moveNotation">Algebraic notation for the move</param>
        public MoveEventArgs(int startRow, int startCol, int endRow, int endCol, string moveNotation)
        {
            StartRow = startRow;
            StartCol = startCol;
            EndRow = endRow;
            EndCol = endCol;
            MoveNotation = moveNotation;
        }
    }

    /// <summary>
    /// Event arguments for game end
    /// </summary>
    public class GameEndEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the type of game end
        /// </summary>
        public GameEndType EndType { get; }

        /// <summary>
        /// Gets the color of the winning player
        /// </summary>
        public string WinnerColor { get; }

        /// <summary>
        /// Creates a new GameEndEventArgs instance
        /// </summary>
        /// <param name="endType">Type of game end</param>
        /// <param name="winnerColor">Color of the winning player</param>
        public GameEndEventArgs(GameEndType endType, string winnerColor)
        {
            EndType = endType;
            WinnerColor = winnerColor;
        }
    }

    /// <summary>
    /// Event arguments for pawn promotion
    /// </summary>
    public class PawnPromotionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the row of the pawn
        /// </summary>
        public int Row { get; }

        /// <summary>
        /// Gets the column of the pawn
        /// </summary>
        public int Col { get; }

        /// <summary>
        /// Gets the color of the pawn
        /// </summary>
        public string PawnColor { get; }

        /// <summary>
        /// Gets or sets the piece type to promote to
        /// </summary>
        public string PromotionPiece { get; set; } = "";

        /// <summary>
        /// Gets or sets whether the promotion was cancelled
        /// </summary>
        public bool IsCancelled { get; set; } = false;

        /// <summary>
        /// Creates a new PawnPromotionEventArgs instance
        /// </summary>
        /// <param name="row">Pawn's row position</param>
        /// <param name="col">Pawn's column position</param>
        /// <param name="pawnColor">Color of the pawn</param>
        public PawnPromotionEventArgs(int row, int col, string pawnColor)
        {
            Row = row;
            Col = col;
            PawnColor = pawnColor;
        }
    }

    #endregion
}