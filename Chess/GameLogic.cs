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

        /// <summary>
        /// Counter for the 50-move rule
        /// </summary>
        private int _halfMovesSinceCaptureOrPawn = 0;

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
            _currentPlayer = "white";
            _halfMovesSinceCaptureOrPawn = 0;

            OnBoardUpdated();

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
            _halfMovesSinceCaptureOrPawn = 0;

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
                    int displayRow = row;
                    int displayCol = col;

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

        /// <summary>
        /// Sets the game mode (computer opponent or two players)
        /// </summary>
        /// <param name="isComputerMode">True for computer opponent, false for two players</param>
        public void SetComputerMode(bool isComputerMode)
        {
            _isComputerMode = isComputerMode;
        }

        /// <summary>
        /// Checks if it's currently the human player's turn
        /// </summary>
        /// <returns>True if it's the human player's turn, false if it's the computer's turn</returns>
        public bool IsPlayerTurn()
        {
            if (!_isComputerMode)
                return true;

            bool isPlayerTurn = (_playerPlaysBlack && _currentPlayer == "black") ||
                               (!_playerPlaysBlack && _currentPlayer == "white");

            return isPlayerTurn;
        }

        /// <summary>
        /// Clears the board
        /// </summary>
        public void ClearBoard()
        {
            _board.ClearBoard();
            _halfMovesSinceCaptureOrPawn = 0;
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
            if (!IsPlayerTurn())
                return false;

            Piece? piece = _board.GetPiece(startRow, startCol);
            if (piece == null || piece.Color != _currentPlayer)
                return false;

            if (_board.IsValidMove(startRow, startCol, endRow, endCol, _currentPlayer))
            {
                Piece? movedPiece = _board.GetPiece(startRow, startCol);
                Piece? capturedPiece = _board.GetPiece(endRow, endCol);

                bool isPawnMove = movedPiece?.Type == "pawn";
                bool isCapture = capturedPiece != null;

                if (isPawnMove || isCapture)
                {
                    _halfMovesSinceCaptureOrPawn = 0;
                }
                else
                {
                    _halfMovesSinceCaptureOrPawn++;
                }

                _board.MovePiece(startRow, startCol, endRow, endCol);

                string moveNotation = GetMoveNotation(movedPiece, startRow, startCol, endRow, endCol, capturedPiece);

                bool wasPromoted = false;
                if (IsPawnPromotion(endRow, endCol))
                {
                    wasPromoted = HandlePawnPromotion(endRow, endCol);
                    if (!wasPromoted)
                    {
                        _board.MovePiece(endRow, endCol, startRow, startCol);
                        if (capturedPiece != null)
                        {
                            _board.SetPiece(endRow, endCol, capturedPiece);
                        }
                        return false;
                    }
                }

                OnMoveMade(new MoveEventArgs(startRow, startCol, endRow, endCol, moveNotation));

                if (capturedPiece?.Type == "king")
                {
                    OnGameEnded(new GameEndEventArgs(GameEndType.KingCaptured, _currentPlayer));
                    return true;
                }

                if (_halfMovesSinceCaptureOrPawn >= 100) 
                {
                    OnGameEnded(new GameEndEventArgs(GameEndType.Draw, ""));
                    return true;
                }

                SwitchPlayer();

                GameEndType endType = _board.CheckForGameEnd(_currentPlayer);
                if (endType != GameEndType.None)
                {
                    string winner = _currentPlayer == "white" ? "black" : "white";
                    OnGameEnded(new GameEndEventArgs(endType, winner));
                    return true;
                }

                OnBoardUpdated();

                if (_isComputerMode && !IsPlayerTurn())
                {
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(500);

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
                string pawnColor = pawn.Color;

                if (_isComputerMode && !IsPlayerTurn())
                {
                    _board.SetPiece(row, col, new Piece(pawnColor, "queen"));
                    OnBoardUpdated();
                    return true;
                }

                var args = new PawnPromotionEventArgs(row, col, pawnColor);
                OnPawnPromotion(args);

                if (args.IsCancelled)
                    return false;

                string pieceType = string.IsNullOrEmpty(args.PromotionPiece)
                    ? _showPromotionDialog(pawnColor)
                    : args.PromotionPiece;

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

            if (piece.Type == "king" && Math.Abs(endCol - startCol) == 2)
            {
                return endCol > startCol ? "O-O" : "O-O-O";
            }

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

            if (piece.Type == "pawn" && capture != "")
            {
                return $"{(char)('a' + startCol)}{capture}{endSquare}";
            }

            bool needsFile = false;
            bool needsRank = false;

            if (piece.Type != "pawn")
            {
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        if (row == startRow && col == startCol)
                            continue;

                        Piece? otherPiece = _board.GetPiece(row, col);

                        if (otherPiece?.Type == piece.Type &&
                            otherPiece.Color == piece.Color &&
                            _board.IsValidMove(row, col, endRow, endCol, piece.Color))
                        {
                            needsFile = true;

                            if (col == startCol)
                                needsRank = true;
                        }
                    }
                }
            }

            string qualifier = "";
            if (needsFile)
                qualifier += (char)('a' + startCol);
            if (needsRank)
                qualifier += (8 - startRow);

            string notation = $"{pieceSymbol}{qualifier}{capture}{endSquare}";

            string opponentColor = piece.Color == "white" ? "black" : "white";

            Board tempBoard = new Board(_board.GetPieces());
            tempBoard.MovePiece(startRow, startCol, endRow, endCol);

            if (tempBoard.IsKingInCheck(opponentColor))
            {
                GameEndType endType = tempBoard.CheckForGameEnd(opponentColor);
                if (endType == GameEndType.Checkmate)
                    notation += "#";
                else
                    notation += "+";
            }

            return notation;
        }

        /// <summary>
        /// Makes a move for the computer player
        /// </summary>
        public void MakeComputerMove()
        {
            string computerColor = _playerPlaysBlack ? "white" : "black";

            if (_currentPlayer != computerColor)
                return;

            var possibleMoves = _board.GetAllPossibleMovesForPlayer(computerColor);

            if (possibleMoves.Count > 0)
            {
                Move? selectedMove = GetBestMove(possibleMoves, (int)CurrentDifficulty, computerColor == "white");

                if (selectedMove != null)
                {
                    Piece? movedPiece = _board.GetPiece(selectedMove.StartRow, selectedMove.StartCol);
                    Piece? capturedPiece = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);

                    bool isPawnMove = movedPiece?.Type == "pawn";
                    bool isCapture = capturedPiece != null;

                    if (isPawnMove || isCapture)
                    {
                        _halfMovesSinceCaptureOrPawn = 0;
                    }
                    else
                    {
                        _halfMovesSinceCaptureOrPawn++;
                    }

                    _board.MovePiece(selectedMove.StartRow, selectedMove.StartCol, selectedMove.EndRow, selectedMove.EndCol);

                    if (IsPawnPromotion(selectedMove.EndRow, selectedMove.EndCol))
                    {
                        Piece? pawn = _board.GetPiece(selectedMove.EndRow, selectedMove.EndCol);
                        if (pawn != null)
                        {
                            _board.SetPiece(selectedMove.EndRow, selectedMove.EndCol, new Piece(pawn.Color, "queen"));
                        }
                    }

                    string moveNotation = GetMoveNotation(
                        movedPiece,
                        selectedMove.StartRow,
                        selectedMove.StartCol,
                        selectedMove.EndRow,
                        selectedMove.EndCol,
                        capturedPiece
                    );

                    OnMoveMade(new MoveEventArgs(
                        selectedMove.StartRow,
                        selectedMove.StartCol,
                        selectedMove.EndRow,
                        selectedMove.EndCol,
                        moveNotation
                    ));

                    if (capturedPiece?.Type == "king")
                    {
                        OnGameEnded(new GameEndEventArgs(GameEndType.KingCaptured, computerColor));
                        return;
                    }

                    if (_halfMovesSinceCaptureOrPawn >= 100)
                    {
                        OnGameEnded(new GameEndEventArgs(GameEndType.Draw, ""));
                        return;
                    }

                    SwitchPlayer();

                    GameEndType endType = _board.CheckForGameEnd(_currentPlayer);
                    if (endType != GameEndType.None)
                    {
                        string winner = _currentPlayer == "white" ? "black" : "white";
                        OnGameEnded(new GameEndEventArgs(endType, winner));
                        return;
                    }

                    OnBoardUpdated();
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
            if (possibleMoves.Count == 0)
                return null;

            if (difficulty <= 1)
            {
                return possibleMoves[_random.Next(possibleMoves.Count)];
            }

            int depth = difficulty;

            if (difficulty >= 4)
            {
                return GetBestMoveParallel(possibleMoves, depth, maximizingPlayer);
            }
            else if (difficulty >= 2)
            {
                return GetBestMoveAlphaBeta(possibleMoves, depth, maximizingPlayer);
            }
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
                Board tempBoard = new Board(_board.GetPieces());
                tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

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
                Board tempBoard = new Board(_board.GetPieces());
                tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

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

                if (maximizingPlayer)
                {
                    if (score > alpha)
                    {
                        alpha = score;
                        bestMove = move;
                    }

                    if (beta <= alpha)
                        break;
                }
                else
                {
                    if (score < beta)
                    {
                        beta = score;
                        bestMove = move;
                    }

                    if (beta <= alpha)
                        break;
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

            List<Move> evaluatedMoves = new List<Move>();

            Parallel.ForEach(possibleMoves, move =>
            {
                Move evaluatedMove = move.Clone();

                Board tempBoard = new Board(_board.GetPieces());
                tempBoard.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);

                int score;

                if (depth == 1)
                {
                    score = tempBoard.EvaluatePosition();
                }
                else
                {
                    string nextPlayerColor = maximizingPlayer ? "black" : "white";
                    var nextMoves = tempBoard.GetAllPossibleMovesForPlayer(nextPlayerColor);

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