using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ChessTrainer;
using Microsoft.Win32;

namespace ChessTrainer
{
    /// <summary>
    /// Main window for the chess trainer application.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        // Panels for piece setup
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
        private bool _isAnalysisMode = false;
        private Piece? _selectedPieceForPlacement = null;
        private Border? _selectedPieceBorder = null;

        // Game state
        private bool _isGameActive = true;
        private string _gameResultText = "";
        private ObservableCollection<BoardCell> _board = new ObservableCollection<BoardCell>();
        private ObservableCollection<string> _moveHistory = new ObservableCollection<string>();
        private string _currentPlayer = "white";

        // Game modes
        private bool _isComputerMode = false;
        private ChessColor _playerColor = ChessColor.White;
        private bool _playerPlaysBlack = false;

        // Game logic
        private GameLogic _gameLogic;

        // Timers
        private DispatcherTimer? _whiteTimer;
        private DispatcherTimer? _blackTimer;
        private TimeSpan _whiteTimeLeft;
        private TimeSpan _blackTimeLeft;
        private bool _isTimersPaused = false;

        // Standard piece counts in chess
        private readonly Dictionary<string, int> _standardPieceCounts = new Dictionary<string, int>
        {
            { "king", 1 },
            { "queen", 1 },
            { "rook", 2 },
            { "bishop", 2 },
            { "knight", 2 },
            { "pawn", 8 }
        };

