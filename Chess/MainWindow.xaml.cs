using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;

namespace ChessTrainer
{
    public enum ChessColor
    {
        White,
        Black
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // --- Поля ---
        private bool _isGameActive = true;
        private string _gameResultText = "";
        private ObservableCollection<BoardCell> _board = new ObservableCollection<BoardCell>();
        private ChessColor _playerColor = ChessColor.White;
        private bool _isTwoPlayersMode = false;
        private bool _isComputerMode = false;
        private string _currentPlayer = "white";
        private int _computerDifficulty = 1;
        private ObservableCollection<string> _moveHistory = new ObservableCollection<string>();
        private DispatcherTimer? _whiteTimer;
        private DispatcherTimer? _blackTimer;
        private TimeSpan _whiteTimeLeft;
        private TimeSpan _blackTimeLeft;
        private bool _isTimerRunning;
        private bool _isPositionLoaded = false;
        private bool _firstMoveAfterLoad = false;
        private bool _isClearingFromComputerMode = false;
        private GameLogic _gameLogic;
        private Color _lightBoardColor = Brushes.LightGray.Color;
        private Color _darkBoardColor = Brushes.White.Color;
        private Point _dragStartPoint;
        private BoardCell? _draggedCell;

        // --- Властивості ---
        public bool IsGameActive
        {
            get => _isGameActive;
            set => SetProperty(ref _isGameActive, value);
        }

        public string GameResultText
        {
            get => _gameResultText;
            set => SetProperty(ref _gameResultText, value);
        }

        public ObservableCollection<BoardCell> Board
        {
            get => _board;
            set => SetProperty(ref _board, value);
        }

        public ObservableCollection<string> MoveHistory
        {
            get => _moveHistory;
            set => SetProperty(ref _moveHistory, value);
        }

        // --- Події ---
        public event PropertyChangedEventHandler? PropertyChanged;

        // --- Конструктор ---
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _gameLogic = new GameLogic();
            _gameLogic.BoardUpdated += OnGameLogicBoardUpdated;
            _gameLogic.MoveMade += OnGameLogicMoveMade;
            _gameLogic.GameEnded += OnGameLogicGameEnded;

            Board = _gameLogic.GetCurrentBoard(); // Ініціалізація дошки
            InitializeTimers();
            StartTimers();
            UpdateStatusText();
            DifficultyComboBox.SelectedIndex = 0; // За замовчуванням - Легкий
        }

        // --- Методи ---

