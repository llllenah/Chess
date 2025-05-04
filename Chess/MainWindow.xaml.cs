using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;

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
        private bool _isBoardFlipped = false;

        public bool IsBoardFlipped
        {
            get => _isBoardFlipped;
            set => SetProperty(ref _isBoardFlipped, value);
        }

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

        // Функція збереження (виправлена)
        private void SavePosition_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Chess Position File (*.ches)|*.ches";
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        // Зберігаємо конфігурацію дошки
                        for (int row = 0; row < 8; row++)
                        {
                            string rowString = "";
                            for (int col = 0; col < 8; col++)
                            {
                                BoardCell cell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
                                if (cell != null && cell.Piece != null)
                                {
                                    // Перший символ - колір (w для білих, b для чорних)
                                    char colorChar = cell.Piece.Color == "white" ? 'w' : 'b';

                                    // Другий символ - тип фігури
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
                                    rowString += ".."; // Використовуємо два символи для порожніх клітинок для узгодженості
                                }
                            }
                            writer.WriteLine(rowString);
                        }

                        // Зберігаємо поточного гравця
                        writer.WriteLine($"CurrentPlayer:{_currentPlayer}");

                        // Зберігаємо історію ходів
                        writer.WriteLine("MoveHistory:");
                        foreach (var move in MoveHistory)
                        {
                            writer.WriteLine(move);
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

        // Функція завантаження (виправлена)
        private void LoadPosition_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Chess Position File (*.ches)|*.ches";
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openFileDialog.FileName);
                    if (lines.Length >= 8)
                    {
                        Piece?[,] loadedBoardState = new Piece?[8, 8];

                        // Зчитуємо конфігурацію дошки
                        for (int row = 0; row < 8; row++)
                        {
                            string line = lines[row];

                            // Перевіряємо чи лінія містить правильну кількість символів
                            // Якщо старий формат - очікуємо 8 символів (по 1 на клітинку)
                            // Якщо новий формат - очікуємо 16 символів (по 2 на клітинку)
                            if (line.Length == 8 || line.Length == 16)
                            {
                                for (int col = 0; col < 8; col++)
                                {
                                    Piece? piece = null;

                                    if (line.Length == 8) // Старий формат
                                    {
                                        string pieceCode = line[col].ToString();
                                        piece = DecodePiece(pieceCode);
                                    }
                                    else if (line.Length == 16) // Новий формат (2 символи на клітинку)
                                    {
                                        int idx = col * 2;
                                        string pieceCode = line.Substring(idx, 2);

                                        // Якщо не порожня клітинка
                                        if (pieceCode != ".." && pieceCode.Length == 2)
                                        {
                                            char colorChar = pieceCode[0];
                                            char typeChar = pieceCode[1];

                                            string color = colorChar == 'w' ? "white" : (colorChar == 'b' ? "black" : "");
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

                                            if (!string.IsNullOrEmpty(color) && !string.IsNullOrEmpty(type))
                                            {
                                                piece = new Piece(color, type);
                                            }
                                        }
                                    }

                                    loadedBoardState[row, col] = piece;
                                }
                            }
                            else
                            {
                                MessageBox.Show("Файл пошкоджено (неправильний формат дошки).", "Помилка");
                                return;
                            }
                        }

                        // Зчитуємо поточного гравця та історію ходів
                        string loadedCurrentPlayer = "white";
                        ObservableCollection<string> loadedMoveHistory = new ObservableCollection<string>();

                        for (int i = 8; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith("CurrentPlayer:"))
                            {
                                loadedCurrentPlayer = lines[i].Substring("CurrentPlayer:".Length);
                            }
                            else if (lines[i] == "MoveHistory:")
                            {
                                // Наступні рядки - це історія ходів
                            }
                            else if (!string.IsNullOrWhiteSpace(lines[i]))
                            {
                                loadedMoveHistory.Add(lines[i]);
                            }
                        }

                        // Завантажуємо гру
                        _gameLogic.LoadGame(loadedBoardState, loadedCurrentPlayer);
                        Board = _gameLogic.GetCurrentBoard();
                        _currentPlayer = loadedCurrentPlayer;
                        UpdateStatusText();
                        UpdateMoveHistoryFromLoaded(loadedMoveHistory);
                        _isPositionLoaded = true;
                        _firstMoveAfterLoad = true;
                        ResetTimers();
                        StartTimers();

                        MessageBox.Show("Позицію гри завантажено.", "Завантаження");
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

        // Допоміжний метод декодування фігури - залишаємо для сумісності зі старим форматом
        private Piece? DecodePiece(string code)
        {
            if (code == ".") return null;
            if (code.Length == 1)
            {
                // Старий формат - одиночний символ
                char pieceChar = code[0];

                // Спрощена логіка визначення кольору та типу фігури
                // (це приблизний приклад, може потребувати коригування)
                switch (pieceChar)
                {
                    case 'P': return new Piece("white", "pawn");
                    case 'R': return new Piece("white", "rook");
                    case 'N': return new Piece("white", "knight");
                    case 'B': return new Piece("white", "bishop");
                    case 'Q': return new Piece("white", "queen");
                    case 'K': return new Piece("white", "king");
                    case 'p': return new Piece("black", "pawn");
                    case 'r': return new Piece("black", "rook");
                    case 'n': return new Piece("black", "knight");
                    case 'b': return new Piece("black", "bishop");
                    case 'q': return new Piece("black", "queen");
                    case 'k': return new Piece("black", "king");
                    default: return null;
                }
            }
            else if (code.Length == 2)
            {
                // Новий формат - два символи (колір та тип)
                string color = code[0] == 'w' ? "white" : (code[0] == 'b' ? "black" : "");
                string type = code[1] switch
                {
                    'p' => "pawn",
                    'r' => "rook",
                    'n' => "knight",
                    'b' => "bishop",
                    'q' => "queen",
                    'k' => "king",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(color) && !string.IsNullOrEmpty(type))
                {
                    return new Piece(color, type);
                }
            }

            return null;
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
            StartNewGame_Click(sender, e);
        }

        private void FlipBoardCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Змінюємо стан перевертання
            IsBoardFlipped = (sender as CheckBox)?.IsChecked ?? false;

            // Оновлюємо UI дошки після перевертання
            UpdateBoardUI();
        }

        private void UpdateBoardUI()
        {
            Dispatcher.Invoke(() =>
            {
                // Отримуємо дошку з правильною орієнтацією
                Board = _gameLogic.GetCurrentBoard();

                // Якщо дошка перевернута, міняємо відображення, не змінюючи внутрішню логіку
                if (IsBoardFlipped)
                {
                    // Створюємо новий список клітинок з перевернутими візуальними координатами
                    var flippedBoard = new ObservableCollection<BoardCell>();

                    for (int row = 7; row >= 0; row--)
                    {
                        for (int col = 7; col >= 0; col--)
                        {
                            // Знаходимо оригінальну клітинку
                            var originalCell = Board.FirstOrDefault(c => c.Row == row && c.Col == col);
                            if (originalCell != null)
                            {
                                flippedBoard.Add(originalCell);
                            }
                        }
                    }

                    // Оновлюємо відображення перевернутої дошки
                    Board = flippedBoard;
                }

                ChessBoardItemsControl.ItemsSource = Board;
                ChessBoardItemsControl.Items.Refresh();
            });
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

                // Оновлюємо _currentPlayer після ходу гравця
                _currentPlayer = _gameLogic.CurrentPlayer;
                UpdateStatusText();
                UpdateBoardUI();

                // Виконуємо хід комп'ютера, якщо потрібно
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
                    // Перевіряємо чий зараз хід і чи потрібно комп'ютеру ходити
                    string currentPlayer = _gameLogic.CurrentPlayer;
                    bool isComputerTurn = (_playerColor == ChessColor.Black && currentPlayer == "white") ||
                                          (_playerColor == ChessColor.White && currentPlayer == "black");

                    if (isComputerTurn && _isGameActive)
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
            else if (_isComputerMode && _isGameActive)
            {
                // Перевіряємо, чи повинен комп'ютер зробити хід
                string currentPlayer = _gameLogic.CurrentPlayer;
                bool isComputerTurn = (_playerColor == ChessColor.Black && currentPlayer == "white") ||
                                      (_playerColor == ChessColor.White && currentPlayer == "black");

                if (isComputerTurn)
                {
                    _gameLogic.MakeComputerMove();
                    SwitchPlayer();
                    UpdateBoardUI();
                }
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