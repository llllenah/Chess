using System;
using System.Collections.Generic;
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

        // Main panels for piece setup
        private StackPanel _whitePanel;
        private StackPanel _blackPanel;

        // Setup mode active pieces
        private Dictionary<string, Border> _whitePieces = new Dictionary<string, Border>();
        private Dictionary<string, Border> _blackPieces = new Dictionary<string, Border>();

        // UI state
        private Point _dragStartPoint;
        private BoardCell? _draggedCell;
        private bool _isBoardFlipped = false;
        private bool _isSetupPositionMode = false;
        private Piece? _selectedPieceForPlacement = null;
        private Border? _selectedPieceBorder = null;

        // Game state
        private bool _isGameActive = true;
        private string _gameResultText = "";
        private ObservableCollection<BoardCell> _board = new ObservableCollection<BoardCell>();
        private ObservableCollection<string> _moveHistory = new ObservableCollection<string>();
        private string _currentPlayer = "white";
        private bool _isPositionLoaded = false;
        private bool _firstMoveAfterLoad = false;
        private bool _isClearingFromComputerMode = false;

        // Game modes
        private bool _isTwoPlayersMode = false;
        private bool _isComputerMode = false;
        private ChessColor _playerColor = ChessColor.White;

        // Game logic
        private GameLogic _gameLogic;

        // Timers
        private DispatcherTimer? _whiteTimer;
        private DispatcherTimer? _blackTimer;
        private TimeSpan _whiteTimeLeft;
        private TimeSpan _blackTimeLeft;
        private bool _isTimerRunning;

        // Colors
        private Color _lightBoardColor = Brushes.LightGray.Color;
        private Color _darkBoardColor = Brushes.White.Color;

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
                _isComputerMode = value;
                OnPropertyChanged(nameof(IsComputerMode));
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

            // Initialize non-nullable fields
            _whitePanel = new StackPanel();
            _blackPanel = new StackPanel();
            SideGrid = new Grid();

            // Инициализация игровой логики
            _gameLogic = new GameLogic();
            _gameLogic.BoardUpdated += OnGameLogicBoardUpdated;
            _gameLogic.MoveMade += OnGameLogicMoveMade;
            _gameLogic.GameEnded += OnGameLogicGameEnded;
            _gameLogic.PawnPromotion += OnPawnPromotion;

            // Настройка диалога продвижения пешки
            _gameLogic.SetPromotionDialogCallback(ShowPromotionDialog);

            // Сначала создаем боковые панели, затем инициализируем их
            CreateSidePanels();
            InitializeSetupPanels();

            // Инициализация игрового состояния
            Board = _gameLogic.GetCurrentBoard();
            InitializeTimers();
            StartTimers();
            UpdateStatusText();

            // Устанавливаем начальную сложность
            if (DifficultyComboBox != null)
            {
                DifficultyComboBox.SelectedIndex = 2; // Средняя сложность
                DifficultyComboBox.Visibility = Visibility.Collapsed; // Изначально скрываем
            }
        }
        /// <summary>
        /// Creates the side panels for piece selection during setup
        /// </summary>
        private void CreateSidePanels()
        {
            // Create side grid for placing pieces
            Grid sideGrid = new Grid();
            sideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Увеличенная ширина до 150
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Create white pieces panel
            _whitePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 140, // Увеличенная ширина панели
                Margin = new Thickness(0) // Добавляем отступ
            };

            // Create black pieces panel
            _blackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 140, // Увеличенная ширина панели
                Margin = new Thickness(0) // Добавляем отступ
            };

            // Add panels to the grid
            sideGrid.Children.Add(_whitePanel);
            sideGrid.Children.Add(_blackPanel);
            Grid.SetRow(_whitePanel, 0);
            Grid.SetRow(_blackPanel, 1);

            // Add the side grid to the main grid
            MainGrid.Children.Add(sideGrid);
            Grid.SetColumn(sideGrid, 3);
            Grid.SetRow(sideGrid, 1);

            // Initially hide panels
            sideGrid.Visibility = Visibility.Collapsed;

            // Store reference to side grid
            SideGrid = sideGrid;
        }
        /// <summary>
        /// Side grid for piece selection
        /// </summary>
        private Grid SideGrid { get; set; }

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

        /// <summary>
        /// Handles the PawnPromotion event from GameLogic
        /// </summary>
        private void OnPawnPromotion(object? sender, PawnPromotionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Show the promotion dialog
                PawnPromotionDialog dialog = new PawnPromotionDialog(e.PawnColor);

                if (dialog.ShowDialog() == true)
                {
                    // Set the promotion piece
                    e.PromotionPiece = dialog.SelectedPieceType;
                }
                else
                {
                    // Promotion was cancelled
                    e.IsCancelled = true;
                }
            });
        }

        /// <summary>
        /// Shows the pawn promotion dialog
        /// </summary>
        /// <param name="pawnColor">Color of the pawn being promoted</param>
        /// <returns>The type of piece to promote to</returns>
        private string ShowPromotionDialog(string pawnColor)
        {
            PawnPromotionDialog dialog = new PawnPromotionDialog(pawnColor);

            if (dialog.ShowDialog() == true)
            {
                return dialog.SelectedPieceType;
            }

            // Default to queen if dialog was cancelled
            return "queen";
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

                // Set player color
                _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

                StartTimers();
                _isPositionLoaded = false;
                _firstMoveAfterLoad = false;
                _isClearingFromComputerMode = false;

                // If computer plays white, make first move
                if (_playerColor == ChessColor.Black && _currentPlayer == "white")
                {
                    _gameLogic.MakeComputerMove();
                }
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
                InitializeSetupPanels();

                // Show side panel
                if (SideGrid != null)
                {
                    SideGrid.Visibility = Visibility.Visible;

                    // Update status
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "Режим розстановки фігур. Оберіть фігуру та клацніть на дошці, щоб розмістити.";
                }
            }
            else
            {
                // Exit setup mode
                if (SideGrid != null)
                {
                    SideGrid.Visibility = Visibility.Collapsed;
                }

                _selectedPieceForPlacement = null;
                _selectedPieceBorder = null;

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
            InitializeBoardUI();

            // If computer plays first, make its move
            if (_isComputerMode && _playerColor == ChessColor.Black && _currentPlayer == "white")
            {
                _gameLogic.MakeComputerMove();
            }

            UpdateStatusText();
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
                // Режим расстановки - размещаем или удаляем фигуры
                if (_selectedPieceForPlacement != null)
                {
                    PlacePieceInSetupMode(clickedCell);
                }
                else
                {
                    // Фигура не выбрана - удаляем фигуру в этой позиции
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
                // Проверяем, чей сейчас ход
                if (!_gameLogic.IsPlayerTurn())
                {
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = $"Сейчас ход компьютера. Пожалуйста, подождите.";
                    return;
                }

                // Проверяем, что выбрана фигура текущего игрока
                var piece = clickedCell.Piece;
                if (piece == null || piece.Color != _currentPlayer)
                {
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = $"Сейчас ход {(_currentPlayer == "white" ? "белых" : "черных")}. Выберите свою фигуру.";
                    return;
                }

                // Обычный игровой процесс - показываем возможные ходы и начинаем перетаскивание
                ShowValidMovesForPiece(clickedCell.Row, clickedCell.Col);

                // Не начинаем перетаскивание, если нет доступных ходов
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
        /// Handles piece selection during setup
        /// </summary>
        private void PieceSetup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Border pieceBorder && pieceBorder.Tag is string pieceInfo)
            {
                string[] parts = pieceInfo.Split(',');
                if (parts.Length == 2)
                {
                    string pieceColor = parts[0];
                    string pieceType = parts[1];

                    // Select this piece for placement
                    _selectedPieceForPlacement = new Piece(pieceColor, pieceType);

                    // Highlight this piece border
                    HighlightSelectedPieceBorder(pieceBorder);

                    // Update status
                    if (StatusTextBlock != null)
                    {
                        string colorName = pieceColor == "white" ? "Білий" : "Чорний";
                        string typeName = GetPieceTypeName(pieceType);
                        StatusTextBlock.Text = $"Обрано фігуру: {colorName} {typeName}. Клацніть на дошці, щоб розмістити.";
                    }
                }
            }
        }

        /// <summary>
        /// Handles the remove button click during setup
        /// </summary>
        private void RemovePieceButton_Click(object sender, RoutedEventArgs e)
        {
            // Deselect current piece
            _selectedPieceForPlacement = null;

            if (_selectedPieceBorder != null)
            {
                _selectedPieceBorder.BorderBrush = Brushes.Transparent;
                _selectedPieceBorder.BorderThickness = new Thickness(1);
                _selectedPieceBorder = null;
            }

            // Update status
            if (StatusTextBlock != null)
                StatusTextBlock.Text = "Режим видалення. Клацніть на фігуру на дошці, щоб видалити її.";
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
                    // Add indication of whose turn it is
                    bool isPlayerTurn = _gameLogic.IsPlayerTurn();
                    string turnInfo = isPlayerTurn ? "Ваш хід" : "Хід комп'ютера";
                    StatusTextBlock.Text = $"Режим проти комп'ютера. Хід {playerText}. {turnInfo}.";
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
        /// <param name="row">Row of the piece</param>
        /// <param name="col">Column of the piece</param>
        private void ShowValidMovesForPiece(int row, int col)
        {
            // First check if it's the player's turn
            if (!_gameLogic.IsPlayerTurn())
                return;

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
        /// <param name="clickedCell">The cell to place the piece on</param>
        private void PlacePieceInSetupMode(BoardCell clickedCell)
        {
            if (_selectedPieceForPlacement == null)
                return;

            // Проверка на ограничения количества фигур
            if (!CanPlacePiece(_selectedPieceForPlacement.Type, _selectedPieceForPlacement.Color))
            {
                string localizedType = GetLocalizedPieceType(_selectedPieceForPlacement.Type);
                string localizedColor = _selectedPieceForPlacement.Color == "white" ? "білих" : "чорних";
                MessageBox.Show($"Досягнуто максимальна кількість {localizedType} для {localizedColor}!",
                                "Обмеження фігур",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            // Если на клетке уже есть фигура, сначала удаляем её из счётчика
            Piece? existingPiece = null;
            foreach (var cell in Board)
            {
                if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                {
                    existingPiece = cell.Piece;
                    break;
                }
            }

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
        /// Проверяет, можно ли разместить ещё одну фигуру данного типа и цвета
        /// </summary>
        /// <param name="pieceType">Тип фигуры</param>
        /// <param name="pieceColor">Цвет фигуры</param>
        /// <returns>True, если можно разместить, иначе false</returns>
        private bool CanPlacePiece(string pieceType, string pieceColor)
        {
            // Подсчитываем количество фигур данного типа и цвета на доске
            int count = 0;
            foreach (var cell in Board)
            {
                if (cell.Piece != null &&
                    cell.Piece.Type == pieceType &&
                    cell.Piece.Color == pieceColor)
                {
                    count++;
                }
            }

            // Устанавливаем ограничения в зависимости от типа фигуры
            switch (pieceType)
            {
                case "king":
                    return count < 1;  // Только 1 король каждого цвета
                case "queen":
                    return count < 9;  // Максимум 9 ферзей (из пешек)
                case "rook":
                    return count < 2;  // 2 ладьи
                case "bishop":
                    return count < 2;  // 2 слона
                case "knight":
                    return count < 2;  // 2 коня
                case "pawn":
                    return count < 8;  // 8 пешек
                default:
                    return true;
            }
        }

        /// <summary>
        /// Возвращает локализованное название типа фигуры
        /// </summary>
        /// <param name="pieceType">Тип фигуры</param>
        /// <returns>Локализованное название</returns>
        private string GetLocalizedPieceType(string pieceType)
        {
            return pieceType switch
            {
                "king" => "королів",
                "queen" => "ферзів",
                "rook" => "ладей",
                "bishop" => "слонів",
                "knight" => "коней",
                "pawn" => "пішаків",
                _ => pieceType
            };
        }

        /// <summary>
        /// Highlights the selected piece border
        /// </summary>
        /// <param name="selectedBorder">The border to highlight</param>
        private void HighlightSelectedPieceBorder(Border selectedBorder)
        {
            // Remove highlight from previously selected border
            if (_selectedPieceBorder != null)
            {
                _selectedPieceBorder.BorderBrush = Brushes.Transparent;
                _selectedPieceBorder.BorderThickness = new Thickness(1);
            }

            // Highlight the new border
            _selectedPieceBorder = selectedBorder;
            _selectedPieceBorder.BorderBrush = Brushes.Green;
            _selectedPieceBorder.BorderThickness = new Thickness(3);
        }

        /// <summary>
        /// Sets the board colors
        /// </summary>
        /// <param name="lightColor">Light squares color</param>
        /// <param name="darkColor">Dark squares color</param>
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
        /// <param name="move">The move to add to history</param>
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
        /// <param name="loadedHistory">The loaded history to display</param>
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
        /// Initializes the panels for piece setup
        /// </summary>
        private void InitializeSetupPanels()
        {
            // Clear existing panels
            _whitePanel.Children.Clear();
            _blackPanel.Children.Clear();

            // Add title for white pieces
            TextBlock whiteTitle = new TextBlock
            {
                Text = "Білі фігури",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _whitePanel.Children.Add(whiteTitle);

            // Add title for black pieces
            TextBlock blackTitle = new TextBlock
            {
                Text = "Чорні фігури",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _blackPanel.Children.Add(blackTitle);

            // Create piece types
            string[] pieceTypes = { "king", "queen", "rook", "bishop", "knight", "pawn" };

            // Add white pieces
            foreach (var pieceType in pieceTypes)
            {
                CreatePieceSetupButton("white", pieceType, _whitePanel);
            }

            // Add black pieces
            foreach (var pieceType in pieceTypes)
            {
                CreatePieceSetupButton("black", pieceType, _blackPanel);
            }

            // Add remove button for both panels
            Button removeWhiteButton = new Button
            {
                Content = "Видалити",
                Margin = new Thickness(0, 5, 0, 0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 80
            };
            removeWhiteButton.Click += RemovePieceButton_Click;
            _whitePanel.Children.Add(removeWhiteButton);

            // Reset selection
            _selectedPieceForPlacement = null;
            _selectedPieceBorder = null;
        }
        /// <summary>
        /// Creates a button for piece setup
        /// </summary>
        /// <param name="pieceColor">Color of the piece</param>
        /// <param name="pieceType">Type of the piece</param>
        /// <param name="panel">Panel to add the button to</param>
        private void CreatePieceSetupButton(string pieceColor, string pieceType, StackPanel panel)
        {
            // Create a piece
            Piece piece = new Piece(pieceColor, pieceType);

            // Create border for the piece
            Border pieceBorder = new Border
            {
                Width = 50,  // Увеличенная ширина
                Height = 50, // Увеличенная высота
                Margin = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Tag = $"{pieceColor},{pieceType}",
                ToolTip = piece.GetLocalizedName()
            };

            // Add the piece icon
            TextBlock pieceIcon = new TextBlock
            {
                Text = piece.GetUnicodeSymbol(),
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 36,  // Увеличенный размер шрифта
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            pieceBorder.Child = pieceIcon;
            pieceBorder.MouseDown += PieceSetup_Click;

            // Add to panel
            panel.Children.Add(pieceBorder);

            // Store reference to the border
            string key = $"{pieceColor}_{pieceType}";
            if (pieceColor == "white")
                _whitePieces[key] = pieceBorder;
            else
                _blackPieces[key] = pieceBorder;
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
        /// <param name="startCell">Starting cell</param>
        /// <param name="endCell">Ending cell</param>
        private void TryMove(BoardCell startCell, BoardCell endCell)
        {
            // Check if it's the player's turn
            if (!_gameLogic.IsPlayerTurn())
            {
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Зараз не ваш хід!";
                return;
            }

            if (_gameLogic.TryMovePiece(startCell.Row, startCell.Col, endCell.Row, endCell.Col))
            {
                // Move was successful - update UI state
                ClearHighlights();
                _currentPlayer = _gameLogic.CurrentPlayer;
                UpdateStatusText();
                UpdateBoardUI();
            }
        }

        /// <summary>
        /// Loads a game from file data
        /// </summary>
        /// <param name="lines">Lines from the file</param>
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
        /// <param name="line">Line from the file</param>
        /// <param name="row">Row index</param>
        /// <param name="boardState">Board state to update</param>
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
        /// <param name="code">Piece code</param>
        /// <returns>The decoded piece, or null for empty</returns>
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
        /// <param name="code">Piece code</param>
        /// <returns>The decoded piece, or null for empty</returns>
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
        /// <param name="tickHandler">Handler for timer ticks</param>
        /// <returns>The created timer</returns>
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
        /// <param name="timeLeft">Reference to time left</param>
        /// <param name="textBlock">TextBlock to update</param>
        /// <param name="gameOverMessage">Message to show when time runs out</param>
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
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="field">Reference to the field</param>
        /// <param name="newValue">New value</param>
        /// <param name="propertyName">Name of the property</param>
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
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <returns>True if confirmed, false if cancelled</returns>
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
        /// <param name="pieceType">Type of the piece</param>
        /// <returns>Localized name</returns>
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