        private readonly Dictionary<string, int> _maxPieceCounts = new Dictionary<string, int>
        {
            { "king", 1 },
            { "queen", 9 },
            { "rook", 10 },
            { "bishop", 10 },
            { "knight", 10 },
            { "pawn", 8 }
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the board is flipped.
        /// </summary>
        public bool IsBoardFlipped
        {
            get => _isBoardFlipped;
            set => SetProperty(ref _isBoardFlipped, value);
        }

        /// <summary>
        /// Gets or sets whether the game is active.
        /// </summary>
        public bool IsGameActive
        {
            get => _isGameActive;
            set => SetProperty(ref _isGameActive, value);
        }

        /// <summary>
        /// Gets or sets the game result text.
        /// </summary>
        public string GameResultText
        {
            get => _gameResultText;
            set => SetProperty(ref _gameResultText, value);
        }

        /// <summary>
        /// Gets or sets whether computer mode is enabled.
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
        /// Gets or sets the board cells.
        /// </summary>
        public ObservableCollection<BoardCell> Board
        {
            get => _board;
            set => SetProperty(ref _board, value);
        }

        /// <summary>
        /// Gets or sets the move history.
        /// </summary>
        public ObservableCollection<string> MoveHistory
        {
            get => _moveHistory;
            set => SetProperty(ref _moveHistory, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Creates a new MainWindow instance.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            this.Width = 1100;

            _whitePanel = new StackPanel();
            _blackPanel = new StackPanel();
            SideGrid = new Grid();

            _gameLogic = new GameLogic();
            _gameLogic.BoardUpdated += OnGameLogicBoardUpdated;
            _gameLogic.MoveMade += OnGameLogicMoveMade;
            _gameLogic.GameEnded += OnGameLogicGameEnded;
            _gameLogic.PawnPromotion += OnPawnPromotion;

            _gameLogic.SetPromotionDialogCallback(ShowPromotionDialog);

            CreateSidePanels();
            InitializeSetupPanels();

            Board = _gameLogic.GetCurrentBoard();
            InitializeTimers();
            StartTimers();
            UpdateStatusText();

            if (DifficultyComboBox != null)
            {
                DifficultyComboBox.SelectionChanged -= DifficultyComboBox_SelectionChanged;

                DifficultyComboBox.SelectedIndex = 2;

                DifficultyComboBox.SelectionChanged += DifficultyComboBox_SelectionChanged;

                DifficultyComboBox.Visibility = Visibility.Collapsed;
            }

            _isComputerMode = false;
            _gameLogic.SetComputerMode(false);
            IsComputerMode = false;

            _isGameActive = true;
            _isAnalysisMode = false;
            _isSetupPositionMode = false;
        }

        /// <summary>
        /// Handles the difficulty selection change.
        /// </summary>
        private void DifficultyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyComboBox?.SelectedItem is ComboBoxItem selectedItem && _gameLogic != null)
            {
                string difficultyText = selectedItem.Tag?.ToString() ?? selectedItem.Content?.ToString() ?? "Medium";

                MessageBoxResult result = MessageBox.Show(
                    $"Change difficulty to {difficultyText}? This will start a new game.",
                    "Change Difficulty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _gameLogic.CurrentDifficulty = difficultyText switch
                    {
                        "Random" => GameLogic.ComputerDifficulty.Random,
                        "Easy" => GameLogic.ComputerDifficulty.Easy,
                        "Medium" => GameLogic.ComputerDifficulty.Medium,
                        "Hard" => GameLogic.ComputerDifficulty.Hard,
                        "Expert" => GameLogic.ComputerDifficulty.Expert,
                        _ => GameLogic.ComputerDifficulty.Medium
                    };

                    ClearMoveHistory();

                    _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                    _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;
                    _currentPlayer = "white";
                    _gameLogic.InitializeGame();
                    InitializeBoardUI();
                    UpdateBoardUI();

                    ResetTimers();
                    _isTimersPaused = false;

                    if (TimerControlButton != null)
                        TimerControlButton.Content = "Pause Timers";

                    _isGameActive = true;

                    bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                        (!_playerPlaysBlack && _currentPlayer == "black");

                    if (isComputerTurn)
                    {
                        Task.Run(() =>
                        {
                            System.Threading.Thread.Sleep(500);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _gameLogic.MakeComputerMove();
                            });
                        });
                    }

                    ForceRefreshBoardState();
                }
                else
                {
                    GameLogic.ComputerDifficulty currentDifficulty = _gameLogic.CurrentDifficulty;

                    for (int i = 0; i < DifficultyComboBox.Items.Count; i++)
                    {
                        if (DifficultyComboBox.Items[i] is ComboBoxItem item)
                        {
                            string diffText = item.Tag?.ToString() ?? "";
                            if (diffText == currentDifficulty.ToString())
                            {
                                DifficultyComboBox.SelectionChanged -= DifficultyComboBox_SelectionChanged;

                                DifficultyComboBox.SelectedIndex = i;

                                DifficultyComboBox.SelectionChanged += DifficultyComboBox_SelectionChanged;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the board flip checkbox changes.
        /// </summary>
        private void FlipBoardCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IsBoardFlipped = (sender as CheckBox)?.IsChecked ?? false;
            UpdateBoardUI();
        }

        /// <summary>
        /// Creates the side panels for piece selection during setup with dynamic sizing
        /// </summary>
        // Modifications to MainWindow.xaml.cs

        private void CreateSidePanels()
        {
            Grid sideGrid = new Grid();

            sideGrid.Width = double.NaN;
            sideGrid.MinWidth = 200;
            sideGrid.MaxWidth = 350;

            sideGrid.ColumnDefinitions.Add(new ColumnDefinition());
            sideGrid.ColumnDefinitions.Add(new ColumnDefinition());

            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock setupHeader = new TextBlock
            {
                Text = "Piece Setup",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            ScrollViewer whiteScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(5)
            };

            ScrollViewer blackScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(5)
            };

            _whitePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            _blackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock whiteTitle = new TextBlock
            {
                Text = "White Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _whitePanel.Children.Add(whiteTitle);

            TextBlock blackTitle = new TextBlock
            {
                Text = "Black Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _blackPanel.Children.Add(blackTitle);

            whiteScroll.Content = _whitePanel;
            blackScroll.Content = _blackPanel;

            sideGrid.Children.Add(whiteScroll);
            sideGrid.Children.Add(blackScroll);
            Grid.SetColumn(whiteScroll, 0);
            Grid.SetColumn(blackScroll, 1);

            Border setupBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Colors.WhiteSmoke),
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            StackPanel setupContainer = new StackPanel();
            setupContainer.Children.Add(setupHeader);
            setupContainer.Children.Add(sideGrid);
            setupBorder.Child = setupContainer;

            MainGrid.Children.Add(setupBorder);
            Grid.SetColumn(setupBorder, 3);
            Grid.SetRow(setupBorder, 1);

            setupBorder.Visibility = Visibility.Collapsed;

            SideGrid = setupBorder;
        }

        /// <summary>
        /// Side grid for piece selection.
        /// </summary>
        private UIElement SideGrid { get; set; }

        /// <summary>
        /// Initializes the panels for piece setup.
        /// </summary>
        private void InitializeSetupPanels()
        {
            _whitePanel.Children.Clear();
            _blackPanel.Children.Clear();
            _whitePieces.Clear();
            _blackPieces.Clear();

            TextBlock whiteTitle = new TextBlock
            {
                Text = "White Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _whitePanel.Children.Add(whiteTitle);

            TextBlock blackTitle = new TextBlock
            {
                Text = "Black Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _blackPanel.Children.Add(blackTitle);

            string[] pieceTypes = { "king", "queen", "rook", "bishop", "knight", "pawn" };

            foreach (var pieceType in pieceTypes)
            {
                CreatePieceSetupButton("white", pieceType, _whitePanel);
            }

            foreach (var pieceType in pieceTypes)
            {
                CreatePieceSetupButton("black", pieceType, _blackPanel);
            }

            Button removeWhiteButton = new Button
            {
                Content = "Remove",
                Margin = new Thickness(0, 10, 0, 5),
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 100,
                Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            removeWhiteButton.Click += RemovePieceButton_Click;
            _whitePanel.Children.Add(removeWhiteButton);

            _selectedPieceForPlacement = null;
            _selectedPieceBorder = null;

            RefreshSetupPanelDisplay();
        }

        /// <summary>
        /// Creates a button for piece setup with improved text visibility.
        /// </summary>
        /// <param name="pieceColor">Color of the piece.</param>
        /// <param name="pieceType">Type of the piece.</param>
        /// <param name="panel">Panel to add the button to.</param>
        private void CreatePieceSetupButton(string pieceColor, string pieceType, StackPanel panel)
        {
            Piece piece = new Piece(pieceColor, pieceType);

            Border pieceBorder = new Border
            {
                Width = 80,
                Height = 70,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Tag = $"{pieceColor},{pieceType}"
            };

            pieceBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                BlurRadius = 5,
                Opacity = 0.3
            };

            Grid container = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock pieceIcon = new TextBlock
            {
                Text = piece.GetUnicodeSymbol(),
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 36,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(pieceIcon, 0);

            TextBlock pieceName = new TextBlock
            {
                Text = GetPieceTypeName(pieceType),
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            Grid.SetRow(pieceName, 1);

            TextBlock countText = new TextBlock
            {
                Text = "0/1",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.DarkBlue
            };
            Grid.SetRow(countText, 2);

            container.Children.Add(pieceIcon);
            container.Children.Add(pieceName);
            container.Children.Add(countText);

            pieceBorder.Child = container;
            pieceBorder.MouseDown += PieceSetup_Click;

            panel.Children.Add(pieceBorder);

            string key = $"{pieceColor}_{pieceType}";
            if (pieceColor == "white")
                _whitePieces[key] = pieceBorder;
            else
                _blackPieces[key] = pieceBorder;
        }

        #endregion

        #region Game Logic Event Handlers

        /// <summary>
        /// Handles the BoardUpdated event from GameLogic.
        /// </summary>
        private void OnGameLogicBoardUpdated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateBoardUI();

                if (_isSetupPositionMode)
                {
                    RefreshSetupPanelDisplay();
                }
            });
        }

        /// <summary>
        /// Handles the MoveMade event from GameLogic.
        /// </summary>
        private void OnGameLogicMoveMade(object? sender, MoveEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateMoveHistory(e.MoveNotation);

                ForceRefreshBoardState();
            });
        }

        /// <summary>
        /// Handles the GameEnded event from GameLogic.
        /// </summary>
        private void OnGameLogicGameEnded(object? sender, GameEndEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                IsGameActive = false;
                StopTimers();
                _isTimersPaused = true;

                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                string resultMessage;
                string title = "Game Over";
                MessageBoxImage icon = MessageBoxImage.Information;

                switch (e.EndType)
                {
                    case GameEndType.Checkmate:
                        string winner = e.WinnerColor == "white" ? "White" : "Black";
                        resultMessage = $"{winner} won by checkmate!";
                        icon = MessageBoxImage.Exclamation;
                        break;

                    case GameEndType.Stalemate:
                        resultMessage = "Stalemate! It's a draw.";
                        break;

                    case GameEndType.KingCaptured:
                        string captureWinner = e.WinnerColor == "white" ? "White" : "Black";
                        resultMessage = $"{captureWinner} won! King captured.";
                        break;

                    case GameEndType.Draw:
                        resultMessage = "Draw by 50-move rule.";
                        break;

                    case GameEndType.InsufficientMaterial:
                        resultMessage = "Draw by insufficient material. Neither side can checkmate.";
                        break;

                    default:
                        resultMessage = "Game Over!";
                        break;
                }

                MessageBoxResult result = MessageBox.Show(
                    resultMessage + "\n\nWould you like to start a new game?",
                    title,
                    MessageBoxButton.YesNo,
                    icon
                );

                if (result == MessageBoxResult.Yes)
                {
                    bool startNewGame = true;

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (startNewGame)
                        {
                            ClearMoveHistory();

                            _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;
                            _currentPlayer = "white";
                            _gameLogic.InitializeGame();
                            InitializeBoardUI();
                            UpdateBoardUI();

                            ResetTimers();
                            _isTimersPaused = false;

                            if (TimerControlButton != null)
                                TimerControlButton.Content = "Pause Timers";

                            _isGameActive = true;
                            ForceRefreshBoardState();
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Handles the PawnPromotion event from GameLogic.
        /// </summary>
        private void OnPawnPromotion(object? sender, PawnPromotionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PawnPromotionDialog dialog = new PawnPromotionDialog(e.PawnColor);

                if (dialog.ShowDialog() == true)
                {
                    e.PromotionPiece = dialog.SelectedPieceType;
                }
                else
                {
                    e.IsCancelled = true;
                }
            });
        }

        /// <summary>
        /// Shows the pawn promotion dialog.
        /// </summary>
        /// <param name="pawnColor">Color of the pawn to promote ("white" or "black").</param>
        /// <returns>The type of piece to promote to.</returns>
        private string ShowPromotionDialog(string pawnColor)
        {
            PawnPromotionDialog dialog = new PawnPromotionDialog(pawnColor);

            if (dialog.ShowDialog() == true)
            {
                return dialog.SelectedPieceType;
            }

            return "queen";
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Handles the save position button click.
        /// </summary>
        private void SavePosition_Click(object sender, RoutedEventArgs e)
        {
            string dateTimeString = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"chess_{dateTimeString}.ches";

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
                        for (int row = 0; row < 8; row++)
                        {
                            string rowString = "";
                            for (int col = 0; col < 8; col++)
                            {
                                BoardCell? cell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
                                if (cell != null && cell.Piece != null)
                                {
                                    char colorChar = cell.Piece.Color == "white" ? 'w' : 'b';

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
                                    rowString += "..";
                                }
                            }
                            writer.WriteLine(rowString);
                        }

                        writer.WriteLine($"CurrentPlayer:{_currentPlayer}");

                        if (MoveHistory != null && MoveHistory.Count > 0)
                        {
                            writer.WriteLine("MoveHistory:");
                            foreach (var move in MoveHistory)
                            {
                                writer.WriteLine(move);
                            }
                        }

                        MessageBox.Show("Position saved successfully.", "Save");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving position: {ex.Message}", "Error");
                }
            }
        }

        /// <summary>
        /// Handles the load position button click.
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
                        MessageBox.Show("File is corrupted (not enough lines).", "Error");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading position: {ex.Message}", "Error");
                }
            }
        }

        /// <summary>
        /// Handles the setup position button click.
        /// </summary>
        private void SetupPosition_Click(object sender, RoutedEventArgs e)
        {
            if (_isComputerMode && !_isSetupPositionMode)
            {
                MessageBox.Show("Position setup is only available in two-player mode. Please switch to two-player mode first.",
                                "Setup Mode Not Available",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            if (!_isSetupPositionMode)
            {
                _isSetupPositionMode = true;

                this.Width = 1460;

                StopTimers();
                _isTimersPaused = true;

                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                InitializeSetupPanels();

                if (SideGrid is UIElement sidePanel)
                {
                    sidePanel.Visibility = Visibility.Visible;

                    if (ClearBoardButton != null)
                        ClearBoardButton.Visibility = Visibility.Visible;

                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "Setup mode. Select a piece and click on the board to place it.";
                }

                if (sender is Button setupButton)
                    setupButton.Content = "Finish Setup";

                RefreshSetupPanelDisplay();
            }
            else
            {
                if (!ValidateAndExitSetupMode())
                {
                    return;
                }

                ExitSetupMode(sender);
            }
        }

        /// <summary>
        /// Gets user-friendly message for draw types
        /// </summary>
        /// <param name="endType">The type of game end</param>
        /// <returns>Descriptive message about the draw</returns>
        private string GetDrawMessage(GameEndType endType)
        {
            return endType switch
            {
                GameEndType.Stalemate => "Stalemate: The current player has no legal moves but is not in check.",
                GameEndType.InsufficientMaterial => "Insufficient material: Neither side has enough pieces to deliver checkmate.",
                GameEndType.Checkmate => "Checkmate: The current player's king is in check and has no legal moves.",
                GameEndType.Draw => "Draw by rule (50-move rule or other draw condition).",
                _ => "The game has ended."
            };
        }

        /// <summary>
        /// Handles clicks on the chess board.
        /// </summary>
        private void BoardCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border clickedBorder) || !(clickedBorder.DataContext is BoardCell clickedCell))
                return;

            if (_isSetupPositionMode)
            {
                if (_selectedPieceForPlacement != null)
                {
                    PlacePieceInSetupMode(clickedCell);
                }
                else
                {
                    foreach (var cell in Board)
                    {
                        if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                        {
                            cell.Piece = null;
                            break;
                        }
                    }

                    RefreshSetupPanelDisplay();
                }

                e.Handled = true;
            }
            else if (_isAnalysisMode)
            {
                ShowValidMovesForPiece(clickedCell.Row, clickedCell.Col);
                e.Handled = true;
            }
            else if (_isGameActive)
            {
                _currentPlayer = _gameLogic.CurrentPlayer;

                if (_isComputerMode && !_gameLogic.IsPlayerTurn())
                {
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "It's computer's turn. Please wait.";
                    return;
                }

                var piece = clickedCell.Piece;
                if (piece == null || piece.Color != _currentPlayer)
                {
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = $"It's {(_currentPlayer == "white" ? "White" : "Black")}'s turn. Please select a valid piece.";
                    return;
                }

                ShowValidMovesForPiece(clickedCell.Row, clickedCell.Col);

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
        /// Handles drag enter events on board cells.
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
        /// Handles drop events on board cells.
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

            TryMove(_draggedCell, dropCell);
            _draggedCell = null;

            ClearHighlights();
        }

        /// <summary>
        /// Handles piece selection during setup.
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

                    _selectedPieceForPlacement = new Piece(pieceColor, pieceType);

                    HighlightSelectedPieceBorder(pieceBorder);

                    if (StatusTextBlock != null)
                    {
                        string colorName = pieceColor == "white" ? "White" : "Black";
                        string typeName = GetPieceTypeName(pieceType);
                        StatusTextBlock.Text = $"Selected piece: {colorName} {typeName}. Click on the board to place.";
                    }
                }
            }
        }

        /// <summary>
        /// Handles the remove button click during setup.
        /// </summary>
        private void RemovePieceButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPieceForPlacement = null;

            if (_selectedPieceBorder != null)
            {
                _selectedPieceBorder.BorderBrush = Brushes.Transparent;
                _selectedPieceBorder.BorderThickness = new Thickness(1);
                _selectedPieceBorder = null;
            }

            if (StatusTextBlock != null)
                StatusTextBlock.Text = "Remove mode. Click on a piece on the board to remove it.";
        }

        #endregion

        #region Board UI Methods

        /// <summary>
        /// Updates the chess board UI.
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

                UpdateBoardLabels();

                UpdateTimerVisuals();
            });
        }

        /// <summary>
        /// Updates the board coordinate labels when flipping the board
        /// </summary>
        private void UpdateBoardLabels()
        {
            if (ChessBoardGrid == null) return;

            try
            {
                var topLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, "TopFileLabels");
                var bottomLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, "BottomFileLabels");
                var leftLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, "LeftRankLabels");
                var rightLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, "RightRankLabels");

                if (topLabels == null || bottomLabels == null || leftLabels == null || rightLabels == null)
                {
                    topLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, 0, 1);
                    bottomLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, 2, 1);
                    leftLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, 1, 0);
                    rightLabels = FindVisualChild<UniformGrid>(ChessBoardGrid, 1, 2);
                }

                if (topLabels == null || bottomLabels == null || leftLabels == null || rightLabels == null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(ChessBoardGrid); i++)
                    {
                        var child = VisualTreeHelper.GetChild(ChessBoardGrid, i);
                        if (child is UniformGrid grid)
                        {
                            int row = Grid.GetRow(grid);
                            int col = Grid.GetColumn(grid);

                            if (row == 0 && col == 1) topLabels = grid;
                            else if (row == 2 && col == 1) bottomLabels = grid;
                            else if (row == 1 && col == 0) leftLabels = grid;
                            else if (row == 1 && col == 2) rightLabels = grid;
                        }
                    }
                }

                if (topLabels == null || bottomLabels == null || leftLabels == null || rightLabels == null)
                {
                    System.Diagnostics.Debug.WriteLine("Could not find board coordinate labels");
                    return;
                }

                char[] files = IsBoardFlipped ?
                    new[] { 'h', 'g', 'f', 'e', 'd', 'c', 'b', 'a' } :
                    new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

                char[] ranks = IsBoardFlipped ?
                    new[] { '1', '2', '3', '4', '5', '6', '7', '8' } :
                    new[] { '8', '7', '6', '5', '4', '3', '2', '1' };

                for (int i = 0; i < 8 && i < topLabels.Children.Count && i < bottomLabels.Children.Count; i++)
                {
                    if (topLabels.Children[i] is TextBlock topBlock)
                        topBlock.Text = files[i].ToString();

                    if (bottomLabels.Children[i] is TextBlock bottomBlock)
                        bottomBlock.Text = files[i].ToString();
                }

                for (int i = 0; i < 8 && i < leftLabels.Children.Count && i < rightLabels.Children.Count; i++)
                {
                    if (leftLabels.Children[i] is TextBlock leftBlock)
                        leftBlock.Text = ranks[i].ToString();

                    if (rightLabels.Children[i] is TextBlock rightBlock)
                        rightBlock.Text = ranks[i].ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating board labels: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a visual child of a specific type in a grid by name.
        /// </summary>
        private T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                {
                    return element;
                }

                if (child is DependencyObject container)
                {
                    T? result = FindVisualChildByName<T>(container, name);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }


        /// <summary>
        /// Finds a visual child of a specific type in a grid by name
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                {
                    return element;
                }

                if (child is DependencyObject container)
                {
                    T? result = FindVisualChild<T>(container, name);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }


        /// <summary>
        /// Finds a visual child of a specific type in a grid at specified row and column
        /// </summary>
        private T? FindVisualChild<T>(Grid parent, int row, int column) where T : UIElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element &&
                Grid.GetRow(element) == row &&
                Grid.GetColumn(element) == column)
                {
                    return element;
                }

                if (child is DependencyObject container)
                {
                    T? result = FindVisualChild<T>(container);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a visual child of a specific type
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : Visual
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element)
                    return element;

                T? result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Updates timer visual indicators.
        /// </summary>
        private void UpdateTimerVisuals()
        {
            try
            {
                bool whiteActive = _currentPlayer == "white" && !_isTimersPaused && _isGameActive;
                bool blackActive = _currentPlayer == "black" && !_isTimersPaused && _isGameActive;

                bool showWhiteTimer = true;
                bool showBlackTimer = true;

                if (_isComputerMode)
                {
                    showWhiteTimer = !_playerPlaysBlack;
                    showBlackTimer = _playerPlaysBlack;
                }

                if (WhiteTimerBorder != null)
                    WhiteTimerBorder.Visibility = showWhiteTimer ? Visibility.Visible : Visibility.Collapsed;

                if (BlackTimerBorder != null)
                    BlackTimerBorder.Visibility = showBlackTimer ? Visibility.Visible : Visibility.Collapsed;

                if (WhiteActiveIndicator != null)
                    WhiteActiveIndicator.Visibility = (whiteActive && showWhiteTimer) ? Visibility.Visible : Visibility.Collapsed;

                if (BlackActiveIndicator != null)
                    BlackActiveIndicator.Visibility = (blackActive && showBlackTimer) ? Visibility.Visible : Visibility.Collapsed;

                if (WhiteTimerBorder != null && BlackTimerBorder != null)
                {
                    var defaultBorderBrush = new SolidColorBrush(Color.FromRgb(189, 189, 189));
                    var activeBorderBrush = new SolidColorBrush(Colors.Green);

                    if (showWhiteTimer)
                        WhiteTimerBorder.BorderBrush = whiteActive ? activeBorderBrush : defaultBorderBrush;
                    if (showBlackTimer)
                        BlackTimerBorder.BorderBrush = blackActive ? activeBorderBrush : defaultBorderBrush;
                }
            }
            catch
            {
            }
        }
        /// <summary>
        /// Updates timer visibility based on game mode
        /// </summary>
        private void UpdateTimerModeVisibility()
        {
            if (_isComputerMode)
            {
                if (WhiteTimerBorder != null)
                    WhiteTimerBorder.Visibility = !_playerPlaysBlack ? Visibility.Visible : Visibility.Collapsed;

                if (BlackTimerBorder != null)
                    BlackTimerBorder.Visibility = _playerPlaysBlack ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                if (WhiteTimerBorder != null)
                    WhiteTimerBorder.Visibility = Visibility.Visible;

                if (BlackTimerBorder != null)
                    BlackTimerBorder.Visibility = Visibility.Visible;
            }

            UpdateTimerVisuals();
        }

        /// <summary>
        /// Flips the board display for visual representation.
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
            UpdateBoardLabels();
        }

        /// <summary>
        /// Updates the status text.
        /// </summary>
        private void UpdateStatusText()
        {
            if (StatusTextBlock != null)
            {
                string playerText = _currentPlayer == "white" ? "White" : "Black";

                if (_isSetupPositionMode)
                {
                    StatusTextBlock.Text = "Setup mode. Select a piece and click on the board to place it.";
                }
                else if (_isAnalysisMode)
                {
                    StatusTextBlock.Text = "Position analysis mode. Timers are paused.";
                }
                else if (_isComputerMode)
                {
                    bool isPlayerTurn = _gameLogic.IsPlayerTurn();
                    string turnInfo = isPlayerTurn ? "Your turn" : "Computer's turn";
                    StatusTextBlock.Text = $"Computer mode. {playerText} to move. {turnInfo}.";
                }
                else
                {
                    StatusTextBlock.Text = $"Two players mode. {playerText} to move.";
                }

                if (_isTimersPaused)
                {
                    StatusTextBlock.Text += " (Timers paused)";
                }
            }
        }

        /// <summary>
        /// Clears all highlights from the board.
        /// </summary>
        private void ClearHighlights()
        {
            foreach (var cell in Board)
            {
                cell.IsHighlighted = false;
            }
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if value changed.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="field">Reference to the field.</param>
        /// <param name="newValue">New value.</param>
        /// <param name="propertyName">Name of the property.</param>
        private void SetProperty<T>(ref T field, T newValue, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Shows valid moves for a piece.
        /// </summary>
        /// <param name="row">Row of the piece.</param>
        /// <param name="col">Column of the piece.</param>
        private void ShowValidMovesForPiece(int row, int col)
        {
            if (!_isAnalysisMode && !_gameLogic.IsPlayerTurn())
                return;

            ClearHighlights();

            var selectedCell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
            if (selectedCell?.Piece == null) return;

            string pieceColor = selectedCell.Piece.Color;
            if (!_isAnalysisMode && pieceColor != _currentPlayer) return;

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
        /// Updates a single piece type's display with count information.
        /// </summary>
        private void UpdatePieceSetupDisplay(string pieceColor, string pieceType)
        {
            string key = $"{pieceColor}_{pieceType}";
            Dictionary<string, Border> pieceDict = pieceColor == "white" ? _whitePieces : _blackPieces;

            if (pieceDict.TryGetValue(key, out Border? pieceBorder) && pieceBorder != null)
            {
                int currentCount = Board.Count(c => c.Piece != null &&
                                               c.Piece.Type == pieceType &&
                                               c.Piece.Color == pieceColor);

                int standardCount = _standardPieceCounts.TryGetValue(pieceType, out int std) ? std : 1;
                int maxCount = GetActualMaxPieceCount(pieceType);

                string standardText = $"Standard: {standardCount}";
                string currentText = $"Current: {currentCount}";
                string maxText = $"Maximum: {maxCount}";

                pieceBorder.ToolTip = $"{GetPieceTypeName(pieceType)}\n{standardText}\n{currentText}\n{maxText}";

                if (currentCount >= maxCount)
                {
                    pieceBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                    pieceBorder.BorderThickness = new Thickness(2);
                    pieceBorder.Opacity = 0.4;
                }
                else if (currentCount >= standardCount)
                {
                    pieceBorder.BorderBrush = new SolidColorBrush(Colors.Orange);
                    pieceBorder.BorderThickness = new Thickness(2);
                    pieceBorder.Opacity = 0.8;
                }
                else
                {
                    if (pieceBorder != _selectedPieceBorder)
                    {
                        pieceBorder.BorderBrush = Brushes.Transparent;
                        pieceBorder.BorderThickness = new Thickness(1);
                    }
                    pieceBorder.Opacity = 1.0;
                }

                if (pieceBorder.Child is Grid container && container.Children.Count >= 3)
                {
                    if (container.Children[2] is TextBlock countTextBlock)
                    {
                        countTextBlock.Text = $"{currentCount}/{maxCount}";

                        if (currentCount >= maxCount)
                        {
                            countTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                            countTextBlock.FontWeight = FontWeights.Bold;
                        }
                        else if (currentCount >= standardCount)
                        {
                            countTextBlock.Foreground = new SolidColorBrush(Colors.OrangeRed);
                            countTextBlock.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            countTextBlock.Foreground = new SolidColorBrush(Colors.DarkBlue);
                            countTextBlock.FontWeight = FontWeights.Normal;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns localized piece type name.
        /// </summary>
        /// <param name="pieceType">Type of piece.</param>
        /// <returns>Localized name.</returns>
        private string GetPieceTypeName(string pieceType)
        {
            return pieceType switch
            {
                "king" => "King",
                "queen" => "Queen",
                "rook" => "Rook",
                "bishop" => "Bishop",
                "knight" => "Knight",
                "pawn" => "Pawn",
                _ => pieceType
            };
        }

        /// <summary>
        /// Highlights the selected piece border.
        /// </summary>
        /// <param name="selectedBorder">The border to highlight.</param>
        private void HighlightSelectedPieceBorder(Border selectedBorder)
        {
            if (_selectedPieceBorder != null)
            {
                _selectedPieceBorder.BorderBrush = Brushes.Transparent;
                _selectedPieceBorder.BorderThickness = new Thickness(1);
            }

            _selectedPieceBorder = selectedBorder;
            _selectedPieceBorder.BorderBrush = Brushes.Green;
            _selectedPieceBorder.BorderThickness = new Thickness(3);
        }

        /// <summary>
        /// Places a piece on the board during setup mode.
        /// </summary>
        /// <param name="clickedCell">The cell to place the piece on.</param>
        private void PlacePieceInSetupMode(BoardCell clickedCell)
        {
            if (_selectedPieceForPlacement == null)
                return;

            string pieceType = _selectedPieceForPlacement.Type;
            string pieceColor = _selectedPieceForPlacement.Color;

            if (clickedCell.Piece != null &&
                clickedCell.Piece.Type == pieceType &&
                clickedCell.Piece.Color == pieceColor)
            {
                return;
            }

            bool isReplacement = clickedCell.Piece != null;

            int totalPiecesOfColor = GetTotalPiecesForColor(pieceColor);
            if (!isReplacement && totalPiecesOfColor >= 16)
            {
                string localizedColor = pieceColor == "white" ? "White" : "Black";
                MessageBox.Show($"Cannot place more pieces! {localizedColor} already has the maximum of 16 pieces on the board.\n\n" +
                               $"Current {localizedColor.ToLower()} pieces: {totalPiecesOfColor}/16\n\n" +
                               $"To place more pieces, you need to remove some existing {localizedColor.ToLower()} pieces first.",
                                "Maximum Total Pieces Reached",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            int currentCount = Board.Count(c => c.Piece != null &&
                                           c.Piece.Type == pieceType &&
                                           c.Piece.Color == pieceColor);

            int maxCount = GetMaxPieceCountForColor(pieceColor, pieceType);

            if (!isReplacement && currentCount >= maxCount)
            {
                string localizedType = GetPieceTypeName(pieceType);
                string localizedColor = pieceColor == "white" ? "White" : "Black";

                if (pieceType == "king")
                {
                    MessageBox.Show($"Only one {localizedColor} {localizedType} is allowed!",
                                    "Maximum Pieces Reached",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                else if (pieceType == "pawn")
                {
                    MessageBox.Show($"Maximum 8 {localizedColor} pawns allowed!",
                                    "Maximum Pieces Reached",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                else
                {
                    int currentPawns = Board.Count(c => c.Piece != null && c.Piece.Type == "pawn" && c.Piece.Color == pieceColor);
                    int standardCount = GetStandardPieceCount(pieceType);
                    int missingPawns = Math.Max(0, 8 - currentPawns);

                    int currentQueens = Board.Count(c => c.Piece != null && c.Piece.Type == "queen" && c.Piece.Color == pieceColor);
                    int currentRooks = Board.Count(c => c.Piece != null && c.Piece.Type == "rook" && c.Piece.Color == pieceColor);
                    int currentBishops = Board.Count(c => c.Piece != null && c.Piece.Type == "bishop" && c.Piece.Color == pieceColor);
                    int currentKnights = Board.Count(c => c.Piece != null && c.Piece.Type == "knight" && c.Piece.Color == pieceColor);

                    int totalExtraPieces = Math.Max(0, currentQueens - 1) +
                                          Math.Max(0, currentRooks - 2) +
                                          Math.Max(0, currentBishops - 2) +
                                          Math.Max(0, currentKnights - 2);

                    MessageBox.Show($"Cannot place more {localizedColor} {localizedType}s!\n\n" +
                                   $"CURRENT STATE:\n" +
                                   $"• Total {localizedColor.ToLower()} pieces: {totalPiecesOfColor}/16\n" +
                                   $"• Pawns: {currentPawns}/8 (missing: {missingPawns})\n" +
                                   $"• Queens: {currentQueens}\n" +
                                   $"• Rooks: {currentRooks}\n" +
                                   $"• Bishops: {currentBishops}\n" +
                                   $"• Knights: {currentKnights}\n\n" +
                                   $"LIMITS:\n" +
                                   $"• Standard {pieceType}s: {standardCount}\n" +
                                   $"• Max {pieceType}s allowed: {maxCount} (standard + missing pawns)\n" +
                                   $"• Current {pieceType}s: {currentCount}\n\n" +
                                   $"EXPLANATION:\n" +
                                   $"Each missing pawn can become any piece through promotion.\n" +
                                   $"You have {missingPawns} missing pawns, so you can have up to\n" +
                                   $"{standardCount} + {missingPawns} = {maxCount} {pieceType}s total.\n\n" +
                                   $"Total promoted pieces so far: {totalExtraPieces}/{missingPawns}",
                                    "Maximum Pieces Reached - Type Limit",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                return;
            }

            var tempPiece = new Piece(pieceColor, pieceType);
            if (!IsValidPiecePlacement(clickedCell.Row, clickedCell.Col, tempPiece))
            {
                return;
            }

            foreach (var cell in Board)
            {
                if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                {
                    cell.Piece = _selectedPieceForPlacement.Clone();
                    break;
                }
            }

            RefreshSetupPanelDisplay();
        }

        /// <summary>
        /// Gets the total number of pieces for a specific color
        /// </summary>
        private int GetTotalPiecesForColor(string pieceColor)
        {
            return Board.Count(c => c.Piece != null && c.Piece.Color == pieceColor);
        }

        /// <summary>
        /// Gets maximum allowed count for a specific piece type and color
        /// </summary>
        private int GetMaxPieceCountForColor(string pieceColor, string pieceType)
        {
            if (pieceType == "king") return 1;
            if (pieceType == "pawn") return 8;

            int currentPawns = Board.Count(c => c.Piece != null && c.Piece.Type == "pawn" && c.Piece.Color == pieceColor);
            int missingPawns = Math.Max(0, 8 - currentPawns);
            int standardCount = GetStandardPieceCount(pieceType);

            return standardCount + missingPawns;
        }

        /// <summary>
        /// Gets the actual maximum count for a piece type (realistic chess limits)
        /// </summary>
        private int GetActualMaxPieceCount(string pieceType)
        {
            return pieceType switch
            {
                "king" => 1,      // Тільки один король
                "pawn" => 8,      // Максимум 8 пішаків
                "queen" => 9,     // 1 початковий + 8 від промоції пішаків
                "rook" => 10,     // 2 початкових + 8 від промоції пішаків  
                "bishop" => 10,   // 2 початкових + 8 від промоції пішаків
                "knight" => 10,   // 2 початкових + 8 від промоції пішаків
                _ => 1
            };
        }
        /// <summary>
        /// Gets standard piece count for a piece type
        /// </summary>
        private int GetStandardPieceCount(string pieceType)
        {
            return pieceType switch
            {
                "king" => 1,
                "queen" => 1,
                "rook" => 2,
                "bishop" => 2,
                "knight" => 2,
                "pawn" => 8,
                _ => 0
            };
        }

        /// <summary>
        /// Validates the current setup and attempts to exit setup mode
        /// </summary>
        /// <returns>True if validation passed and can exit setup mode, false otherwise</returns>
        private bool ValidateAndExitSetupMode()
        {
            Piece?[,] boardSetup = new Piece?[8, 8];
            bool hasAnyPieces = false;

            foreach (var cell in Board)
            {
                if (cell.Piece != null)
                {
                    boardSetup[cell.Row, cell.Col] = cell.Piece.Clone();
                    hasAnyPieces = true;
                }
            }

            if (!hasAnyPieces)
            {
                MessageBox.Show(
                    "The board is empty! Please place some pieces or start a new standard game instead.\n\nCannot exit setup mode with an empty board.",
                    "Empty Board - Cannot Exit Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Setup mode. Board is empty. Please place some pieces.";

                return false;
            }

            bool whiteKingPresent = false;
            bool blackKingPresent = false;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (boardSetup[row, col]?.Type == "king")
                    {
                        if (boardSetup[row, col]?.Color == "white")
                            whiteKingPresent = true;
                        else if (boardSetup[row, col]?.Color == "black")
                            blackKingPresent = true;
                    }
                }
            }

            if (!whiteKingPresent || !blackKingPresent)
            {
                MessageBox.Show("Both white and black kings must be present on the board to exit setup mode.",
                               "Invalid Position - Missing Kings",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);

                if (StatusTextBlock != null)
                {
                    if (!whiteKingPresent && !blackKingPresent)
                        StatusTextBlock.Text = "Setup mode. Missing both kings. Please place white and black kings.";
                    else if (!whiteKingPresent)
                        StatusTextBlock.Text = "Setup mode. Missing white king. Please place a white king.";
                    else
                        StatusTextBlock.Text = "Setup mode. Missing black king. Please place a black king.";
                }

                return false;
            }

            if (!IsPositionValid(boardSetup))
            {
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Setup mode. Invalid position. Please fix the position.";

                return false;
            }

            _gameLogic.LoadGame(boardSetup, "white");
            _currentPlayer = "white";
            Board = _gameLogic.GetCurrentBoard();
            ClearMoveHistory();

            GameEndType endType = _gameLogic.Board.CheckForGameEnd("white");
            if (endType != GameEndType.None)
            {
                string drawMessage = GetDrawMessage(endType);

                MessageBoxResult drawResult = MessageBox.Show(
                    $"This position is a draw!\n\n{drawMessage}\n\nWhat would you like to do?\n\n" +
                    "• Yes - Start a new standard game\n" +
                    "• No - Return to setup mode to fix the position",
                    "Draw Position Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    MessageBoxResult.No);

                if (drawResult == MessageBoxResult.Yes)
                {
                    _gameLogic.InitializeGame();
                    Board = _gameLogic.GetCurrentBoard();
                    ClearMoveHistory();
                    _currentPlayer = "white";
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates if a piece can be placed at the specified position
        /// </summary>
        private bool IsValidPiecePlacement(int row, int col, Piece piece)
        {
            if (piece.Type == "king")
            {
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        var existingPiece = Board.FirstOrDefault(cell => cell.Row == r && cell.Col == c)?.Piece;
                        if (existingPiece?.Type == "king" && existingPiece.Color != piece.Color)
                        {
                            int rowDiff = Math.Abs(row - r);
                            int colDiff = Math.Abs(col - c);

                            if (rowDiff <= 1 && colDiff <= 1)
                            {
                                MessageBox.Show(
                                    "Kings cannot be placed next to each other!\n\nPlease choose a different position.",
                                    "Invalid King Placement",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                return false;
                            }
                        }
                    }
                }
            }

            if (piece.Type == "pawn")
            {
                if ((piece.Color == "white" && row == 0) || (piece.Color == "black" && row == 7))
                {
                    string rankName = piece.Color == "white" ? "8th" : "1st";
                    MessageBox.Show(
                        $"Pawns cannot be placed on the {rankName} rank!\n\nPawns must be placed between the 2nd and 7th ranks.",
                        "Invalid Pawn Placement",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if ((piece.Color == "black" && row == 0) || (piece.Color == "white" && row == 7))
                {
                    string rankName = piece.Color == "black" ? "8th" : "1st";
                    MessageBox.Show(
                        $"Pawns cannot be placed on the {rankName} rank!\n\nPawns must be placed between the 2nd and 7th ranks.",
                        "Invalid Pawn Placement",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates if the current board position is legal
        /// </summary>
        private bool IsPositionValid(Piece?[,] boardSetup)
        {
            Board tempBoard = new Board(boardSetup);

            bool whiteInCheck = tempBoard.IsKingInCheck("white");
            bool blackInCheck = tempBoard.IsKingInCheck("black");

            if (whiteInCheck && blackInCheck)
            {
                MessageBox.Show(
                    "Invalid position: Both kings cannot be in check simultaneously!\n\nPlease adjust the position.",
                    "Invalid Position - Both Kings in Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (blackInCheck)
            {
                MessageBox.Show(
                    "Invalid position: Black king is in check, but it's White's turn to move!\n\nThis position is impossible in a real game.",
                    "Invalid Position - Wrong King in Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Updates all pieces' limit displays.
        /// </summary>
        private void RefreshSetupPanelDisplay()
        {
            if (_isSetupPositionMode)
            {
                foreach (var pieceType in _standardPieceCounts.Keys)
                {
                    UpdatePieceSetupDisplay("white", pieceType);
                    UpdatePieceSetupDisplay("black", pieceType);
                }
            }
        }

        #endregion

        #region Timer Management

        /// <summary>
        /// Initializes the chess clocks.
        /// </summary>
        private void InitializeTimers()
        {
            _whiteTimeLeft = TimeSpan.FromMinutes(30);
            _blackTimeLeft = TimeSpan.FromMinutes(30);

            _whiteTimer = CreateTimer(WhiteTimerTick);
            _blackTimer = CreateTimer(BlackTimerTick);
            _isTimersPaused = false;

            UpdateTimersDisplay();

            if (TimerControlButton != null)
                TimerControlButton.Content = "Pause Timers";
        }

        /// <summary>
        /// Creates a timer with the specified tick handler.
        /// </summary>
        /// <param name="tickHandler">Handler for timer ticks.</param>
        /// <returns>The created timer.</returns>
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
        /// Starts the chess clocks based on current player.
        /// </summary>
        private void StartTimers()
        {
            if (_isTimersPaused || !_isGameActive) return;

            _whiteTimer?.Stop();
            _blackTimer?.Stop();

            if (_currentPlayer == "white")
                _whiteTimer?.Start();
            else
                _blackTimer?.Start();

            UpdateTimerVisuals();
        }

        /// <summary>
        /// Stops all chess clocks.
        /// </summary>
        private void StopTimers()
        {
            _whiteTimer?.Stop();
            _blackTimer?.Stop();
            UpdateTimerVisuals();
        }

        /// <summary>
        /// Handles the timer control button click.
        /// </summary>
        private void TimerControl_Click(object sender, RoutedEventArgs e)
        {
            _isTimersPaused = !_isTimersPaused;

            if (_isTimersPaused)
            {
                StopTimers();
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";
            }
            else
            {
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Pause Timers";
                StartTimers();
            }

            UpdateStatusText();
            UpdateTimerVisuals();
        }

        /// <summary>
        /// Resets the chess clocks to initial values.
        /// </summary>
        private void ResetTimers()
        {
            StopTimers();
            InitializeTimers();
        }

        /// <summary>
        /// Handles the white player's clock tick.
        /// </summary>
        private void WhiteTimerTick(object? sender, EventArgs e)
        {
            UpdateTimeLeft(ref _whiteTimeLeft, WhiteTimeTextBlock, "Black won on time!");
        }

        /// <summary>
        /// Handles the black player's clock tick.
        /// </summary>
        private void BlackTimerTick(object? sender, EventArgs e)
        {
            UpdateTimeLeft(ref _blackTimeLeft, BlackTimeTextBlock, "White won on time!");
        }

        /// <summary>
        /// Updates a player's remaining time.
        /// </summary>
        /// <param name="timeLeft">Reference to time left.</param>
        /// <param name="textBlock">TextBlock to update.</param>
        /// <param name="gameOverMessage">Message to show when time runs out.</param>
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
                _isTimersPaused = true;

                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                if (StatusTextBlock != null)
                    StatusTextBlock.Text = gameOverMessage;

                IsGameActive = false;

                MessageBox.Show(gameOverMessage, "Time's Up", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Updates the timer displays.
        /// </summary>
        private void UpdateTimersDisplay()
        {
            if (WhiteTimeTextBlock != null)
                WhiteTimeTextBlock.Text = _whiteTimeLeft.ToString(@"hh\:mm\:ss");

            if (BlackTimeTextBlock != null)
                BlackTimeTextBlock.Text = _blackTimeLeft.ToString(@"hh\:mm\:ss");

            UpdateTimerVisuals();
        }

        #endregion

        #region Analysis Mode

        /// <summary>
        /// Handles the analyze position button click.
        /// </summary>
        private void AnalyzePosition_Click(object sender, RoutedEventArgs e)
        {
            _isAnalysisMode = !_isAnalysisMode;
            Button? button = sender as Button;

            if (_isAnalysisMode)
            {
                if (button != null)
                    button.Content = "Exit Analysis";

                _isTimersPaused = true;
                StopTimers();
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                string analysisText = PerformPositionAnalysis();

                ShowAnalysisResults(analysisText);

                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Analysis mode. Click on pieces to see possible moves. Check the analysis results window.";
            }
            else
            {
                if (button != null)
                    button.Content = "Analyze Position";

                if (_isGameActive)
                {
                    _isTimersPaused = false;
                    if (TimerControlButton != null)
                        TimerControlButton.Content = "Pause Timers";
                    StartTimers();
                }

                UpdateStatusText();
                ClearHighlights();
            }
        }

        /// <summary>
        /// Performs position analysis and returns the results as text.
        /// </summary>
        private string PerformPositionAnalysis()
        {
            System.Text.StringBuilder analysis = new System.Text.StringBuilder();

            analysis.AppendLine("=== POSITION ANALYSIS ===\n");

            int whiteMaterial = 0, blackMaterial = 0;
            int whitePawns = 0, blackPawns = 0;
            int whiteMinorPieces = 0, blackMinorPieces = 0;
            int whiteMajorPieces = 0, blackMajorPieces = 0;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Piece? piece = _gameLogic.Board.GetPiece(row, col);
                    if (piece != null)
                    {
                        int value = piece.GetValue();
                        if (piece.Color == "white")
                        {
                            whiteMaterial += value;
                            if (piece.Type == "pawn") whitePawns++;
                            else if (piece.Type == "knight" || piece.Type == "bishop") whiteMinorPieces++;
                            else if (piece.Type == "rook" || piece.Type == "queen") whiteMajorPieces++;
                        }
                        else
                        {
                            blackMaterial += value;
                            if (piece.Type == "pawn") blackPawns++;
                            else if (piece.Type == "knight" || piece.Type == "bishop") blackMinorPieces++;
                            else if (piece.Type == "rook" || piece.Type == "queen") blackMajorPieces++;
                        }
                    }
                }
            }

            analysis.AppendLine("MATERIAL BALANCE:");
            analysis.AppendLine($"White: {whiteMaterial} points (Pawns: {whitePawns}, Minor: {whiteMinorPieces}, Major: {whiteMajorPieces})");
            analysis.AppendLine($"Black: {blackMaterial} points (Pawns: {blackPawns}, Minor: {blackMinorPieces}, Major: {blackMajorPieces})");

            int materialDiff = whiteMaterial - blackMaterial;
            if (materialDiff > 0)
                analysis.AppendLine($"White has a material advantage of {materialDiff} points.");
            else if (materialDiff < 0)
                analysis.AppendLine($"Black has a material advantage of {Math.Abs(materialDiff)} points.");
            else
                analysis.AppendLine("Material is equal.");

            analysis.AppendLine();

            int positionScore = _gameLogic.Board.EvaluatePosition();
            analysis.AppendLine("POSITIONAL EVALUATION:");
            if (positionScore > 0)
                analysis.AppendLine($"White has a positional advantage (+{positionScore})");
            else if (positionScore < 0)
                analysis.AppendLine($"Black has a positional advantage ({positionScore})");
            else
                analysis.AppendLine("Position is approximately equal (0)");

            analysis.AppendLine();

            bool whiteInCheck = _gameLogic.Board.IsKingInCheck("white");
            bool blackInCheck = _gameLogic.Board.IsKingInCheck("black");

            analysis.AppendLine("KING SAFETY:");
            if (whiteInCheck)
                analysis.AppendLine("⚠️ White king is in CHECK!");
            if (blackInCheck)
                analysis.AppendLine("⚠️ Black king is in CHECK!");
            if (!whiteInCheck && !blackInCheck)
                analysis.AppendLine("Both kings are safe.");

            analysis.AppendLine();

            var whiteMoves = _gameLogic.Board.GetAllPossibleMovesForPlayer("white");
            var blackMoves = _gameLogic.Board.GetAllPossibleMovesForPlayer("black");

            analysis.AppendLine("MOBILITY:");
            analysis.AppendLine($"White has {whiteMoves.Count} possible moves");
            analysis.AppendLine($"Black has {blackMoves.Count} possible moves");

            if (whiteMoves.Count == 0)
            {
                if (whiteInCheck)
                    analysis.AppendLine("🏁 WHITE IS CHECKMATED!");
                else
                    analysis.AppendLine("🏁 WHITE IS STALEMATED!");
            }

            if (blackMoves.Count == 0)
            {
                if (blackInCheck)
                    analysis.AppendLine("🏁 BLACK IS CHECKMATED!");
                else
                    analysis.AppendLine("🏁 BLACK IS STALEMATED!");
            }

            analysis.AppendLine();

            GameEndType endType = _gameLogic.Board.CheckForGameEnd(_currentPlayer);
            if (endType == GameEndType.InsufficientMaterial)
            {
                analysis.AppendLine("GAME STATUS:");
                analysis.AppendLine("🏁 Insufficient material for checkmate - Draw!");
            }

            analysis.AppendLine();
            analysis.AppendLine("=== END OF ANALYSIS ===");

            return analysis.ToString();
        }

        /// <summary>
        /// Shows analysis results in a separate window.
        /// </summary>
        private void ShowAnalysisResults(string analysisText)
        {
            Window analysisWindow = new Window
            {
                Title = "Position Analysis Results",
                Width = 500,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10)
            };

            TextBlock analysisTextBlock = new TextBlock
            {
                Text = analysisText,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
                Background = Brushes.White,
                Foreground = Brushes.Black
            };

            scrollViewer.Content = analysisTextBlock;
            analysisWindow.Content = scrollViewer;

            analysisWindow.Show();
        }

        #endregion

        #region Game Management Methods

        /// <summary>
        /// Loads a game from file data.
        /// </summary>
        /// <param name="lines">Lines from the file.</param>
        private void LoadGameFromFile(string[] lines)
        {
            this.Width = 1100;

            if (_isSetupPositionMode)
            {
                _isSetupPositionMode = false;
                if (SideGrid is UIElement sidePanel)
                {
                    sidePanel.Visibility = Visibility.Collapsed;
                }
                if (ClearBoardButton != null)
                {
                    ClearBoardButton.Visibility = Visibility.Collapsed;
                }
                Button? setupButton = FindButtonByContent("Finish Setup");
                if (setupButton != null)
                {
                    setupButton.Content = "Setup Position";
                }
            }

            Piece?[,] boardState = new Piece?[8, 8];

            for (int row = 0; row < 8; row++)
            {
                if (row < lines.Length)
                {
                    ParseBoardRow(lines[row], row, boardState);
                }
            }

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
                    continue;
                }
                else if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    moveHistory.Add(lines[i]);
                }
            }

            _gameLogic.LoadGame(boardState, currentPlayer);
            Board = _gameLogic.GetCurrentBoard();
            _currentPlayer = currentPlayer;
            UpdateStatusText();
            UpdateMoveHistoryFromLoaded(moveHistory);

            MessageBoxResult result = MessageBox.Show(
                "Would you like to play against the computer with this position?",
                "Game Mode Selection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ShowComputerModeDialog();
            }
            else
            {
                _isComputerMode = false;
                _gameLogic.SetComputerMode(false);
                IsComputerMode = false;

                if (DifficultyComboBox != null)
                    DifficultyComboBox.Visibility = Visibility.Collapsed;
            }

            MessageBox.Show("Position loaded.", "Load");
        }

        /// <summary>
        /// Shows a dialog for computer mode settings only (color and difficulty)
        /// </summary>
        private void ShowComputerModeDialog()
        {
            Window computerModeWindow = new Window
            {
                Title = "Computer Opponent Settings",
                Width = 350,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            StackPanel mainPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            TextBlock titleLabel = new TextBlock
            {
                Text = "Computer Opponent Settings",
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            mainPanel.Children.Add(titleLabel);

            GroupBox colorGroupBox = new GroupBox
            {
                Header = "Play As",
                Padding = new Thickness(10)
            };

            StackPanel colorPanel = new StackPanel();

            RadioButton whiteRadio = new RadioButton
            {
                Content = "White",
                Margin = new Thickness(0, 0, 0, 10),
                IsChecked = !_playerPlaysBlack
            };

            RadioButton blackRadio = new RadioButton
            {
                Content = "Black",
                Margin = new Thickness(0, 0, 0, 10),
                IsChecked = _playerPlaysBlack
            };

            colorPanel.Children.Add(whiteRadio);
            colorPanel.Children.Add(blackRadio);
            colorGroupBox.Content = colorPanel;
            mainPanel.Children.Add(colorGroupBox);

            GroupBox difficultyGroupBox = new GroupBox
            {
                Header = "Computer Difficulty",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 0)
            };

            ComboBox difficultyCombo = new ComboBox
            {
                Margin = new Thickness(0, 5, 0, 5)
            };

            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Random", Tag = "Random" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Easy", Tag = "Easy" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Medium", Tag = "Medium" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Hard", Tag = "Hard" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Expert", Tag = "Expert" });

            for (int i = 0; i < difficultyCombo.Items.Count; i++)
            {
                if (difficultyCombo.Items[i] is ComboBoxItem item)
                {
                    string difficulty = item.Tag?.ToString() ?? "";
                    if (difficulty == _gameLogic.CurrentDifficulty.ToString())
                    {
                        difficultyCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (difficultyCombo.SelectedIndex == -1)
                difficultyCombo.SelectedIndex = 2;

            difficultyGroupBox.Content = difficultyCombo;
            mainPanel.Children.Add(difficultyGroupBox);

            TextBlock randomExplanation = new TextBlock
            {
                Text = "Random mode makes the computer choose moves randomly rather than strategically.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 5, 10, 15),
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            };
            mainPanel.Children.Add(randomExplanation);

            Button okButton = new Button
            {
                Content = "OK",
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 80
            };

            okButton.Click += (s, e) =>
            {
                _isComputerMode = true;
                _gameLogic.SetComputerMode(true);
                IsComputerMode = true;

                _playerPlaysBlack = blackRadio.IsChecked == true;
                _playerColor = _playerPlaysBlack ? ChessColor.Black : ChessColor.White;
                _gameLogic.PlayerPlaysBlack = _playerPlaysBlack;

                if (PlayAsWhiteRadioButton != null)
                    PlayAsWhiteRadioButton.IsChecked = !_playerPlaysBlack;
                if (PlayAsBlackRadioButton != null)
                    PlayAsBlackRadioButton.IsChecked = _playerPlaysBlack;

                if (difficultyCombo.SelectedItem is ComboBoxItem selectedItem)
                {
                    string difficultyStr = selectedItem.Tag?.ToString() ?? "Medium";
                    _gameLogic.CurrentDifficulty = difficultyStr switch
                    {
                        "Random" => GameLogic.ComputerDifficulty.Random,
                        "Easy" => GameLogic.ComputerDifficulty.Easy,
                        "Medium" => GameLogic.ComputerDifficulty.Medium,
                        "Hard" => GameLogic.ComputerDifficulty.Hard,
                        "Expert" => GameLogic.ComputerDifficulty.Expert,
                        _ => GameLogic.ComputerDifficulty.Medium
                    };

                    if (DifficultyComboBox != null)
                    {
                        for (int i = 0; i < DifficultyComboBox.Items.Count; i++)
                        {
                            if (DifficultyComboBox.Items[i] is ComboBoxItem item &&
                                item.Tag?.ToString() == difficultyStr)
                            {
                                DifficultyComboBox.SelectionChanged -= DifficultyComboBox_SelectionChanged;

                                DifficultyComboBox.SelectedIndex = i;

                                DifficultyComboBox.SelectionChanged += DifficultyComboBox_SelectionChanged;
                                break;
                            }
                        }
                    }
                }

                if (DifficultyComboBox != null)
                    DifficultyComboBox.Visibility = Visibility.Visible;

                bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                    (!_playerPlaysBlack && _currentPlayer == "black");

                if (isComputerTurn)
                {
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(500);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _gameLogic.MakeComputerMove();
                        });
                    });
                }

                UpdateStatusText();
                ForceRefreshBoardState();

                computerModeWindow.Close();
            };

            mainPanel.Children.Add(okButton);
            computerModeWindow.Content = mainPanel;
            computerModeWindow.ShowDialog();
        }
        /// <summary>
        /// Parses a board row from file data.
        /// </summary>
        /// <param name="line">Line from the file.</param>
        /// <param name="row">Row index.</param>
        /// <param name="boardState">Board state to update.</param>
        private void ParseBoardRow(string line, int row, Piece?[,] boardState)
        {
            if (line.Length == 8)
            {
                for (int col = 0; col < 8; col++)
                {
                    boardState[row, col] = DecodePieceOldFormat(line[col].ToString());
                }
            }
            else if (line.Length == 16)
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
        /// Decodes a piece from the old file format.
        /// </summary>
        /// <param name="code">Piece code.</param>
        /// <returns>The decoded piece, or null for empty.</returns>
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
        /// Decodes a piece from the new file format.
        /// </summary>
        /// <param name="code">Piece code.</param>
        /// <returns>The decoded piece, or null for empty.</returns>
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

        /// <summary>
        /// Updates the move history.
        /// </summary>
        /// <param name="move">The move to add to history.</param>
        private void UpdateMoveHistory(string move)
        {
            if (string.IsNullOrEmpty(move))
                return;

            MoveHistory.Add(move);

            if (MoveHistoryListBox != null)
            {
                Dispatcher.Invoke(() =>
                {
                    MoveHistoryListBox.Items.Add(move);
                    MoveHistoryListBox.ScrollIntoView(move);
                });
            }
        }

        /// <summary>
        /// Clears the move history.
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
        /// Updates move history from loaded data.
        /// </summary>
        /// <param name="loadedHistory">The loaded history to display.</param>
        private void UpdateMoveHistoryFromLoaded(ObservableCollection<string> loadedHistory)
        {
            ClearMoveHistory();

            foreach (var move in loadedHistory)
            {
                UpdateMoveHistory(move);
            }
        }

        /// <summary>
        /// Tries to make a move.
        /// </summary>
        /// <param name="startCell">Starting cell.</param>
        /// <param name="endCell">Ending cell.</param>
        private void TryMove(BoardCell startCell, BoardCell endCell)
        {
            if (!_gameLogic.IsPlayerTurn())
            {
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "It's not your turn!";
                return;
            }

            Piece? piece = startCell.Piece;
            if (piece == null || piece.Color != _currentPlayer)
            {
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = $"It's {(_currentPlayer == "white" ? "White" : "Black")}'s turn!";
                return;
            }

            if (_gameLogic.TryMovePiece(startCell.Row, startCol: startCell.Col, endRow: endCell.Row, endCol: endCell.Col))
            {
                ClearHighlights();

                ForceRefreshBoardState();
            }
        }

        /// <summary>
        /// Force refreshes the board state and all related UI elements.
        /// </summary>
        private void ForceRefreshBoardState()
        {
            _currentPlayer = _gameLogic.CurrentPlayer;

            ClearHighlights();
            _draggedCell = null;

            Board = _gameLogic.GetCurrentBoard();

            UpdateBoardUI();
            UpdateStatusAndTimers();
        }

        /// <summary>
        /// Updates both status text and timer states.
        /// </summary>
        private void UpdateStatusAndTimers()
        {
            UpdateStatusText();

            StopTimers();

            if (!_isGameActive || _isTimersPaused) return;

            StartTimers();
        }

        /// <summary>
        /// Handles the start new game button click.
        /// </summary>
        private void StartNewGame_Click(object sender, RoutedEventArgs e)
        {
            if (ShowConfirmation("Are you sure you want to start a new game? All unsaved progress will be lost."))
            {
                this.Width = 1100;

                ClearMoveHistory();

                _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;
                _currentPlayer = "white";

                _gameLogic.InitializeGame();
                InitializeBoardUI();
                UpdateBoardUI();

                if (_isSetupPositionMode)
                {
                    _isSetupPositionMode = false;

                    if (SideGrid is UIElement sidePanel)
                    {
                        sidePanel.Visibility = Visibility.Collapsed;
                    }

                    if (ClearBoardButton != null)
                    {
                        ClearBoardButton.Visibility = Visibility.Collapsed;
                    }

                    Button? setupButton = FindButtonByContent("Finish Setup");
                    if (setupButton != null)
                    {
                        setupButton.Content = "Setup Position";
                    }
                }

                if (_isAnalysisMode)
                {
                    _isAnalysisMode = false;
                    Button? analyzeButton = FindButtonByContent("Exit Analysis");
                    if (analyzeButton != null)
                        analyzeButton.Content = "Analyze Position";
                }

                _isTimersPaused = false;
                ResetTimers();
                StartTimers();

                _isGameActive = true;
                ForceRefreshBoardState();
            }
        }
        /// <summary>
        /// Exits setup mode and shows game mode selection
        /// </summary>
        /// <param name="sender">The button that triggered the exit</param>
        private void ExitSetupMode(object sender)
        {
            _isSetupPositionMode = false;

            this.Width = 1100;

            if (SideGrid is UIElement sidePanel)
            {
                sidePanel.Visibility = Visibility.Collapsed;

                if (ClearBoardButton != null)
                    ClearBoardButton.Visibility = Visibility.Collapsed;
            }

            _selectedPieceForPlacement = null;
            _selectedPieceBorder = null;

            if (sender is Button setupButton)
                setupButton.Content = "Setup Position";

            UpdateBoardUI();

            MessageBoxResult gameResult = MessageBox.Show(
                "Position setup complete. Would you like to play against the computer with this position?",
                "Game Mode Selection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (gameResult == MessageBoxResult.Yes)
            {
                ShowComputerModeDialog();
            }
            else
            {
                _isComputerMode = false;
                _gameLogic.SetComputerMode(false);
                IsComputerMode = false;

                if (DifficultyComboBox != null)
                    DifficultyComboBox.Visibility = Visibility.Collapsed;
            }

            _isGameActive = true;
            UpdateStatusText();
        }
        /// <summary>
        /// Handles the clear board button click.
        /// </summary>
        private void ClearBoard_Click(object sender, RoutedEventArgs e)
        {
            if (_isSetupPositionMode)
            {
                if (ShowConfirmation("Are you sure you want to clear the board? This will remove all pieces."))
                {
                    _gameLogic.ClearBoard();
                    UpdateBoardUI();
                    ClearMoveHistory();
                    _currentPlayer = "white";

                    RefreshSetupPanelDisplay();

                    _isGameActive = false;
                    ClearHighlights();

                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "Setup mode. Board cleared. Select pieces to place on the board.";
                }
            }
            else
            {
                if (ShowConfirmation("Are you sure you want to clear the board and move history?"))
                {
                    _gameLogic.ClearBoard();
                    UpdateBoardUI();
                    ClearMoveHistory();
                    _currentPlayer = "white";

                    _isGameActive = false;
                    StopTimers();
                    _isTimersPaused = true;
                    if (TimerControlButton != null)
                        TimerControlButton.Content = "Resume Timers";

                    UpdateStatusText();
                    ClearHighlights();

                    MessageBoxResult result = MessageBox.Show(
                        "Board cleared. Would you like to start a new game?",
                        "Start New Game?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _gameLogic.InitializeGame();
                        UpdateBoardUI();
                        ResetTimers();
                        _isTimersPaused = false;
                        if (TimerControlButton != null)
                            TimerControlButton.Content = "Pause Timers";
                        StartTimers();
                        _isGameActive = true;
                        UpdateStatusText();
                    }
                }
            }
        }

        /// <summary>
        /// Handles the two players mode button click.
        /// </summary>
        private void SetTwoPlayersMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isComputerMode)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Switch to two player mode? This will start a new game.",
                    "Switch to Two Players Mode",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _isComputerMode = false;
                    _gameLogic.SetComputerMode(false);

                    if (DifficultyComboBox != null)
                        DifficultyComboBox.Visibility = Visibility.Collapsed;

                    IsComputerMode = false;

                    ClearMoveHistory();
                    _currentPlayer = "white";
                    _gameLogic.InitializeGame();
                    InitializeBoardUI();
                    UpdateBoardUI();

                    _isTimersPaused = false;
                    ResetTimers();

                    UpdateTimerModeVisibility();

                    StartTimers();

                    if (TimerControlButton != null)
                        TimerControlButton.Content = "Pause Timers";

                    UpdateStatusText();
                }
            }
        }

        /// <summary>
        /// Handles the computer mode button click.
        /// </summary>
        private void SetComputerMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isSetupPositionMode)
            {
                MessageBox.Show("Please finish position setup before switching to computer mode.",
                              "Setup Mode Active",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Switch to computer mode? This will start a new game.",
                "Switch to Computer Mode",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
                return;

            _isComputerMode = true;
            _gameLogic.SetComputerMode(true);

            if (DifficultyComboBox != null)
                DifficultyComboBox.Visibility = Visibility.Visible;

            IsComputerMode = true;

            _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

            ClearMoveHistory();

            _currentPlayer = "white";
            _gameLogic.InitializeGame();
            InitializeBoardUI();

            if (_isAnalysisMode)
            {
                _isAnalysisMode = false;
                Button? analyzeButton = FindButtonByContent("Exit Analysis");
                if (analyzeButton != null)
                    analyzeButton.Content = "Analyze Position";
            }

            _isTimersPaused = false;
            ResetTimers();

            UpdateTimerModeVisibility();

            StartTimers();

            UpdateStatusText();

            bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                (!_playerPlaysBlack && _currentPlayer == "black");

            if (isComputerTurn)
            {
                _gameLogic.MakeComputerMove();
            }

            ForceRefreshBoardState();
        }

        /// <summary>
        /// Handles player color radio button checks.
        /// </summary>
        private void PlayerColorRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || !_isComputerMode) return;

            ChessColor newColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;

            if (_playerColor != newColor)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Changing color will start a new game. Continue?",
                    "Color Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _playerColor = newColor;
                    _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

                    ClearMoveHistory();
                    _currentPlayer = "white";
                    _gameLogic.InitializeGame();
                    InitializeBoardUI();

                    _isTimersPaused = false;
                    ResetTimers();

                    UpdateTimerModeVisibility();

                    StartTimers();

                    bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                        (!_playerPlaysBlack && _currentPlayer == "black");

                    if (isComputerTurn)
                    {
                        _gameLogic.MakeComputerMove();
                    }

                    ForceRefreshBoardState();
                }
                else
                {
                    if (PlayAsWhiteRadioButton != null && PlayAsBlackRadioButton != null)
                    {
                        PlayAsWhiteRadioButton.IsChecked = _playerColor == ChessColor.White;
                        PlayAsBlackRadioButton.IsChecked = _playerColor == ChessColor.Black;
                    }
                }
            }
        }
        #endregion

        #region Board UI Methods

        /// <summary>
        /// Initializes the board UI.
        /// </summary>
        private void InitializeBoardUI()
        {
            Board = _gameLogic.GetCurrentBoard();
        }

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        /// <param name="message">Message to show.</param>
        /// <returns>True if confirmed, false if cancelled.</returns>
        private bool ShowConfirmation(string message)
        {
            return MessageBox.Show(
                message,
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Finds a button by its content.
        /// </summary>
        private Button? FindButtonByContent(string content)
        {
            Button? result = null;
            FindButtonByContentRecursive(MainGrid, content, ref result);
            return result;
        }

        /// <summary>
        /// Recursively searches for button with specified content.
        /// </summary>
        private void FindButtonByContentRecursive(DependencyObject parent, string content, ref Button? result)
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is Button button && button.Content as string == content)
                {
                    result = button;
                    return;
                }

                FindButtonByContentRecursive(child, content, ref result);
            }
        }

        #endregion

    }
    /// <summary>
    /// Enum for player colors.
    /// </summary>
    public enum ChessColor
    {
        /// <summary>
        /// White chess pieces.
        /// </summary>
        White,

        /// <summary>
        /// Black chess pieces.
        /// </summary>
        Black
    }
}