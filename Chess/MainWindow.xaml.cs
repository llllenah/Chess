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

        // Maximum piece counts (including pawn promotions)
        private readonly Dictionary<string, int> _maxPieceCounts = new Dictionary<string, int>
        {
            { "king", 1 },
            { "queen", 9 },  // 1 original + 8 from pawn promotions
            { "rook", 10 },  // 2 original + 8 from pawn promotions
            { "bishop", 10 }, // 2 original + 8 from pawn promotions
            { "knight", 10 }, // 2 original + 8 from pawn promotions
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

            // Set initial window size
            this.Width = 1100;

            // Initialize non-nullable fields
            _whitePanel = new StackPanel();
            _blackPanel = new StackPanel();
            SideGrid = new Grid();

            // Initialize game logic
            _gameLogic = new GameLogic();
            _gameLogic.BoardUpdated += OnGameLogicBoardUpdated;
            _gameLogic.MoveMade += OnGameLogicMoveMade;
            _gameLogic.GameEnded += OnGameLogicGameEnded;
            _gameLogic.PawnPromotion += OnPawnPromotion;

            // Set up pawn promotion dialog
            _gameLogic.SetPromotionDialogCallback(ShowPromotionDialog);

            // First create side panels, then initialize them
            CreateSidePanels();
            InitializeSetupPanels();

            // Initialize game state
            Board = _gameLogic.GetCurrentBoard();
            InitializeTimers();
            StartTimers();
            UpdateStatusText();

            // Set initial difficulty
            if (DifficultyComboBox != null)
            {
                // Temporarily remove the event handler to avoid triggering dialog
                DifficultyComboBox.SelectionChanged -= DifficultyComboBox_SelectionChanged;

                // Set the initial selection to Medium (index 2)
                DifficultyComboBox.SelectedIndex = 2; // Medium difficulty

                // Reattach the event handler
                DifficultyComboBox.SelectionChanged += DifficultyComboBox_SelectionChanged;

                // Initially hide difficulty combo box since we start in Two Players mode
                DifficultyComboBox.Visibility = Visibility.Collapsed;
            }

            // Make sure we're in Two Players mode at startup
            _isComputerMode = false;
            _gameLogic.SetComputerMode(false);
            IsComputerMode = false;

            // Set the game status
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
                // Get difficulty from tag or content
                string difficultyText = selectedItem.Tag?.ToString() ?? selectedItem.Content?.ToString() ?? "Medium";

                // Single dialog for both changing difficulty and starting a new game
                MessageBoxResult result = MessageBox.Show(
                    $"Change difficulty to {difficultyText}? This will start a new game.",
                    "Change Difficulty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Apply the difficulty change
                    _gameLogic.CurrentDifficulty = difficultyText switch
                    {
                        "Random" => GameLogic.ComputerDifficulty.Random,
                        "Easy" => GameLogic.ComputerDifficulty.Easy,
                        "Medium" => GameLogic.ComputerDifficulty.Medium,
                        "Hard" => GameLogic.ComputerDifficulty.Hard,
                        "Expert" => GameLogic.ComputerDifficulty.Expert,
                        _ => GameLogic.ComputerDifficulty.Medium
                    };

                    // Start a new game immediately - no second confirmation
                    ClearMoveHistory();

                    _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                    _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;
                    _currentPlayer = "white";
                    _gameLogic.InitializeGame();
                    InitializeBoardUI();
                    UpdateBoardUI();

                    // Reset timers
                    ResetTimers();
                    _isTimersPaused = false;

                    if (TimerControlButton != null)
                        TimerControlButton.Content = "Pause Timers";

                    // Update game state
                    _isGameActive = true;

                    // Check if it's computer's turn
                    bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                        (!_playerPlaysBlack && _currentPlayer == "black");

                    // If it's computer's turn, make computer move
                    if (isComputerTurn)
                    {
                        Task.Run(() =>
                        {
                            // Small delay for better user experience
                            System.Threading.Thread.Sleep(500);

                            // Make the computer move
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _gameLogic.MakeComputerMove();
                            });
                        });
                    }

                    // Update everything
                    ForceRefreshBoardState();
                }
                else
                {
                    // User canceled the difficulty change, revert the ComboBox selection
                    // Find and select the previous difficulty setting
                    GameLogic.ComputerDifficulty currentDifficulty = _gameLogic.CurrentDifficulty;

                    for (int i = 0; i < DifficultyComboBox.Items.Count; i++)
                    {
                        if (DifficultyComboBox.Items[i] is ComboBoxItem item)
                        {
                            string diffText = item.Tag?.ToString() ?? "";
                            if (diffText == currentDifficulty.ToString())
                            {
                                // Temporarily remove the event handler
                                DifficultyComboBox.SelectionChanged -= DifficultyComboBox_SelectionChanged;

                                // Set the selection back
                                DifficultyComboBox.SelectedIndex = i;

                                // Reattach the event handler
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
            // Create side grid for placing pieces with auto width
            Grid sideGrid = new Grid();

            // Use auto-width with min/max constraints for better responsiveness
            sideGrid.Width = double.NaN; // Auto width
            sideGrid.MinWidth = 200;     // Minimum width
            sideGrid.MaxWidth = 350;     // Maximum width to avoid taking too much space

            // Use columns for white/black pieces side by side
            sideGrid.ColumnDefinitions.Add(new ColumnDefinition());
            sideGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Single row to place white and black pieces side by side
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Add header with larger font
            TextBlock setupHeader = new TextBlock
            {
                Text = "Piece Setup",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            // Create piece panels with ScrollViewer to handle overflow
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

            // Create white pieces panel with auto width
            _whitePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Create black pieces panel with auto width
            _blackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Add title for white pieces with larger text
            TextBlock whiteTitle = new TextBlock
            {
                Text = "White Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _whitePanel.Children.Add(whiteTitle);

            // Add title for black pieces with larger text
            TextBlock blackTitle = new TextBlock
            {
                Text = "Black Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _blackPanel.Children.Add(blackTitle);

            // Add panels to scrollviewers
            whiteScroll.Content = _whitePanel;
            blackScroll.Content = _blackPanel;

            // Add scrollviewers to the grid
            sideGrid.Children.Add(whiteScroll);
            sideGrid.Children.Add(blackScroll);
            Grid.SetColumn(whiteScroll, 0);
            Grid.SetColumn(blackScroll, 1);

            // Add the side grid to the main grid inside a border for better visibility
            Border setupBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Colors.WhiteSmoke),
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left // Important! Allows to auto-adapt size
            };

            StackPanel setupContainer = new StackPanel();
            setupContainer.Children.Add(setupHeader);
            setupContainer.Children.Add(sideGrid);
            setupBorder.Child = setupContainer;

            // Add to main grid with column span
            MainGrid.Children.Add(setupBorder);
            Grid.SetColumn(setupBorder, 3);
            Grid.SetRow(setupBorder, 1);

            // Initially hide the panel
            setupBorder.Visibility = Visibility.Collapsed;

            // Store reference to side grid
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
            // Clear existing panels
            _whitePanel.Children.Clear();
            _blackPanel.Children.Clear();
            _whitePieces.Clear();
            _blackPieces.Clear();

            // Add title for white pieces
            TextBlock whiteTitle = new TextBlock
            {
                Text = "White Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _whitePanel.Children.Add(whiteTitle);

            // Add title for black pieces
            TextBlock blackTitle = new TextBlock
            {
                Text = "Black Pieces",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10),
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

            // Reset selection
            _selectedPieceForPlacement = null;
            _selectedPieceBorder = null;

            // Update piece counts display
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
            // Create a piece
            Piece piece = new Piece(pieceColor, pieceType);

            // Create border for the piece
            Border pieceBorder = new Border
            {
                Width = 80, // Increased from 60 to 80 for more space
                Height = 70, // Increased height to accommodate text better
                Margin = new Thickness(2), // Reduced margin to fit better
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Tag = $"{pieceColor},{pieceType}"
            };

            // Add shadow effect
            pieceBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                BlurRadius = 5,
                Opacity = 0.3
            };

            // Create container for content
            Grid container = new Grid // Changed from StackPanel to Grid for better layout control
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Define rows for the grid
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) }); // For piece icon
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // For piece name
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // For counter

            // Add piece icon
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

            // Add piece name with smaller font
            TextBlock pieceName = new TextBlock
            {
                Text = GetPieceTypeName(pieceType),
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            Grid.SetRow(pieceName, 1);

            // Add counter with more visible style
            TextBlock countText = new TextBlock
            {
                Text = "0/1",
                FontSize = 10,
                FontWeight = FontWeights.Bold, // Make counter bold
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.DarkBlue // More visible color
            };
            Grid.SetRow(countText, 2);

            container.Children.Add(pieceIcon);
            container.Children.Add(pieceName);
            container.Children.Add(countText);

            pieceBorder.Child = container;
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

                // Update setup panel if in setup mode
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
                // Update move history
                UpdateMoveHistory(e.MoveNotation);

                // Force refresh the board state
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
                // Immediately stop game and timers
                IsGameActive = false;
                StopTimers();
                _isTimersPaused = true;

                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                string resultMessage;
                string title = "Game Over";
                MessageBoxImage icon = MessageBoxImage.Information;

                // Set the result message based on the end type
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

                // Show game over message and ask about new game
                MessageBoxResult result = MessageBox.Show(
                    resultMessage + "\n\nWould you like to start a new game?",
                    title,
                    MessageBoxButton.YesNo,
                    icon
                );

                if (result == MessageBoxResult.Yes)
                {
                    // Save the message box result to use after confirmation
                    bool startNewGame = true;

                    // Use Application.Current.Dispatcher to avoid nested message boxes issue
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (startNewGame)
                        {
                            // Clear history BEFORE starting new game
                            ClearMoveHistory();

                            // Complete reset game state
                            _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;
                            _currentPlayer = "white";
                            _gameLogic.InitializeGame();
                            InitializeBoardUI();
                            UpdateBoardUI();

                            // Reset timers
                            ResetTimers();
                            _isTimersPaused = false;

                            if (TimerControlButton != null)
                                TimerControlButton.Content = "Pause Timers";

                            // Update game state
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

            // Default to queen if dialog was cancelled
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
            // Check if we're in computer mode and prevent setup
            if (_isComputerMode && !_isSetupPositionMode)
            {
                MessageBox.Show("Position setup is only available in two-player mode. Please switch to two-player mode first.",
                                "Setup Mode Not Available",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            _isSetupPositionMode = !_isSetupPositionMode;

            if (_isSetupPositionMode)
            {
                // Resize window to accommodate setup panel
                this.Width = 1460;

                // Stop timers when entering setup mode
                StopTimers();
                _isTimersPaused = true;

                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                // Enter setup mode
                InitializeSetupPanels();

                // Show side panel and clear board button
                if (SideGrid is UIElement sidePanel)
                {
                    sidePanel.Visibility = Visibility.Visible;

                    // Show Clear Board button only in setup mode
                    if (ClearBoardButton != null)
                        ClearBoardButton.Visibility = Visibility.Visible;

                    // Update status
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "Setup mode. Select a piece and click on the board to place it.";
                }

                // Change button text
                if (sender is Button setupButton)
                    setupButton.Content = "Finish Setup";

                // Initialize piece count display
                RefreshSetupPanelDisplay();
            }
            else
            {
                // Resize window back to normal size
                this.Width = 1100;

                // Exit setup mode
                if (SideGrid is UIElement sidePanel)
                {
                    sidePanel.Visibility = Visibility.Collapsed;

                    // Hide Clear Board button when not in setup mode
                    if (ClearBoardButton != null)
                        ClearBoardButton.Visibility = Visibility.Collapsed;
                }

                _selectedPieceForPlacement = null;
                _selectedPieceBorder = null;

                // Save the current setup to the game logic
                SaveCurrentSetupToGameLogic();

                // Change button text
                if (sender is Button setupButton)
                    setupButton.Content = "Setup Position";

                // Update game state
                UpdateStatusText();

                // Update board
                UpdateBoardUI();
            }
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
                // Setup mode - place or remove pieces
                if (_selectedPieceForPlacement != null)
                {
                    PlacePieceInSetupMode(clickedCell);
                }
                else
                {
                    // No piece selected - remove piece at this position
                    foreach (var cell in Board)
                    {
                        if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                        {
                            cell.Piece = null;
                            break;
                        }
                    }

                    // Update piece counts display
                    RefreshSetupPanelDisplay();
                }

                e.Handled = true;
            }
            else if (_isAnalysisMode)
            {
                // In analysis mode just show possible moves
                ShowValidMovesForPiece(clickedCell.Row, clickedCell.Col);
                e.Handled = true;
            }
            else if (_isGameActive)
            {
                // Make sure current player is synced with game logic
                _currentPlayer = _gameLogic.CurrentPlayer;

                // Check if it's the player's turn
                if (_isComputerMode && !_gameLogic.IsPlayerTurn())
                {
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = "It's computer's turn. Please wait.";
                    return;
                }

                // Check that it's the current player's piece
                var piece = clickedCell.Piece;
                if (piece == null || piece.Color != _currentPlayer)
                {
                    if (StatusTextBlock != null)
                        StatusTextBlock.Text = $"It's {(_currentPlayer == "white" ? "White" : "Black")}'s turn. Please select a valid piece.";
                    return;
                }

                // Show valid moves and start dragging
                ShowValidMovesForPiece(clickedCell.Row, clickedCell.Col);

                // Don't start dragging if there are no available moves
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

            // Try to make the move
            TryMove(_draggedCell, dropCell);
            _draggedCell = null;

            // Clear highlights
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

                    // Select this piece for placement
                    _selectedPieceForPlacement = new Piece(pieceColor, pieceType);

                    // Highlight this piece border
                    HighlightSelectedPieceBorder(pieceBorder);

                    // Update status
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

                // Update timer visuals
                UpdateTimerVisuals();
            });
        }

        /// <summary>
        /// Updates the board coordinate labels when flipping the board
        /// </summary>
        private void UpdateBoardLabels()
        {
            if (ChessBoardGrid == null) return;

            // Find labels
            StackPanel? topLabels = FindVisualChild<StackPanel>(ChessBoardGrid, 0, 1);
            StackPanel? bottomLabels = FindVisualChild<StackPanel>(ChessBoardGrid, 2, 1);
            StackPanel? leftLabels = FindVisualChild<StackPanel>(ChessBoardGrid, 1, 0);
            StackPanel? rightLabels = FindVisualChild<StackPanel>(ChessBoardGrid, 1, 2);

            if (topLabels == null || bottomLabels == null || leftLabels == null || rightLabels == null)
                return;

            // File labels (a-h)
            char[] files = IsBoardFlipped ?
                new[] { 'h', 'g', 'f', 'e', 'd', 'c', 'b', 'a' } :
                new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

            // Rank labels (1-8)
            char[] ranks = IsBoardFlipped ?
                new[] { '1', '2', '3', '4', '5', '6', '7', '8' } :
                new[] { '8', '7', '6', '5', '4', '3', '2', '1' };

            // Update file labels
            for (int i = 0; i < 8; i++)
            {
                if (topLabels.Children[i] is TextBlock topBlock)
                    topBlock.Text = files[i].ToString();

                if (bottomLabels.Children[i] is TextBlock bottomBlock)
                    bottomBlock.Text = files[i].ToString();
            }

            // Update rank labels
            for (int i = 0; i < 8; i++)
            {
                if (leftLabels.Children[i] is TextBlock leftBlock)
                    leftBlock.Text = ranks[i].ToString();

                if (rightLabels.Children[i] is TextBlock rightBlock)
                    rightBlock.Text = ranks[i].ToString();
            }
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

                // Update active indicator
                if (WhiteActiveIndicator != null)
                    WhiteActiveIndicator.Visibility = whiteActive ? Visibility.Visible : Visibility.Collapsed;

                if (BlackActiveIndicator != null)
                    BlackActiveIndicator.Visibility = blackActive ? Visibility.Visible : Visibility.Collapsed;

                // Update border colors
                if (WhiteTimerBorder != null && BlackTimerBorder != null)
                {
                    var defaultBorderBrush = new SolidColorBrush(Color.FromRgb(189, 189, 189)); // #BDBDBD
                    var activeBorderBrush = new SolidColorBrush(Colors.Green);

                    WhiteTimerBorder.BorderBrush = whiteActive ? activeBorderBrush : defaultBorderBrush;
                    BlackTimerBorder.BorderBrush = blackActive ? activeBorderBrush : defaultBorderBrush;
                }
            }
            catch
            {
                // Ignore errors when updating visual elements
                // Can happen if elements are not yet initialized
            }
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
                    // Add indication of whose turn it is
                    bool isPlayerTurn = _gameLogic.IsPlayerTurn();
                    string turnInfo = isPlayerTurn ? "Your turn" : "Computer's turn";
                    StatusTextBlock.Text = $"Computer mode. {playerText} to move. {turnInfo}.";
                }
                else
                {
                    StatusTextBlock.Text = $"Two players mode. {playerText} to move.";
                }

                // Add timer state information
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
            // First check if it's the player's turn
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
                // Count current pieces on board
                int currentCount = Board.Count(c => c.Piece != null &&
                                               c.Piece.Type == pieceType &&
                                               c.Piece.Color == pieceColor);

                // Get standard and maximum counts
                _standardPieceCounts.TryGetValue(pieceType, out int standardCount);
                _maxPieceCounts.TryGetValue(pieceType, out int maxCount);

                // Update tooltip
                string standardText = $"Standard count: {currentCount}/{standardCount}";
                string maxText = pieceType != "pawn" && pieceType != "king"
                              ? $"\nMaximum count (with promotions): {maxCount}"
                              : "";

                pieceBorder.ToolTip = $"{GetPieceTypeName(pieceType)}\n{standardText}{maxText}";

                // Visually indicate if standard count is reached
                if (currentCount >= standardCount)
                {
                    // Over standard - use orange highlight for warning
                    pieceBorder.BorderBrush = new SolidColorBrush(Colors.Orange);
                    pieceBorder.BorderThickness = new Thickness(1);

                    // Reduce opacity if maximum count is reached
                    pieceBorder.Opacity = currentCount >= maxCount ? 0.5 : 1.0;
                }
                else
                {
                    // Under standard - normal appearance
                    if (pieceBorder != _selectedPieceBorder)
                    {
                        pieceBorder.BorderBrush = Brushes.Transparent;
                        pieceBorder.BorderThickness = new Thickness(1);
                    }
                    pieceBorder.Opacity = 1.0;
                }

                // Update counter on piece icon - find the counter text block in the Grid
                if (pieceBorder.Child is Grid container && container.Children.Count >= 3)
                {
                    if (container.Children[2] is TextBlock countTextBlock)
                    {
                        countTextBlock.Text = $"{currentCount}/{standardCount}";

                        // Change color based on count
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
        /// Places a piece on the board during setup mode.
        /// </summary>
        /// <param name="clickedCell">The cell to place the piece on.</param>
        private void PlacePieceInSetupMode(BoardCell clickedCell)
        {
            if (_selectedPieceForPlacement == null)
                return;

            // Check piece count limitations
            string pieceType = _selectedPieceForPlacement.Type;
            string pieceColor = _selectedPieceForPlacement.Color;

            // Count current pieces of this type on the board
            int currentCount = Board.Count(c => c.Piece != null &&
                                       c.Piece.Type == pieceType &&
                                       c.Piece.Color == pieceColor);

            // Get the maximum allowed count
            int maxCount = _maxPieceCounts.TryGetValue(pieceType, out int limit) ? limit : 1;

            if (currentCount >= maxCount)
            {
                string localizedType = GetPieceTypeName(pieceType);
                string localizedColor = pieceColor == "white" ? "White" : "Black";
                MessageBox.Show($"Maximum number of {localizedType} for {localizedColor} reached ({currentCount}/{maxCount})!",
                                "Piece Limit",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            // If there's already a piece on the cell, first remove it to avoid piece count issues
            foreach (var cell in Board)
            {
                if (cell.Row == clickedCell.Row && cell.Col == clickedCell.Col)
                {
                    // If the cell already has a piece, decrement its count before replacing
                    if (cell.Piece != null)
                    {
                        // We're removing the existing piece, so no need to check counts
                    }

                    // Place the new piece
                    cell.Piece = _selectedPieceForPlacement.Clone();
                    break;
                }
            }

            // Update piece counts display
            RefreshSetupPanelDisplay();
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

            // Make sure the button text is correct at initialization
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

            // Update UI elements
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
                // Pause timers
                StopTimers();
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";
            }
            else
            {
                // Resume timers
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Pause Timers";
                StartTimers();
            }

            // Update status and UI elements
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

                // End the game
                IsGameActive = false;

                // Show game over message
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
                // Enter analysis mode
                if (button != null)
                    button.Content = "Exit Analysis";

                // Stop timers
                _isTimersPaused = true;
                StopTimers();
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Resume Timers";

                // Update status
                StatusTextBlock.Text = "Analysis mode. Click on pieces to see possible moves.";
            }
            else
            {
                // Exit analysis mode
                if (button != null)
                    button.Content = "Analyze Position";

                // Update status
                UpdateStatusText();

                // Clear highlights
                ClearHighlights();
            }
        }

        #endregion

        #region Game Management Methods

        /// <summary>
        /// Initializes the game.
        /// </summary>
        private void InitializeGame()
        {
            _gameLogic.InitializeGame();
            _currentPlayer = "white";

            MoveHistory.Clear();
            IsGameActive = true;
            GameResultText = "";
            _isAnalysisMode = false;
            ResetTimers();
            StartTimers();
            UpdateStatusText();
        }

        /// <summary>
        /// Loads a game from file data.
        /// </summary>
        /// <param name="lines">Lines from the file.</param>
        private void LoadGameFromFile(string[] lines)
        {
            // Reset window size to normal when loading a game
            this.Width = 1100;

            // Exit setup mode if active
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
                // Find and reset the setup button
                Button? setupButton = FindButtonByContent("Finish Setup");
                if (setupButton != null)
                {
                    setupButton.Content = "Setup Position";
                }
            }

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

            // Ask user if they want to play against the computer or two-player
            MessageBoxResult result = MessageBox.Show(
                "Would you like to play against the computer with this position?",
                "Game Mode Selection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Show game mode dialog but only for computer mode settings
                ShowComputerModeDialog();
            }
            else
            {
                // Set to two-player mode
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
            // Create a dialog window
            Window computerModeWindow = new Window
            {
                Title = "Computer Opponent Settings",
                Width = 350,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            // Create a main layout panel
            StackPanel mainPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // Add title
            TextBlock titleLabel = new TextBlock
            {
                Text = "Computer Opponent Settings",
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            mainPanel.Children.Add(titleLabel);

            // Create color selection for computer mode
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

            // Create difficulties dropdown for computer mode
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

            // Add random mode first
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Random", Tag = "Random" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Easy", Tag = "Easy" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Medium", Tag = "Medium" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Hard", Tag = "Hard" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Expert", Tag = "Expert" });

            // Select current difficulty level
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

            // Default to Medium if not found
            if (difficultyCombo.SelectedIndex == -1)
                difficultyCombo.SelectedIndex = 2; // Medium is now index 2

            difficultyGroupBox.Content = difficultyCombo;
            mainPanel.Children.Add(difficultyGroupBox);

            // Add label for explanation of Random mode
            TextBlock randomExplanation = new TextBlock
            {
                Text = "Random mode makes the computer choose moves randomly rather than strategically.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 5, 10, 15),
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            };
            mainPanel.Children.Add(randomExplanation);

            // Add OK button
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
                // Apply computer mode
                _isComputerMode = true;
                _gameLogic.SetComputerMode(true);
                IsComputerMode = true;

                // Apply color selection
                _playerPlaysBlack = blackRadio.IsChecked == true;
                _playerColor = _playerPlaysBlack ? ChessColor.Black : ChessColor.White;
                _gameLogic.PlayerPlaysBlack = _playerPlaysBlack;

                // Update UI to reflect player's color
                if (PlayAsWhiteRadioButton != null)
                    PlayAsWhiteRadioButton.IsChecked = !_playerPlaysBlack;
                if (PlayAsBlackRadioButton != null)
                    PlayAsBlackRadioButton.IsChecked = _playerPlaysBlack;

                // Apply difficulty
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

                    // Update difficulty combobox in main UI
                    if (DifficultyComboBox != null)
                    {
                        for (int i = 0; i < DifficultyComboBox.Items.Count; i++)
                        {
                            if (DifficultyComboBox.Items[i] is ComboBoxItem item &&
                                item.Tag?.ToString() == difficultyStr)
                            {
                                // Temporarily remove the event handler
                                DifficultyComboBox.SelectionChanged -= DifficultyComboBox_SelectionChanged;

                                DifficultyComboBox.SelectedIndex = i;

                                // Reattach the event handler
                                DifficultyComboBox.SelectionChanged += DifficultyComboBox_SelectionChanged;
                                break;
                            }
                        }
                    }
                }

                // Show difficulty combobox
                if (DifficultyComboBox != null)
                    DifficultyComboBox.Visibility = Visibility.Visible;

                // Check if it's computer's turn
                bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                    (!_playerPlaysBlack && _currentPlayer == "black");

                // If it's computer's turn, make computer move
                if (isComputerTurn)
                {
                    Task.Run(() =>
                    {
                        // Small delay for better user experience
                        System.Threading.Thread.Sleep(500);

                        // Make the computer move
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _gameLogic.MakeComputerMove();
                        });
                    });
                }

                // Update all UI
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
        /// Saves the current board setup to the game logic.
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

            // Check if kings are present
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

            // Both kings must be present
            if (!whiteKingPresent || !blackKingPresent)
            {
                MessageBox.Show("Both white and black kings must be present on the board.",
                               "Invalid Position",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);

                UpdateBoardUI();
                return;
            }

            // Load the board into the game logic
            _gameLogic.LoadGame(boardSetup, "white");
            _currentPlayer = "white"; // Reset to white's turn

            // Update the UI board
            Board = _gameLogic.GetCurrentBoard();
            ClearMoveHistory();

            // Ask user if they want to play against computer with this position
            MessageBoxResult result = MessageBox.Show(
                "Would you like to play against the computer with this position?",
                "Game Mode Selection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Show computer mode dialog only for computer settings (color/difficulty)
                ShowComputerModeDialog();
            }
            else
            {
                // Set to two-player mode
                _isComputerMode = false;
                _gameLogic.SetComputerMode(false);
                IsComputerMode = false;

                if (DifficultyComboBox != null)
                    DifficultyComboBox.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Shows a dialog to select game mode (computer vs. human)
        /// </summary>
        private void ShowGameModeSelectionDialog()
        {
            // Create a dialog window
            Window modeSelectionWindow = new Window
            {
                Title = "Select Game Mode",
                Width = 400,
                Height = 650, // Increased height for more content
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            // Create a main layout panel
            StackPanel mainPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // Add title
            TextBlock titleLabel = new TextBlock
            {
                Text = "Select Game Mode",
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            mainPanel.Children.Add(titleLabel);

            // Create a group box for mode selection
            GroupBox modeGroupBox = new GroupBox
            {
                Header = "Mode",
                Padding = new Thickness(10)
            };

            StackPanel modePanel = new StackPanel();

            RadioButton humanModeRadio = new RadioButton
            {
                Content = "Two Players",
                Margin = new Thickness(0, 0, 0, 10),
                IsChecked = !_isComputerMode
            };

            RadioButton computerModeRadio = new RadioButton
            {
                Content = "Play Against Computer",
                Margin = new Thickness(0, 0, 0, 10),
                IsChecked = _isComputerMode
            };

            modePanel.Children.Add(humanModeRadio);
            modePanel.Children.Add(computerModeRadio);
            modeGroupBox.Content = modePanel;
            mainPanel.Children.Add(modeGroupBox);

            // Create color selection for computer mode
            GroupBox colorGroupBox = new GroupBox
            {
                Header = "Play As",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 0)
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

            // Create difficulties dropdown for computer mode
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

            // Add random mode first
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Random", Tag = "Random" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Easy", Tag = "Easy" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Medium", Tag = "Medium" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Hard", Tag = "Hard" });
            difficultyCombo.Items.Add(new ComboBoxItem { Content = "Expert", Tag = "Expert" });

            // Select current difficulty level
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

            // Default to Medium if not found
            if (difficultyCombo.SelectedIndex == -1)
                difficultyCombo.SelectedIndex = 2; // Medium is now index 2

            difficultyGroupBox.Content = difficultyCombo;
            mainPanel.Children.Add(difficultyGroupBox);

            // Add label for explanation of Random mode
            TextBlock randomExplanation = new TextBlock
            {
                Text = "Random mode makes the computer choose moves randomly rather than strategically.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 5, 10, 5),
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            };
            mainPanel.Children.Add(randomExplanation);

            // Bind visibility of color and difficulty panels to computer mode selection
            computerModeRadio.Checked += (s, e) =>
            {
                colorGroupBox.Visibility = Visibility.Visible;
                difficultyGroupBox.Visibility = Visibility.Visible;
                randomExplanation.Visibility = Visibility.Visible;
            };

            humanModeRadio.Checked += (s, e) =>
            {
                colorGroupBox.Visibility = Visibility.Collapsed;
                difficultyGroupBox.Visibility = Visibility.Collapsed;
                randomExplanation.Visibility = Visibility.Collapsed;
            };

            // Initialize visibility
            colorGroupBox.Visibility = computerModeRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            difficultyGroupBox.Visibility = computerModeRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            randomExplanation.Visibility = computerModeRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Add OK button
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
                bool wasComputerMode = _isComputerMode;

                // Apply selected mode
                _isComputerMode = computerModeRadio.IsChecked == true;
                _gameLogic.SetComputerMode(_isComputerMode);
                IsComputerMode = _isComputerMode;

                if (_isComputerMode)
                {
                    // Apply color selection
                    _playerPlaysBlack = blackRadio.IsChecked == true;
                    _playerColor = _playerPlaysBlack ? ChessColor.Black : ChessColor.White;
                    _gameLogic.PlayerPlaysBlack = _playerPlaysBlack;

                    // Update UI to reflect player's color
                    if (PlayAsWhiteRadioButton != null)
                        PlayAsWhiteRadioButton.IsChecked = !_playerPlaysBlack;
                    if (PlayAsBlackRadioButton != null)
                        PlayAsBlackRadioButton.IsChecked = _playerPlaysBlack;

                    // Apply difficulty
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

                        // Update difficulty combobox in main UI
                        if (DifficultyComboBox != null)
                        {
                            for (int i = 0; i < DifficultyComboBox.Items.Count; i++)
                            {
                                if (DifficultyComboBox.Items[i] is ComboBoxItem item &&
                                    item.Tag?.ToString() == difficultyStr)
                                {
                                    DifficultyComboBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    }

                    // Show difficulty combobox
                    if (DifficultyComboBox != null)
                        DifficultyComboBox.Visibility = Visibility.Visible;
                }
                else
                {
                    // Hide difficulty combobox in two player mode
                    if (DifficultyComboBox != null)
                        DifficultyComboBox.Visibility = Visibility.Collapsed;
                }

                // Check if it's computer's turn after potential new game
                bool isComputerTurn = _isComputerMode &&
                                    ((_playerPlaysBlack && _currentPlayer == "white") ||
                                     (!_playerPlaysBlack && _currentPlayer == "black"));

                // If it's computer's turn, make computer move
                if (isComputerTurn)
                {
                    Task.Run(() =>
                    {
                        // Small delay for better user experience
                        System.Threading.Thread.Sleep(500);

                        // Make the computer move
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _gameLogic.MakeComputerMove();
                        });
                    });
                }

                // Update everything
                UpdateStatusText();
                ForceRefreshBoardState();

                // Close the dialog
                modeSelectionWindow.Close();
            };

            mainPanel.Children.Add(okButton);
            modeSelectionWindow.Content = mainPanel;
            modeSelectionWindow.ShowDialog();
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
                // Use Dispatcher to ensure UI thread update
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
            // Check if it's the player's turn
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
                // Move was successful - update UI state
                ClearHighlights();

                // Instead of individual updates, use force refresh to ensure everything is updated
                ForceRefreshBoardState();
            }
        }

        /// <summary>
        /// Force refreshes the board state and all related UI elements.
        /// </summary>
        private void ForceRefreshBoardState()
        {
            // Ensure current player is synced with game logic
            _currentPlayer = _gameLogic.CurrentPlayer;

            // Clear any selected cells or highlights
            ClearHighlights();
            _draggedCell = null;

            // Refresh the board from game logic
            Board = _gameLogic.GetCurrentBoard();

            // Update UI
            UpdateBoardUI();
            UpdateStatusAndTimers();
        }

        /// <summary>
        /// Updates both status text and timer states.
        /// </summary>
        private void UpdateStatusAndTimers()
        {
            // Update status text first
            UpdateStatusText();

            // Then update active timer based on current player
            StopTimers(); // Stop both timers first

            if (!_isGameActive || _isTimersPaused) return;

            // Start the appropriate timer based on current player
            StartTimers();
        }

        /// <summary>
        /// Handles the start new game button click.
        /// </summary>
        private void StartNewGame_Click(object sender, RoutedEventArgs e)
        {
            if (ShowConfirmation("Are you sure you want to start a new game? All unsaved progress will be lost."))
            {
                // Reset window size to normal
                this.Width = 1100;

                // Clear move history first
                ClearMoveHistory();

                // Set player color
                _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
                _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;
                _currentPlayer = "white";

                // Initialize the game
                _gameLogic.InitializeGame();
                InitializeBoardUI();
                UpdateBoardUI();

                // Exit setup mode if active
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

                    // Find and update the setup button
                    Button? setupButton = FindButtonByContent("Finish Setup");
                    if (setupButton != null)
                    {
                        setupButton.Content = "Setup Position";
                    }
                }

                // Exit analysis mode if active
                if (_isAnalysisMode)
                {
                    _isAnalysisMode = false;
                    Button? analyzeButton = FindButtonByContent("Exit Analysis");
                    if (analyzeButton != null)
                        analyzeButton.Content = "Analyze Position";
                }

                // Reset timers
                _isTimersPaused = false;
                ResetTimers();
                StartTimers();

                // Update game state
                _isGameActive = true;
                ForceRefreshBoardState();
            }
        }

        /// <summary>
        /// Handles the clear board button click.
        /// </summary>
        private void ClearBoard_Click(object sender, RoutedEventArgs e)
        {
            if (_isSetupPositionMode)
            {
                // In setup mode, we don't need to ask for confirmation - just clear the board
                _gameLogic.ClearBoard();
                UpdateBoardUI();
                ClearMoveHistory();
                _currentPlayer = "white";
                UpdateStatusText();

                // Reset piece counts display in setup mode
                RefreshSetupPanelDisplay();

                ResetTimers();
                _isTimersPaused = false;
                if (TimerControlButton != null)
                    TimerControlButton.Content = "Pause Timers";
                StartTimers();

                _isGameActive = true;
                ClearHighlights();
            }
            else
            {
                // Outside of setup mode, ask for confirmation
                if (ShowConfirmation("Are you sure you want to clear the board and move history?"))
                {
                    _gameLogic.ClearBoard();
                    UpdateBoardUI();
                    ClearMoveHistory();
                    _currentPlayer = "white";
                    UpdateStatusText();

                    ResetTimers();
                    _isTimersPaused = false;
                    if (TimerControlButton != null)
                        TimerControlButton.Content = "Pause Timers";
                    StartTimers();

                    _isGameActive = true;
                    ClearHighlights();
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
                // Single confirmation dialog asking about switching modes and starting a new game
                MessageBoxResult result = MessageBox.Show(
                    "Switch to two player mode? This will start a new game.",
                    "Switch to Two Players Mode",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Directly proceed with mode switch and new game - no second confirmation
                    _isComputerMode = false;
                    _gameLogic.SetComputerMode(false);

                    if (DifficultyComboBox != null)
                        DifficultyComboBox.Visibility = Visibility.Collapsed;

                    IsComputerMode = false;

                    // Clear history and start new game
                    ClearMoveHistory();
                    _currentPlayer = "white";
                    _gameLogic.InitializeGame();
                    InitializeBoardUI();
                    UpdateBoardUI();

                    // Reset timers
                    _isTimersPaused = false;
                    ResetTimers();
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

            // Always confirm first to avoid multiple dialog boxes
            MessageBoxResult result = MessageBox.Show(
                "Switch to computer mode? This will start a new game.",
                "Switch to Computer Mode",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
                return;

            // Now proceed with switching to computer mode
            _isComputerMode = true;
            _gameLogic.SetComputerMode(true);

            if (DifficultyComboBox != null)
                DifficultyComboBox.Visibility = Visibility.Visible;

            IsComputerMode = true;

            // Set player color
            _playerColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;
            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

            // Clear move history first
            ClearMoveHistory();

            // Reset the game
            _currentPlayer = "white";
            _gameLogic.InitializeGame();
            InitializeBoardUI();

            // Exit analysis mode if active
            if (_isAnalysisMode)
            {
                _isAnalysisMode = false;
                Button? analyzeButton = FindButtonByContent("Exit Analysis");
                if (analyzeButton != null)
                    analyzeButton.Content = "Analyze Position";
            }

            // Reset timers
            _isTimersPaused = false;
            ResetTimers();

            // Start timers based on current player
            StartTimers();

            UpdateStatusText();

            // Check if it's computer's turn
            bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                (!_playerPlaysBlack && _currentPlayer == "black");

            // If it's computer's turn, make computer move
            if (isComputerTurn)
            {
                _gameLogic.MakeComputerMove();
            }

            // Force refresh after any computer move
            ForceRefreshBoardState();
        }

        /// <summary>
        /// Handles player color radio button checks.
        /// </summary>
        private void PlayerColorRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || !_isComputerMode) return;

            ChessColor newColor = PlayAsWhiteRadioButton?.IsChecked == true ? ChessColor.White : ChessColor.Black;

            // Only change if the color actually changed
            if (_playerColor != newColor)
            {
                // Ask if user wants to keep current position
                MessageBoxResult result = MessageBox.Show(
                    "Changing color will start a new game. Continue?",
                    "Color Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _playerColor = newColor;
                    _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black;

                    // Reset the game
                    ClearMoveHistory();
                    _currentPlayer = "white";
                    _gameLogic.InitializeGame();
                    InitializeBoardUI();

                    // Reset timers
                    _isTimersPaused = false;
                    ResetTimers();
                    StartTimers();

                    // Check if it's computer's turn
                    bool isComputerTurn = (_playerPlaysBlack && _currentPlayer == "white") ||
                                        (!_playerPlaysBlack && _currentPlayer == "black");

                    // If computer plays first, make its move
                    if (isComputerTurn)
                    {
                        _gameLogic.MakeComputerMove();
                    }

                    // Force refresh after potentially making a move
                    ForceRefreshBoardState();
                }
                else
                {
                    // Revert back to previous selection
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
        /// Creates a border with proper thickness.
        /// </summary>
        private Thickness CreateBorderThickness(double uniformThickness)
        {
            return new Thickness(uniformThickness);
        }

        /// <summary>
        /// Creates a border with different thickness for each side.
        /// </summary>
        private Thickness CreateBorderThickness(double left, double top, double right, double bottom)
        {
            return new Thickness(left, top, right, bottom);
        }

        /// <summary>
        /// Creates a margin with proper thickness.
        /// </summary>
        private Thickness CreateMargin(double uniformMargin)
        {
            return new Thickness(uniformMargin);
        }

        /// <summary>
        /// Creates a margin with different values for each side.
        /// </summary>
        private Thickness CreateMargin(double left, double top, double right, double bottom)
        {
            return new Thickness(left, top, right, bottom);
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