        // Метод для встановлення значення властивості та виклику OnPropertyChanged
        private void SetProperty<T>(ref T field, T newValue, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                OnPropertyChanged(propertyName);
            }
        }

        // Метод для виклику PropertyChanged
        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Обробники подій GameLogic
        private void OnGameLogicBoardUpdated(object? sender, EventArgs e) =>
            Dispatcher.Invoke(UpdateBoardUI);

        private void OnGameLogicMoveMade(object? sender, EventArgs e)
        {
            Board = _gameLogic.GetCurrentBoard();
            UpdateStatusText();
            // Тут можна додати логіку відтворення звуку ходу
        }

        private void OnGameLogicGameEnded(object? sender, EventArgs e)
        {
            IsGameActive = false;
            string resultMessage = "Гра завершена!";

            if (sender is GameLogic gameLogic)
            {
                string currentPlayer = gameLogic.CurrentPlayer == "white" ? "Білі" : "Чорні";
                string opponentColor = gameLogic.CurrentPlayer == "white" ? "Чорні" : "Білі";
                Piece?[,] currentBoardPieces = gameLogic.Board.GetPieces();

                // Перевірка на мат
                int opponentKingRow = -1, opponentKingCol = -1;
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        if (currentBoardPieces[r, c]?.Color == opponentColor && currentBoardPieces[r, c]?.Type == "king")
                        {
                            opponentKingRow = r;
                            opponentKingCol = c;
                            break;
                        }
                    }
                    if (opponentKingRow != -1) break;
                }

                if (opponentKingRow != -1 && gameLogic.Board.IsKingInCheck(opponentKingRow, opponentKingCol, gameLogic.CurrentPlayer))
                {
                    resultMessage = $"{currentPlayer} оголосили мат!";
                }
                else if (gameLogic.Board.GetAllPossibleMovesForPlayer(opponentColor).Count == 0)
                {
                    resultMessage = "Пат!";
                }
                else if (!gameLogic.GetCurrentBoard().Any(cell => cell.Piece?.Type == "king" && cell.Piece.Color == opponentColor))
                {
                    resultMessage = $"{currentPlayer} виграли!"; // Захоплення короля
                }
            }

            GameResultText = resultMessage;
            StopTimers();
        }

        // --- Методи управління грою ---
        private void StartNewGame_Click(object sender, RoutedEventArgs e)
        {
            _playerColor = PlayAsWhiteRadioButton.IsChecked == true ? ChessColor.White : ChessColor.Black;
            _gameLogic.PlayerPlaysBlack = _playerColor == ChessColor.Black; // Встановлюємо колір гравця в GameLogic
            InitializeGame();
            InitializeBoardUI();
        }

        private void InitializeGame()
        {
            _gameLogic.InitializeGame();
            _currentPlayer = "white";

            if (_isComputerMode && _playerColor == ChessColor.Black)
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

        private void InitializeBoardUI()
        {
            Board = new ObservableCollection<BoardCell>(
                _gameLogic.Board.GetPieces().Cast<Piece?>().Select((piece, index) =>
                {
                    int row = index / 8;
                    int col = index % 8;
                    Color background = (row + col) % 2 == 0 ? _lightBoardColor : _darkBoardColor;
                    return new BoardCell(row, col, new SolidColorBrush(background), piece);
                })
            );
        }

        private void UpdateBoardUI()
        {
            InitializeBoardUI();
        }

        private void UpdateMoveHistory(string move)
        {
            MoveHistory.Add(move);
            MoveHistoryListBox.Items.Add(move);
            MoveHistoryListBox.ScrollIntoView(move);
        }

        private string GetSquareNotation(int col, int row) => $"{(char)('a' + col)}{8 - row}";

        private void ClearBoard_Click(object sender, RoutedEventArgs e)
        {
            if (_isClearingFromComputerMode || ShowConfirmation("Ви впевнені, що хочете очистити дошку та історію ходів?"))
            {
                _gameLogic.InitializeGame();
                UpdateBoardUI();
                MoveHistory.Clear();
                _currentPlayer = "white";
                UpdateStatusText();
                ResetTimers();
                StartTimers();
                _isGameActive = true; // Переконайтеся, що гра активна після очищення
            }
        }

        private bool ShowConfirmation(string message) =>
            MessageBox.Show(message, "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private void SetTwoPlayersMode_Click(object sender, RoutedEventArgs e)
        {
            ClearBoard_Click(sender, e);
            _isTwoPlayersMode = true;
            _isComputerMode = false;
            _gameLogic.SetComputerMode(false);
            DifficultyComboBox.Visibility = Visibility.Collapsed;
            UpdateStatusText();
            ResetTimers();
            StartTimers();
        }

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
                DifficultyComboBox.Visibility = Visibility.Visible;
                UpdateStatusText();
                _currentPlayer = "white";
                StartTimers();
                _isPositionLoaded = false;
                _firstMoveAfterLoad = false;
                _isClearingFromComputerMode = false;
            }
        }

        private void SavePosition_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функціонал збереження позиції буде реалізовано.", "Збереження");
            // Логіка збереження позиції
        }

        private void LoadPosition_Click(object sender, RoutedEventArgs e)
        {
            // Тимчасова логіка завантаження (потрібно замінити)
            Piece?[,] loadedBoardState = _gameLogic.Board.GetPieces();
            string loadedCurrentPlayer = _currentPlayer;
            ObservableCollection<string> loadedMoveHistory = new ObservableCollection<string>(MoveHistory);

            _gameLogic.LoadGame(loadedBoardState, loadedCurrentPlayer);
            Board = _gameLogic.GetCurrentBoard();
            _currentPlayer = loadedCurrentPlayer;
            UpdateStatusText();
            UpdateMoveHistoryFromLoaded(loadedMoveHistory);
            _isPositionLoaded = true;
            _firstMoveAfterLoad = true;
            ResetTimers();
            StartTimers();
            MessageBox.Show("Позицію гри завантажено (тимчасово поточну).", "Завантаження");
        }

        private void UpdateMoveHistoryFromLoaded(ObservableCollection<string> loadedHistory)
        {
            MoveHistory.Clear();
            MoveHistoryListBox.Items.Clear();
            foreach (var move in loadedHistory)
            {
                UpdateMoveHistory(move);
            }
        }

        public void SetBoardColors(Color lightColor, Color darkColor)
        {
            _lightBoardColor = lightColor;
            _darkBoardColor = darkColor;
            UpdateBoardUI();
        }

        private void SwitchPlayer()
        {
            _currentPlayer = _gameLogic.CurrentPlayer;
            UpdateStatusText();
            if (_isTimerRunning)
            {
                _whiteTimer?.Stop();
                _blackTimer?.Stop();
                if (_currentPlayer == "white") _whiteTimer?.Start();
                else _blackTimer?.Start();
            }
        }

        private void UpdateStatusText()
        {
            StatusTextBlock.Text = _isComputerMode ? $"Режим проти комп'ютера. Хід {_currentPlayer}." : $"Режим для двох гравців. Хід {_currentPlayer}.";
        }

        // --- Методи таймерів ---
        private void InitializeTimers()
        {
            _whiteTimeLeft = TimeSpan.FromMinutes(30);
            _blackTimeLeft = TimeSpan.FromMinutes(30);

            _whiteTimer = CreateTimer(WhiteTimerTick);
            _blackTimer = CreateTimer(BlackTimerTick);
            _isTimerRunning = false;

            UpdateTimersDisplay();
        }

        private DispatcherTimer CreateTimer(EventHandler tickHandler)
        {
            var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher) { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += tickHandler;
            return timer;
        }

        private void StartTimers()
        {
            _whiteTimer?.Stop();
            _blackTimer?.Stop();
            if (_currentPlayer == "white") _whiteTimer?.Start();
            else _blackTimer?.Start();
            _isTimerRunning = true;
        }

        private void StopTimers()
        {
            _whiteTimer?.Stop();
            _blackTimer?.Stop();
            _isTimerRunning = false;
        }

        private void ResetTimers()
        {
            StopTimers();
            InitializeTimers();
        }

        private void WhiteTimerTick(object? sender, EventArgs e)
        {
            UpdateTimeLeft(ref _whiteTimeLeft, WhiteTimeTextBlock, "Чорні перемогли за часом!");
        }

        private void BlackTimerTick(object? sender, EventArgs e)
        {
            UpdateTimeLeft(ref _blackTimeLeft, BlackTimeTextBlock, "Білі перемогли за часом!");
        }

        private void UpdateTimeLeft(ref TimeSpan timeLeft, TextBlock? textBlock, string gameOverMessage)
        {
            if (timeLeft > TimeSpan.Zero)
            {
                timeLeft = timeLeft.Subtract(TimeSpan.FromSeconds(1));
                if (textBlock != null) textBlock.Text = timeLeft.ToString(@"hh\:mm\:ss");
            }
            else
            {
                StopTimers();
                StatusTextBlock.Text = gameOverMessage;
            }
        }

        private void UpdateTimersDisplay()
        {
            WhiteTimeTextBlock.Text = _whiteTimeLeft.ToString(@"hh\:mm\:ss");
            BlackTimeTextBlock.Text = _blackTimeLeft.ToString(@"hh\:mm\:ss");
        }

        // --- Обробка дій користувача ---
        private void PlayerColorRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            _gameLogic.PlayerPlaysBlack = PlayAsBlackRadioButton.IsChecked == true;
            InitializeGame();
        }

        private void FlipBoardCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            //  _gameLogic.FlipBoardView = FlipBoardCheckBox.IsChecked == true; //  Немає FlipBoardView в GameLogic
            UpdateBoardUI();
        }

        private void BoardCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border clickedBorder) || !(clickedBorder.DataContext is BoardCell clickedCell)) return;

            _dragStartPoint = e.GetPosition(null);
            _draggedCell = clickedCell;

            DataObject dragData = new DataObject(typeof(BoardCell), clickedCell);
            DragDrop.DoDragDrop(clickedBorder, dragData, DragDropEffects.Move);
        }

        private void BoardCell_DragEnter(object sender, DragEventArgs e)
        {
            if (!(sender is Border targetBorder) || !(targetBorder.DataContext is BoardCell targetCell) || _draggedCell == null || targetCell == _draggedCell) return;

            e.Effects = e.Data.GetDataPresent(typeof(BoardCell)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void BoardCell_Drop(object sender, DragEventArgs e)
        {
            if (!(sender is Border dropBorder) || !(dropBorder.DataContext is BoardCell dropCell) || _draggedCell == null || dropCell == _draggedCell || !_isGameActive) return;

            TryMove(_draggedCell, dropCell);
            _draggedCell = null;
        }

        private void TryMove(BoardCell startCell, BoardCell endCell)
        {
            if (_gameLogic.TryMovePiece(startCell.Row, startCell.Col, endCell.Row, endCell.Col))
            {
                string moveNotation = GetMoveNotation(startCell, endCell);
                UpdateMoveHistory(moveNotation);
                SwitchPlayer();
                UpdateBoardUI();

                HandleComputerMoveAfterPlayerMove();
            }
        }

        private void HandleComputerMoveAfterPlayerMove()
        {
            if (_isPositionLoaded && _isComputerMode && _firstMoveAfterLoad)
            {
                _firstMoveAfterLoad = false;
                if (ShowConfirmation("Продовжити гру з поточної позиції проти комп'ютера?\nНатисніть 'Так' для продовження, 'Ні' - для початку нової гри."))
                { 
                    _gameLogic.SetComputerMode(true);
                    DifficultyComboBox.Visibility = Visibility.Visible;
                    UpdateStatusText();
                    if (_currentPlayer == "black" && _isGameActive)
                    {
                        _gameLogic.MakeComputerMove();
                        SwitchPlayer();
                        UpdateBoardUI();
                    }
                }
                else
                {
                    SetComputerMode_Click(this, new RoutedEventArgs());
                }
            }
            else if (_isComputerMode && _currentPlayer == "black" && _isGameActive)
            {
                _gameLogic.MakeComputerMove();
                SwitchPlayer();
                UpdateBoardUI();
            }
        }

        private string GetMoveNotation(BoardCell startCell, BoardCell endCell) =>
            $"{GetSquareNotation(startCell.Col, startCell.Row)}-{GetSquareNotation(endCell.Col, endCell.Row)}";

        private void DifficultyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _gameLogic.ComputerDifficulty = selectedItem.Content.ToString() switch
                {
                    "Легкий" => 1,
                    "Середній" => 2,
                    "Складний" => 3,
                    _ => 1
                };
            }
        }
    }
}