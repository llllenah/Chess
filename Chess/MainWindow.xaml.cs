using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ChessTrainer
{
    /// <summary>
    /// Main window for the chess trainer application
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        // Game state
        private bool _isGameActive = true;
        private string _gameResultText = "";
        private ObservableCollection<BoardCell> _board = new ObservableCollection<BoardCell>();
        private ChessColor _playerColor = ChessColor.White;
        private bool _isTwoPlayersMode = false;
        private bool _isComputerMode = false;
        private string _currentPlayer = "white";
        private bool _playerPlaysBlack = false;
        private ObservableCollection<string> _moveHistory = new ObservableCollection<string>();
        private bool _isPositionLoaded = false;
        private bool _firstMoveAfterLoad = false;
        private bool _isClearingFromComputerMode = false;
        private GameLogic _gameLogic;

        // UI state
        private Color _lightBoardColor = Brushes.LightGray.Color;
        private Color _darkBoardColor = Brushes.White.Color;
        private Point _dragStartPoint;
        private BoardCell? _draggedCell;
        private bool _isBoardFlipped = false;
        private bool _isSetupPositionMode = false;
        private Piece? _selectedPieceForPlacement = null;
        private Button? _selectedPieceButton = null;

        // Timers
        private DispatcherTimer? _whiteTimer;
        private DispatcherTimer? _blackTimer;
        private TimeSpan _whiteTimeLeft;
        private TimeSpan _blackTimeLeft;
        private bool _isTimerRunning;

        // UI panels
        private StackPanel? _whitePiecePanel;
        private StackPanel? _blackPiecePanel;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the board is flipped
        /// </summary>
        public bool IsBoardFlipped
        {
            get => _isBoardFlipped;
            set => SetProperty(ref _isBoardFlipped, value);
        }

        /// <summary>
        /// Gets or sets whether the game is active
        /// </summary>
        public bool IsGameActive
        {
            get => _isGameActive;
            set => SetProperty(ref _isGameActive, value);
        }

        /// <summary>
        /// Gets or sets the game result text
        /// </summary>
        public string GameResultText
        {
            get => _gameResultText;
            set => SetProperty(ref _gameResultText, value);
        }

        /// <summary>
        /// Gets or sets whether computer mode is enabled
        /// </summary>
        public bool IsComputerMode
        {
            get => _isComputerMode;
            set
            {
                if (_isComputerMode != value)
                {
                    _isComputerMode = value;
                    OnPropertyChanged(nameof(IsComputerMode));
                }
            }
        }

        /// <summary>
        /// Gets or sets the board cells
        /// </summary>
        public ObservableCollection<BoardCell> Board
        {
            get => _board;
            set => SetProperty(ref _board, value);
        }

        /// <summary>
        /// Gets or sets the move history
        /// </summary>
        public ObservableCollection<string> MoveHistory
        {
            get => _moveHistory;
            set => SetProperty(ref _moveHistory, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new MainWindow instance
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize UI elements
            _whitePiecePanel = FindName("WhitePiecePanel") as StackPanel;
            _blackPiecePanel = FindName("BlackPiecePanel") as StackPanel;

            // Initialize game logic
            _gameLogic = new GameLogic();
            _gameLogic.BoardUpdated += OnGameLogicBoardUpdated;
            _gameLogic.MoveMade += OnGameLogicMoveMade;
            _gameLogic.GameEnded += OnGameLogicGameEnded;

            // Initialize game state
            Board = _gameLogic.GetCurrentBoard();
            InitializeTimers();
            StartTimers();
            UpdateStatusText();

            // Set initial difficulty
            if (DifficultyComboBox != null)
            {
                DifficultyComboBox.SelectedIndex = 2; // Medium difficulty
            }

            // Initial UI setup
            if (_whitePiecePanel != null) _whitePiecePanel.Visibility = Visibility.Collapsed;
            if (_blackPiecePanel != null) _blackPiecePanel.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Game Logic Event Handlers

        /// <summary>
        /// Handles the BoardUpdated event from GameLogic
        /// </summary>
        private void OnGameLogicBoardUpdated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(UpdateBoardUI);
        }

        /// <summary>
        /// Handles the MoveMade event from GameLogic
        /// </summary>
        private void OnGameLogicMoveMade(object? sender, MoveEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Board = _gameLogic.GetCurrentBoard();
                UpdateMoveHistory(e.MoveNotation);
                UpdateStatusText();
            });
        }

        /// <summary>
        /// Handles the GameEnded event from GameLogic
        /// </summary>
        private void OnGameLogicGameEnded(object? sender, GameEndEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                IsGameActive = false;
                StopTimers();

                string resultMessage;
                string title = "Кінець гри";
                MessageBoxImage icon = MessageBoxImage.Information;

                // Set the result message based on the end type
                switch (e.EndType)
                {
                    case GameEndType.Checkmate:
                        string winner = e.WinnerColor == "white" ? "Білі" : "Чорні";
                        resultMessage = $"{winner} оголосили мат!";
                        icon = MessageBoxImage.Exclamation;
                        break;

                    case GameEndType.Stalemate:
                        resultMessage = "Пат! Нічия.";
                        break;

                    case GameEndType.KingCaptured:
                        string captureWinner = e.WinnerColor == "white" ? "Білі" : "Чорні";
                        resultMessage = $"{captureWinner} виграли! Король захоплений.";
                        break;

                    default:
                        resultMessage = "Гра завершена!";
                        break;
                }

                // Show game over message and ask about new game
                MessageBoxResult result = MessageBox.Show(
                    resultMessage + "\n\nХочете почати нову гру?",
                    title,
                    MessageBoxButton.YesNo,
                    icon
                );

                if (result == MessageBoxResult.Yes)
                {
                    // Clear history BEFORE starting new game
                    ClearMoveHistory();
                    StartNewGame_Click(this, new RoutedEventArgs());
                }
                else
                {
                    Application.Current.Shutdown();
                }
            });
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Handles the start new game button click
        /// </summary>
        private void StartNewGame_Click(object sender, RoutedEventArgs e)
        {
            // Clear move history first
            ClearMoveHistory();

            // Set player color
            _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

            // Initialize the game
            InitializeGame();
            InitializeBoardUI();

            // Exit setup mode if active
            if (_isSetupPositionMode)
            {
                SetupPosition_Click(sender, e);
            }
        }

        /// <summary>
        /// Handles the clear board button click
        /// </summary>
        private void ClearBoard_Click(object sender, RoutedEventArgs e)
        {
            if (_isClearingFromComputerMode || ShowConfirmation("Ви впевнені, що хочете очистити дошку та історію ходів?"))
            {
                _gameLogic.InitializeGame();
                UpdateBoardUI();
                ClearMoveHistory();
                _currentPlayer = "white";
                UpdateStatusText();
                ResetTimers();
                StartTimers();
                _isGameActive = true;
                ClearHighlights();
            }
        }

        /// <summary>
        /// Handles the two players mode button click
        /// </summary>
        private void SetTwoPlayersMode_Click(object sender, RoutedEventArgs e)
        {
            ClearBoard_Click(sender, e);
            ClearMoveHistory();
            _isTwoPlayersMode = true;
            _isComputerMode = false;
            _gameLogic.SetComputerMode(false);

            if (DifficultyComboBox != null)
                DifficultyComboBox.Visibility = Visibility.Collapsed;

            IsComputerMode = false;
            UpdateStatusText();
            ResetTimers();
            StartTimers();
        }

        /// <summary>
        /// Handles the computer mode button click
        /// </summary>
        private void SetComputerMode_Click(object sender, RoutedEventArgs e)
        {
            string message = "Ви впевнені, що хочете перейти в режим гри проти комп'ютера?";
            if (!_isPositionLoaded) message += "\nУсі незбережені зміни будуть втрачені.";
            message += "\nОчистити дошку та розпочати нову гру?";

            if (ShowConfirmation(message))
            {
                _isClearingFromComputerMode = true;
                ClearBoard_Click(sender, e);
                _isComputerMode = true;
                _isTwoPlayersMode = false;
                _gameLogic.SetComputerMode(true);

                if (DifficultyComboBox != null)
                    DifficultyComboBox.Visibility = Visibility.Visible;

                IsComputerMode = true;
                UpdateStatusText();
                _currentPlayer = "white";
                StartTimers();
                _isPositionLoaded = false;
                _firstMoveAfterLoad = false;
                _isClearingFromComputerMode = false;
            }
        }

        /// <summary>
        /// Handles the setup position button click
        /// </summary>
        private void SetupPosition_Click(object sender, RoutedEventArgs e)
        {
            _isSetupPositionMode = !_isSetupPositionMode;

            if (_isSetupPositionMode)
            {
                // Enter setup mode
                InitializePiecePanels();

                if (_whitePiecePanel != null && _blackPiecePanel != null)
                {
                    // Show piece panels
                    _whitePiecePanel.Visibility = Visibility.Visible;
                    _blackPiecePanel.Visibility = Visibility.Visible;

                    // Update status
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "Режим розстановки фігур. Оберіть фігуру та клацніть на дошці, щоб розмістити.";
                }
            }
            else
            {
                // Exit setup mode
                if (_whitePiecePanel != null && _blackPiecePanel != null)
                {
                    _whitePiecePanel.Visibility = Visibility.Collapsed;
                    _blackPiecePanel.Visibility = Visibility.Collapsed;
                }

                _selectedPieceForPlacement = null;
                _selectedPieceButton = null;

                // Save the current setup to the game logic
                SaveCurrentSetupToGameLogic();

                // Update game state
                _currentPlayer = "white";
                UpdateStatusText();
            }
        }

        /// <summary>
        /// Handles the save position button click
        /// </summary>
        private void SavePosition_Click(object sender, RoutedEventArgs e)
        {
            string dateTimeString = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"шахи_{dateTimeString}.ches";

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = fileName,
                Filter = "Chess Position File (*.ches)|*.ches"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        // Save board configuration
                        for (int row = 0; row < 8; row++)
                        {
                            string rowString = "";
                            for (int col = 0; col < 8; col++)
                            {
                                BoardCell? cell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
                                if (cell != null && cell.Piece != null)
                                {
                                    // First char - color (w for white, b for black)
                                    char colorChar = cell.Piece.Color == "white" ? 'w' : 'b';

                                    // Second char - piece type
                                    char typeChar = cell.Piece.Type switch
                                    {
                                        "pawn" => 'p',
                                        "rook" => 'r',
                                        "knight" => 'n',
                                        "bishop" => 'b',
                                        "queen" => 'q',
                                        "king" => 'k',
                                        _ => '.'
                                    };

                                    rowString += $"{colorChar}{typeChar}";
                                }
                                else
                                {
                                    rowString += ".."; // Empty cell
                                }
                            }
                            writer.WriteLine(rowString);
                        }

                        // Save current player
                        writer.WriteLine($"CurrentPlayer:{_currentPlayer}");

                        // Save move history
                        if (MoveHistory != null && MoveHistory.Count > 0)
                        {
                            writer.WriteLine("MoveHistory:");
                            foreach (var move in MoveHistory)
                            {
                                writer.WriteLine(move);
                            }
                        }

                        MessageBox.Show("Позицію збережено.", "Збереження");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при збереженні позиції: {ex.Message}", "Помилка");
                }
            }
        }

        /// <summary>
        /// Handles the load position button click
        /// </summary>
        private void LoadPosition_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Chess Position File (*.ches)|*.ches"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openFileDialog.FileName);
                    if (lines.Length >= 8)
                    {
                        LoadGameFromFile(lines);
                    }
                    else
                    {
                        MessageBox.Show("Файл пошкоджено (замало рядків).", "Помилка");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні позиції: {ex.Message}", "Помилка");
                }
            }
        }

        /// <summary>
        /// Handles the difficulty selection change
        /// </summary>
        private void DifficultyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyComboBox?.SelectedItem is ComboBoxItem selectedItem && _gameLogic != null)
            {
                // Get difficulty from tag or content
                string difficultyText = selectedItem.Tag?.ToString() ?? selectedItem.Content?.ToString() ?? "Medium";

                _gameLogic.CurrentDifficulty = difficultyText switch
                {
                    "Easy" => GameLogic.ComputerDifficulty.Easy,
                    "Medium" => GameLogic.ComputerDifficulty.Medium,
                    "Hard" => GameLogic.ComputerDifficulty.Hard,
                    "Expert" => GameLogic.ComputerDifficulty.Expert,
                    _ => GameLogic.ComputerDifficulty.Random
                };
            }
        }

        /// <summary>
        /// Handles player color radio button checks
        /// </summary>
        private void PlayerColorRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

            // Reset the game
            InitializeGame();
            StartNewGame_Click(sender, e);
        }

        /// <summary>
        /// Handles the board flip checkbox changes
        /// </summary>
        private void FlipBoardCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IsBoardFlipped = (sender as CheckBox)?.IsChecked ?? false;
            UpdateBoardUI();
        }

        /// <summary>
        /// Handles clicks on the chess board
        /// </summary>
        private void BoardCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border clickedBorder) || !(clickedBorder.DataContext is BoardCell clickedCell))
                return;

            if (_isSetupPositionMode)
            {
                // Setup mode - place or remove pieces
                if (_selectedPieceForPlacement != null)
                {
                    PlacePieceInSetupMode(clickedCell);
                }
                else
                {
                    // No piece selected - remove piece at this location
                    foreach (var cell in Board)
                    {
                        if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                        {
                            cell.Piece = null;
                            break;
                        }
                    }
                }

                e.Handled = true;
            }
            else if (_isGameActive)
            {
                // Normal game play - show valid moves and initiate drag
                ShowValidMovesForPiece(clickedCell.Row, clickedCell.Col);

                // Don't start drag if no valid moves
                if (Board.Any(c => c.IsHighlighted))
                {
                    _dragStartPoint = e.GetPosition(null);
                    _draggedCell = clickedCell;

                    DataObject dragData = new DataObject(typeof(BoardCell), clickedCell);
                    DragDrop.DoDragDrop(clickedBorder, dragData, DragDropEffects.Move);
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles drag enter events on board cells
        /// </summary>
        private void BoardCell_DragEnter(object sender, DragEventArgs e)
        {
            if (!(sender is Border targetBorder) ||
                !(targetBorder.DataContext is BoardCell targetCell) ||
                _draggedCell == null ||
                targetCell == _draggedCell)
                return;

            e.Effects = targetCell.IsHighlighted ?
                DragDropEffects.Move : DragDropEffects.None;
        }

        /// <summary>
        /// Handles drop events on board cells
        /// </summary>
        private void BoardCell_Drop(object sender, DragEventArgs e)
        {
            if (!(sender is Border dropBorder) ||
                !(dropBorder.DataContext is BoardCell dropCell) ||
                _draggedCell == null ||
                dropCell == _draggedCell ||
                !_isGameActive ||
                !dropCell.IsHighlighted)
                return;

            // Try to make the move
            TryMove(_draggedCell, dropCell);
            _draggedCell = null;

            // Clear highlights
            ClearHighlights();
        }

        /// <summary>
        /// Handles piece button clicks during setup
        /// </summary>
        private void PieceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                string[] parts = tag.Split(',');
                if (parts.Length == 2)
                {
                    string pieceColor = parts[0];
                    string pieceType = parts[1];

                    // Select this piece for placement
                    _selectedPieceForPlacement = new Piece(pieceColor, pieceType);

                    // Update status
                    if (StatusTextBlock != null)
                    {
                        string colorName = pieceColor == "white" ? "Білий" : "Чорний";
                        string typeName = GetPieceTypeName(pieceType);
                        StatusTextBlock.Text = $"Обрано фігуру: {colorName} {typeName}. Клацніть на дошці, щоб розмістити.";
                    }

                    // Update button highlight
                    HighlightSelectedPieceButton(button);
                }
            }
        }

        #endregion

        #region Board UI Methods

        /// <summary>
        /// Updates the chess board UI
        /// </summary>
        private void UpdateBoardUI()
        {
            Dispatcher.Invoke(() =>
            {
                Board = _gameLogic.GetCurrentBoard();

                if (IsBoardFlipped)
                {
                    FlipBoardDisplay();
                }

                if (ChessBoardItemsControl != null)
                {
                    ChessBoardItemsControl.ItemsSource = Board;
                    ChessBoardItemsControl.Items.Refresh();
                }
            });
        }

        /// <summary>
        /// Flips the board display for visual representation
        /// </summary>
        private void FlipBoardDisplay()
        {
            var flippedBoard = new ObservableCollection<BoardCell>();

            for (int row = 7; row >= 0; row--)
            {
                for (int col = 7; col >= 0; col--)
                {
                    var originalCell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
                    if (originalCell != null)
                    {
                        flippedBoard.Add(originalCell);
                    }
                }
            }

            Board = flippedBoard;
        }

        /// <summary>
        /// Updates the status text
        /// </summary>
        private void UpdateStatusText()
        {
            if (StatusTextBlock != null)
            {
                string playerText = _currentPlayer == "white" ? "Білих" : "Чорних";

                if (_isSetupPositionMode)
                {
                    StatusTextBlock.Text = "Режим розстановки фігур. Оберіть фігуру та клацніть на дошці, щоб розмістити.";
                }
                else if (_isComputerMode)
                {
                    StatusTextBlock.Text = $"Режим проти комп'ютера. Хід {playerText}.";
                }
                else
                {
                    StatusTextBlock.Text = $"Режим для двох гравців. Хід {playerText}.";
                }
            }
        }

        /// <summary>
        /// Clears all highlights from the board
        /// </summary>
        private void ClearHighlights()
        {
            foreach (var cell in Board)
            {
                cell.IsHighlighted = false;
            }
        }

        /// <summary>
        /// Shows valid moves for a piece
        /// </summary>
        private void ShowValidMovesForPiece(int row, int col)
        {
            ClearHighlights();

            var selectedCell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
            if (selectedCell?.Piece == null) return;

            string pieceColor = selectedCell.Piece.Color;
            if (pieceColor != _currentPlayer) return;

            var validMoves = _gameLogic.Board.GetValidMovesForPiece(row, col);

            foreach (var (targetRow, targetCol) in validMoves)
            {
                var targetCell = Board.FirstOrDefault(c => c.Row == targetRow && c.Col == targetCol);
                if (targetCell != null)
                {
                    targetCell.IsHighlighted = true;
                }
            }
        }

        /// <summary>
        /// Places a piece on the board during setup mode
        /// </summary>
        private void PlacePieceInSetupMode(BoardCell clickedCell)
        {
            if (_selectedPieceForPlacement == null)
                return;

            foreach (var cell in Board)
            {
                if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                {
                    cell.Piece = _selectedPieceForPlacement.Clone();
                    break;
                }
            }
        }

        /// <summary>
        /// Highlights the selected piece button
        /// </summary>
        private void HighlightSelectedPieceButton(Button selectedButton)
        {
            // Remove highlight from previously selected button
            if (_selectedPieceButton != null)
            {
                _selectedPieceButton.BorderBrush = Brushes.Transparent;
                _selectedPieceButton.BorderThickness = new Thickness(1);
            }

            // Highlight the new button
            _selectedPieceButton = selectedButton;
            _selectedPieceButton.BorderBrush = Brushes.Green;
            _selectedPieceButton.BorderThickness = new Thickness(3);
        }

        /// <summary>
        /// Sets the board colors
        /// </summary>
        private void SetBoardColors(Color lightColor, Color darkColor)
        {
            _lightBoardColor = lightColor;
            _darkBoardColor = darkColor;
            UpdateBoardUI();
        }

        #endregion

        #region Move History Methods

        /// <summary>
        /// Updates the move history
        /// </summary>
        private void UpdateMoveHistory(string move)
        {
            MoveHistory.Add(move);

            if (MoveHistoryListBox != null)
            {
                MoveHistoryListBox.Items.Add(move);
                MoveHistoryListBox.ScrollIntoView(move);
            }
        }

        /// <summary>
        /// Clears the move history
        /// </summary>
        private void ClearMoveHistory()
        {
            MoveHistory.Clear();

            if (MoveHistoryListBox != null)
            {
                MoveHistoryListBox.Items.Clear();
            }
        }

        /// <summary>
        /// Updates move history from loaded data
        /// </summary>
        private void UpdateMoveHistoryFromLoaded(ObservableCollection<string> loadedHistory)
        {
            ClearMoveHistory();

            foreach (var move in loadedHistory)
            {
                UpdateMoveHistory(move);
            }
        }

        #endregion

        #region Game Management Methods

        /// <summary>
        /// Initializes the game
        /// </summary>
        private void InitializeGame()
        {
            _gameLogic.InitializeGame();
            _currentPlayer = "white";

            // Handle first move if computer plays white
            if (_isComputerMode && _playerPlaysBlack && _currentPlayer == "white")
            {
                _currentPlayer = "black";
                Dispatcher.BeginInvoke(new Action(() => _gameLogic.MakeComputerMove()));
            }

            MoveHistory.Clear();
            IsGameActive = true;
            GameResultText = "";
            ResetTimers();
            StartTimers();
            UpdateStatusText();
            _isPositionLoaded = false;
            _firstMoveAfterLoad = false;
        }

        /// <summary>
        /// Initializes the board UI
        /// </summary>
        private void InitializeBoardUI()
        {
            Board = _gameLogic.GetCurrentBoard();
        }

        /// <summary>
        /// Initializes the piece panels for setup mode
        /// </summary>
        private void InitializePiecePanels()
        {
            // Ensure panels exist
            if (_whitePiecePanel == null || _blackPiecePanel == null)
            {
                _whitePiecePanel = new StackPanel { Orientation = Orientation.Horizontal };
                _blackPiecePanel = new StackPanel { Orientation = Orientation.Horizontal };

                if (MainGrid != null)
                {
                    if (!MainGrid.Children.Contains(_whitePiecePanel))
                        MainGrid.Children.Add(_whitePiecePanel);

                    if (!MainGrid.Children.Contains(_blackPiecePanel))
                        MainGrid.Children.Add(_blackPiecePanel);

                    Grid.SetRow(_whitePiecePanel, 0);
                    Grid.SetColumn(_whitePiecePanel, 0);
                    Grid.SetRow(_blackPiecePanel, 0);
                    Grid.SetColumn(_blackPiecePanel, 0);

                    // Add margin to separate black pieces
                    _blackPiecePanel.Margin = new Thickness(250, 10, 10, 10);
                }
            }

            // Clear panels
            _whitePiecePanel.Children.Clear();
            _blackPiecePanel.Children.Clear();

            // Add piece buttons
            string[] pieceTypes = { "pawn", "rook", "knight", "bishop", "queen", "king" };

            foreach (var pieceType in pieceTypes)
            {
                CreatePieceButton("white", pieceType, _whitePiecePanel);
                CreatePieceButton("black", pieceType, _blackPiecePanel);
            }

            // Reset selection
            _selectedPieceForPlacement = null;
            _selectedPieceButton = null;
        }

        /// <summary>
        /// Creates a piece button for the setup panel
        /// </summary>
        private void CreatePieceButton(string pieceColor, string pieceType, StackPanel panel)
        {
            // Create a button for the piece
            Button pieceButton = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(2),
                Tag = $"{pieceColor},{pieceType}",
                ToolTip = $"{(pieceColor == "white" ? "Білий" : "Чорний")} {GetPieceTypeName(pieceType)}"
            };

            // Create the piece icon
            TextBlock pieceIcon = new TextBlock
            {
                FontSize = 24,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = new Piece(pieceColor, pieceType).GetUnicodeSymbol()
            };

            pieceButton.Content = pieceIcon;
            pieceButton.Click += PieceButton_Click;

            panel.Children.Add(pieceButton);
        }

        /// <summary>
        /// Saves the current board setup to the game logic
        /// </summary>
        private void SaveCurrentSetupToGameLogic()
        {
            // Create a new board representation
            Piece?[,] boardSetup = new Piece?[8, 8];

            // Copy pieces from the UI board to the logical board
            foreach (var cell in Board)
            {
                if (cell.Piece != null)
                {
                    boardSetup[cell.Row, cell.Col] = cell.Piece.Clone();
                }
            }

            // Load the board into the game logic
            _gameLogic.LoadGame(boardSetup, "white");

            // Update the UI board
            Board = _gameLogic.GetCurrentBoard();

            // Clear move history
            ClearMoveHistory();
        }

        /// <summary>
        /// Tries to make a move
        /// </summary>
        private void TryMove(BoardCell startCell, BoardCell endCell)
        {
            if (_gameLogic.TryMovePiece(startCell.Row, startCell.Col, endCell.Row, endCell.Col))
            {
                // Move was successful - update UI state
                ClearHighlights();
                _currentPlayer = _gameLogic.CurrentPlayer;
                UpdateStatusText();
                UpdateBoardUI();

                // Check if computer should move
                if (_isComputerMode && _isGameActive)
                {
                    CheckForComputerMove();
                }
            }
        }

        /// <summary>
        /// Checks if the computer should make a move and initiates it
        /// </summary>
        private void CheckForComputerMove()
        {
            // In normal computer mode
            string currentPlayer = _gameLogic.CurrentPlayer;
            bool isComputerTurn = (_playerPlaysBlack && currentPlayer == "white") ||
                                 (!_playerPlaysBlack && currentPlayer == "black");

            if (isComputerTurn)
            {
                _gameLogic.MakeComputerMove();
            }
            // Handle special case of continuing from a loaded position
            else if (_isPositionLoaded && _firstMoveAfterLoad)
            {
                _firstMoveAfterLoad = false;

                if (ShowConfirmation("Продовжити гру з поточної позиції проти комп'ютера?\nНатисніть 'Так' для продовження, 'Ні' - для початку нової гри."))
                {
                    _gameLogic.SetComputerMode(true);

                    if (DifficultyComboBox != null)
                        DifficultyComboBox.Visibility = Visibility.Visible;

                    UpdateStatusText();

                    // Check again for computer move
                    CheckForComputerMove();
                }
                else
                {
                    SetComputerMode_Click(this, new RoutedEventArgs());
                }
            }
        }

        /// <summary>
        /// Loads a game from file data
        /// </summary>
        private void LoadGameFromFile(string[] lines)
        {
            Piece?[,] boardState = new Piece?[8, 8];

            // Parse board data
            for (int row = 0; row < 8; row++)
            {
                if (row < lines.Length)
                {
                    ParseBoardRow(lines[row], row, boardState);
                }
            }

            // Parse metadata
            string currentPlayer = "white";
            ObservableCollection<string> moveHistory = new ObservableCollection<string>();

            for (int i = 8; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("CurrentPlayer:"))
                {
                    currentPlayer = lines[i].Substring("CurrentPlayer:".Length);
                }
                else if (lines[i] == "MoveHistory:")
                {
                    continue; // Next lines are move history
                }
                else if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    moveHistory.Add(lines[i]);
                }
            }

            // Load the game
            _gameLogic.LoadGame(boardState, currentPlayer);
            Board = _gameLogic.GetCurrentBoard();
            _currentPlayer = currentPlayer;
            UpdateStatusText();
            UpdateMoveHistoryFromLoaded(moveHistory);
            _isPositionLoaded = true;
            _firstMoveAfterLoad = true;
            ResetTimers();
            StartTimers();

            MessageBox.Show("Позицію гри завантажено.", "Завантаження");
        }

        /// <summary>
        /// Parses a board row from file data
        /// </summary>
        private void ParseBoardRow(string line, int row, Piece?[,] boardState)
        {
            // Board file formats:
            // - Old: 8 chars (1 per cell)
            // - New: 16 chars (2 per cell)

            if (line.Length == 8) // Old format
            {
                for (int col = 0; col < 8; col++)
                {
                    boardState[row, col] = DecodePieceOldFormat(line[col].ToString());
                }
            }
            else if (line.Length == 16) // New format
            {
                for (int col = 0; col < 8; col++)
                {
                    int idx = col * 2;
                    string pieceCode = line.Substring(idx, 2);
                    boardState[row, col] = DecodePieceNewFormat(pieceCode);
                }
            }
            else
            {
                throw new FormatException("Invalid board row format");
            }
        }

        /// <summary>
        /// Decodes a piece from the old file format
        /// </summary>
        private Piece? DecodePieceOldFormat(string code)
        {
            if (code == ".") return null;

            char pieceChar = code[0];

            return pieceChar switch
            {
                'P' => new Piece("white", "pawn"),
                'R' => new Piece("white", "rook"),
                'N' => new Piece("white", "knight"),
                'B' => new Piece("white", "bishop"),
                'Q' => new Piece("white", "queen"),
                'K' => new Piece("white", "king"),
                'p' => new Piece("black", "pawn"),
                'r' => new Piece("black", "rook"),
                'n' => new Piece("black", "knight"),
                'b' => new Piece("black", "bishop"),
                'q' => new Piece("black", "queen"),
                'k' => new Piece("black", "king"),
                _ => null
            };
        }

        /// <summary>
        /// Decodes a piece from the new file format
        /// </summary>
        private Piece? DecodePieceNewFormat(string code)
        {
            if (code == ".." || code.Length != 2) return null;

            char colorChar = code[0];
            char typeChar = code[1];

            if (colorChar != 'w' && colorChar != 'b') return null;

            string color = colorChar == 'w' ? "white" : "black";
            string type = typeChar switch
            {
                'p' => "pawn",
                'r' => "rook",
                'n' => "knight",
                'b' => "bishop",
                'q' => "queen",
                'k' => "king",
                _ => ""
            };

            if (string.IsNullOrEmpty(type)) return null;

            return new Piece(color, type);
        }

        #endregion

        #region Timer Methods

        /// <summary>
        /// Initializes the chess clocks
        /// </summary>
        private void InitializeTimers()
        {
            _whiteTimeLeft = TimeSpan.FromMinutes(30);
            _blackTimeLeft = TimeSpan.FromMinutes(30);

            _whiteTimer = CreateTimer(WhiteTimerTick);
            _blackTimer = CreateTimer(BlackTimerTick);
            _isTimerRunning = false;

            UpdateTimersDisplay();
        }

        /// <summary>
        /// Creates a timer with the specified tick handler
        /// </summary>
        private DispatcherTimer CreateTimer(EventHandler tickHandler)
        {
            var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += tickHandler;
            return timer;
        }

        /// <summary>
        /// Starts the chess clocks
        /// </summary>
        private void StartTimers()
        {
            _whiteTimer?.Stop();
            _blackTimer?.Stop();

            if (_currentPlayer == "white")
                _whiteTimer?.Start();
            else
                _blackTimer?.Start();

            _isTimerRunning = true;
        }

        /// <summary>
        /// Stops the chess clocks
        /// </summary>
        private void StopTimers()
        {
            _whiteTimer?.Stop();
            _blackTimer?.Stop();
            _isTimerRunning = false;
        }

        /// <summary>
        /// Resets the chess clocks
        /// </summary>
        private void ResetTimers()
        {
            StopTimers();
            InitializeTimers();
        }

        /// <summary>
        /// Handles the white player's clock tick
        /// </summary>
        private void WhiteTimerTick(object? sender, EventArgs e)
        {
            UpdateTimeLeft(ref _whiteTimeLeft, WhiteTimeTextBlock, "Чорні перемогли за часом!");
        }

        /// <summary>
        /// Handles the black player's clock tick
        /// </summary>
        private void BlackTimerTick(object? sender, EventArgs e)
        {
            UpdateTimeLeft(ref _blackTimeLeft, BlackTimeTextBlock, "Білі перемогли за часом!");
        }

        /// <summary>
        /// Updates a player's remaining time
        /// </summary>
        private void UpdateTimeLeft(ref TimeSpan timeLeft, TextBlock? textBlock, string gameOverMessage)
        {
            if (timeLeft > TimeSpan.Zero)
            {
                timeLeft = timeLeft.Subtract(TimeSpan.FromSeconds(1));

                if (textBlock != null)
                    textBlock.Text = timeLeft.ToString(@"hh\:mm\:ss");
            }
            else
            {
                StopTimers();

                if (StatusTextBlock != null)
                    StatusTextBlock.Text = gameOverMessage;

                // End the game
                IsGameActive = false;

                // Show game over message
                MessageBox.Show(gameOverMessage, "Час вийшов", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Updates the timer displays
        /// </summary>
        private void UpdateTimersDisplay()
        {
            if (WhiteTimeTextBlock != null)
                WhiteTimeTextBlock.Text = _whiteTimeLeft.ToString(@"hh\:mm\:ss");

            if (BlackTimeTextBlock != null)
                BlackTimeTextBlock.Text = _blackTimeLeft.ToString(@"hh\:mm\:ss");
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Sets a property value and raises PropertyChanged if value changed
        /// </summary>
        private void SetProperty<T>(ref T field, T newValue, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        private bool ShowConfirmation(string message)
        {
            return MessageBox.Show(
                message,
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Gets a localized name for a piece type
        /// </summary>
        private string GetPieceTypeName(string pieceType)
        {
            return pieceType switch
            {
                "pawn" => "Пішак",
                "rook" => "Тура",
                "knight" => "Кінь",
                "bishop" => "Слон",
                "queen" => "Ферзь",
                "king" => "Король",
                _ => pieceType
            };
        }

        #endregion
    }

    /// <summary>
    /// Enum for player colors
    /// </summary>
    public enum ChessColor
    {
        White,
        Black
    }
}