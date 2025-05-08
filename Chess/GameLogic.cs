using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new GameLogic instance
        /// </summary>
        public GameLogic()
        {
            _board = new Board();
            InitializeGame();
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
                    int displayRow = _playerPlaysBlack ? 7 - row : row;
                    int displayCol = _playerPlaysBlack ? 7 - col : col;

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
            // Check if it's player's turn in computer mode
            if (_isComputerMode)
            {
                bool isPlayerTurn = (_playerPlaysBlack && _currentPlayer == "black") ||
                                   (!_playerPlaysBlack && _currentPlayer == "white");

                if (!isPlayerTurn)
                    return false;
            }

            // Validate and execute the move
            if (_board.IsValidMove(startRow, startCol, endRow, endCol, _currentPlayer))
            {
                // Get moved and captured pieces
                Piece? movedPiece = _board.GetPiece(startRow, startCol);
                Piece? capturedPiece = _board.GetPiece(endRow, endCol);

                // Execute the move
                _board.MovePiece(startRow, startCol, endRow, endCol);

                // Create move information
                string moveNotation = GetMoveNotation(movedPiece, startRow, startCol, endRow, endCol, capturedPiece);

                // Handle pawn promotion
                if (IsPawnPromotion(endRow, endCol))
                {
                    PromotePawn(endRow, endCol);
                }

                // Notify that a move was made
                OnMoveMade(new MoveEventArgs(startRow, startCol, endRow, endCol, moveNotation));

                // Check for game end by king capture
                if (capturedPiece?.Type == "king")
                {
                    OnGameEnded(new GameEndEventArgs(GameEndType.KingCaptured, _currentPlayer));
                    return true;
                }

                // Switch players
                SwitchPlayer();

                // Check for checkmate or stalemate for the new current player
                GameEndType endType = _board.CheckForGameEnd(_currentPlayer);
                if (endType != GameEndType.None)
                {
                    // The winner is the player who just moved
                    string winner = _currentPlayer == "white" ? "black" : "white";
                    OnGameEnded(new GameEndEventArgs(endType, winner));
                    return true;
                }

                // Update the board
                OnBoardUpdated();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a pawn needs promotion
        /// </summary>
        private bool IsPawnPromotion(int row, int col)
        {
            Piece? piece = _board.GetPiece(row, col);

            return piece?.Type == "pawn" &&
                 ((piece.Color == "white" && row == 0) ||
                  (piece.Color == "black" && row == 7));
        }

        /// <summary>
        /// Promotes a pawn to a queen
        /// </summary>
        private void PromotePawn(int row, int col)
        {
            Piece? pawn = _board.GetPiece(row, col);

            if (pawn?.Type == "pawn")
            {
                // Automatically promote to queen (could be modified to show selection dialog)
                _board.SetPiece(row, col, new Piece(pawn.Color, "queen"));
                OnBoardUpdated();
            }
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
            // Determine computer's color
            string computerColor = _playerPlaysBlack ? "white" : "black";

            // Check if it's computer's turn
            if ((_playerPlaysBlack && _currentPlayer == "white") ||
                (!_playerPlaysBlack && _currentPlayer == "black"))
            {
                // Get all possible moves
                var possibleMoves = _board.GetAllPossibleMovesForPlayer(computerColor);

                if (possibleMoves.Count > 0)
                {
                    // Select the best move based on difficulty
                    Move? selectedMove = GetBestMove(possibleMoves, (int)CurrentDifficulty, computerColor == "white");

                    if (selectedMove != null)
                    {
                        // Execute the move
                        Piece? movedPiece = _board.GetPiece(selectedMove.StartRow, selectedMove.StartCol);
                        Piece? capturedPiece = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);

                        _board.MovePiece(selectedMove.StartRow, selectedMove.StartCol, selectedMove.EndRow, selectedMove.EndCol);

                        // Handle pawn promotion
                        if (IsPawnPromotion(selectedMove.EndRow, selectedMove.EndCol))
                        {
                            PromotePawn(selectedMove.EndRow, selectedMove.EndCol);
                        }

                        // Create move notation
                        string moveNotation = GetMoveNotation(
                            movedPiece,
                            selectedMove.StartRow,
                            selectedMove.StartCol,
                            selectedMove.EndRow,
                            selectedMove.EndCol,
                            capturedPiece
                        );

                        // Notify that a move was made
                        OnMoveMade(new MoveEventArgs(
                            selectedMove.StartRow,
                            selectedMove.StartCol,
                            selectedMove.EndRow,
                            selectedMove.EndCol,
                            moveNotation
                        ));

                        // Check for king capture
                        if (capturedPiece?.Type == "king")
                        {
                            OnGameEnded(new GameEndEventArgs(GameEndType.KingCaptured, computerColor));
                            return;
                        }

                        // Switch players
                        SwitchPlayer();

                        // Check for checkmate or stalemate
                        GameEndType endType = _board.CheckForGameEnd(_currentPlayer);
                        if (endType != GameEndType.None)
                        {
                            string winner = _currentPlayer == "white" ? "black" : "white";
                            OnGameEnded(new GameEndEventArgs(endType, winner));
                            return;
                        }

                        // Update the board
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
        public GameEndEventArgs(GameEndType endType, string winnerColor)
        {
            EndType = endType;
            WinnerColor = winnerColor;
        }
    }

    #endregion
}