using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Data;

namespace ChessTrainer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<BoardCell> _board = new ObservableCollection<BoardCell>();
        public ObservableCollection<BoardCell> Board
        {
            get { return _board; }
            set
            {
                _board = value;
                OnPropertyChanged(nameof(Board));
            }
        }

        private bool _isTwoPlayersMode = false;
        private bool _isComputerMode = false;
        private string _currentPlayer = "white";
        private int _computerDifficulty = 1;
        private ObservableCollection<string> _moveHistory = new ObservableCollection<string>();
        public ObservableCollection<string> MoveHistory
        {
            get { return _moveHistory; }
            set
            {
                _moveHistory = value;
                OnPropertyChanged(nameof(MoveHistory));
            }
        }

        private DispatcherTimer? _whiteTimer;
        private DispatcherTimer? _blackTimer;
        private TimeSpan _whiteTimeLeft;
        private TimeSpan _blackTimeLeft;
        private bool _isTimerRunning;

        private BoardCell? _selectedCell;

        public MainWindow()
        {
            InitializeComponent();
            InitializeBoard();
            DataContext = this;
            InitializeTimers();
            StartTimers();
        }

        private void InitializeBoard()
        {
            Board = new ObservableCollection<BoardCell>();
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    string? piece = null;
                    string? color = null;

                    if (row == 1) color = "black";
                    if (row == 6) color = "white";
                    if (row == 0 && (col == 0 || col == 7)) { piece = "♜"; color = "black"; }
                    if (row == 7 && (col == 0 || col == 7)) { piece = "♖"; color = "white"; }
                    if (row == 0 && (col == 1 || col == 6)) { piece = "♞"; color = "black"; }
                    if (row == 7 && (col == 1 || col == 6)) { piece = "♘"; color = "white"; }
                    if (row == 0 && (col == 2 || col == 5)) { piece = "♝"; color = "black"; }
                    if (row == 7 && (col == 2 || col == 5)) { piece = "♗"; color = "white"; }
                    if (row == 0 && col == 3) { piece = "♛"; color = "black"; }
                    if (row == 7 && col == 3) { piece = "♕"; color = "white"; }
                    if (row == 0 && col == 4) { piece = "♚"; color = "black"; }
                    if (row == 7 && col == 4) { piece = "♔"; color = "white"; }
                    if (row == 1) piece = "♟";
                    if (row == 6) piece = "♙";

                    Board.Add(new BoardCell(row, col, (row + col) % 2 == 0 ? Brushes.LightGray : Brushes.White, piece, color));
                }
            }
        }

        private void ClearBoard_Click(object sender, RoutedEventArgs e)
        {
            InitializeBoard();
            MoveHistory.Clear();
            _currentPlayer = "white";
            StatusTextBlock.Text = "Дошку очищено. Хід білих.";
            ResetTimers();
            StartTimers();
        }

        private void SetTwoPlayersMode_Click(object sender, RoutedEventArgs e)
        {
            _isTwoPlayersMode = true;
            _isComputerMode = false;
            DifficultyComboBox.Visibility = Visibility.Collapsed;
            StatusTextBlock.Text = "Режим для двох гравців.";
            ResetTimers();
            StartTimers();
        }

        private void SetComputerMode_Click(object sender, RoutedEventArgs e)
        {
            _isComputerMode = true;
            _isTwoPlayersMode = false;
            DifficultyComboBox.Visibility = Visibility.Visible;
            StatusTextBlock.Text = "Режим проти комп'ютера. Хід білих.";
            ResetTimers();
            StartTimers();
        }

        private void SavePosition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter("chess_position.txt"))
                {
                    foreach (var cell in Board)
                    {
                        writer.WriteLine($"{cell.Row},{cell.Col},{cell.Piece},{cell.Color}");
                    }
                    writer.WriteLine(_currentPlayer);
                    writer.WriteLine(_whiteTimeLeft.Ticks);
                    writer.WriteLine(_blackTimeLeft.Ticks);
                }
                MessageBox.Show("Позицію збережено у файл chess_position.txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при збереженні позиції: {ex.Message}");
            }
        }

        private void LoadPosition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists("chess_position.txt"))
                {
                    using (StreamReader reader = new StreamReader("chess_position.txt"))
                    {
                        Board.Clear();
                        for (int i = 0; i < 64; i++)
                        {
                            string? line = reader.ReadLine();
                            if (line != null)
                            {
                                string[] parts = line.Split(',');
                                int row = int.Parse(parts[0]);
                                int col = int.Parse(parts[1]);
                                string piece = parts[2];
                                string color = parts[3];
                                Board.Add(new BoardCell(row, col, (row + col) % 2 == 0 ? Brushes.LightGray : Brushes.White, piece, color));
                            }
                        }

                        string? currentPlayerLine = reader.ReadLine();
                        if (currentPlayerLine != null)
                            _currentPlayer = currentPlayerLine;

                        string? whiteTimeTicksStr = reader.ReadLine();
                        string? blackTimeTicksStr = reader.ReadLine();

                        if (!string.IsNullOrEmpty(whiteTimeTicksStr) && !string.IsNullOrEmpty(blackTimeTicksStr))
                        {
                            _whiteTimeLeft = TimeSpan.FromTicks(long.Parse(whiteTimeTicksStr));
                            _blackTimeLeft = TimeSpan.FromTicks(long.Parse(blackTimeTicksStr));
                        }
                        else
                        {
                            InitializeTimers();
                        }
                        OnPropertyChanged(nameof(Board));
                        UpdateTimersDisplay();
                        StartTimers();
                        MessageBox.Show("Позицію завантажено з файлу chess_position.txt");
                    }
                }
                else
                {
                    MessageBox.Show("Файл chess_position.txt не знайдено.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні позиції: {ex.Message}");
            }
        }

        private void UpdateMoveHistory(string move)
        {
            MoveHistory.Add(move);
            MoveHistoryListBox.Items.Add(move);
            MoveHistoryListBox.ScrollIntoView(move);
        }

        private void SwitchPlayer()
        {
            _currentPlayer = _currentPlayer == "white" ? "black" : "white";
            StatusTextBlock.Text = $"Хід {_currentPlayer}.";

            if (_isTimerRunning)
            {
                if (_currentPlayer == "white")
                {
                    _blackTimer?.Stop();
                    _whiteTimer?.Start();
                }
                else
                {
                    _whiteTimer?.Stop();
                    _blackTimer?.Start();
                }
            }
        }

        private void InitializeTimers()
        {
            _whiteTimeLeft = TimeSpan.FromMinutes(30);
            _blackTimeLeft = TimeSpan.FromMinutes(30);

            _whiteTimer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, WhiteTimerTick, Dispatcher.CurrentDispatcher);
            _blackTimer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, BlackTimerTick, Dispatcher.CurrentDispatcher);
            _isTimerRunning = false;

            UpdateTimersDisplay();
        }

        private void StartTimers()
        {
            if (_currentPlayer == "white")
            {
                _whiteTimer?.Start();
            }
            else
            {
                _blackTimer?.Start();
            }
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
            if (_whiteTimeLeft > TimeSpan.Zero)
            {
                _whiteTimeLeft = _whiteTimeLeft.Subtract(TimeSpan.FromSeconds(1));
                if (WhiteTimeTextBlock != null)
                    WhiteTimeTextBlock.Text = _whiteTimeLeft.ToString(@"hh\:mm\:ss");
            }
            else
            {
                _whiteTimer?.Stop();
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Чорні перемогли за часом!";
            }
        }

        private void BlackTimerTick(object? sender, EventArgs e)
        {
            if (_blackTimeLeft > TimeSpan.Zero)
            {
                _blackTimeLeft = _blackTimeLeft.Subtract(TimeSpan.FromSeconds(1));
                if (BlackTimeTextBlock != null)
                    BlackTimeTextBlock.Text = _blackTimeLeft.ToString(@"hh\:mm\:ss");
            }
            else
            {
                _blackTimer?.Stop();
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Білі перемогли за часом!";
            }
        }

        private void UpdateTimersDisplay()
        {
            if (WhiteTimeTextBlock != null)
                WhiteTimeTextBlock.Text = _whiteTimeLeft.ToString(@"hh\:mm\:ss");
            if (BlackTimeTextBlock != null)
                BlackTimeTextBlock.Text = _blackTimeLeft.ToString(@"hh\:mm\:ss");
        }

        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void BoardCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BoardCell cell)
            {
                if (cell.Piece != null && ((cell.Color == "white" && _currentPlayer == "white") || (cell.Color == "black" && _currentPlayer == "black")))
                {
                    _selectedCell = cell;
                    try
                    {
                        DragDrop.DoDragDrop(element, cell, DragDropEffects.Move);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception during DragDrop: {ex.Message}");
                    }
                }
            }
        }

        private void BoardCell_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BoardCell cell)
            {
                if (_selectedCell != null && cell != _selectedCell)
                {
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            }
        }

        private void BoardCell_Drop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BoardCell targetCell)
            {
                if (_selectedCell != null)
                {
                    string? movingPiece = _selectedCell.Piece;
                    string? movingPieceColor = _selectedCell.Color;

                    _selectedCell.Piece = null;
                    _selectedCell.Color = null;

                    targetCell.Piece = movingPiece;
                    targetCell.Color = movingPieceColor;

                    string moveString = $"{GetCellNotation(_selectedCell.Col, _selectedCell.Row)} - {GetCellNotation(targetCell.Col, targetCell.Row)}";
                    UpdateMoveHistory(moveString);

                    SwitchPlayer();

                    OnPropertyChanged(nameof(Board));

                    _selectedCell = null;
                }
            }
            e.Handled = true;
        }

        private string GetCellNotation(int col, int row)
        {
            char colChar = (char)('a' + col);
            int rowNum = 8 - row;
            return $"{colChar}{rowNum}";
        }
    }

    [Serializable]
    public class BoardCell : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Row { get; }
        public int Col { get; }

        private Brush _backgroundColor = Brushes.White;
        public Brush BackgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }

        private string? _piece;
        public string? Piece
        {
            get { return _piece; }
            set
            {
                _piece = value;
                OnPropertyChanged(nameof(Piece));
            }
        }

        private string? _color;
        public string? Color
        {
            get { return _color; }
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
            }
        }

        public BoardCell()
        {
            _piece = null;
            _color = null;
        }

        public BoardCell(int row, int col, Brush backgroundColor, string? piece, string? color)
        {
            Row = row;
            Col = col;
            BackgroundColor = backgroundColor;
            Piece = piece;
            Color = color;
        }

        protected virtual void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorName)
            {
                switch (colorName.ToLower())
                {
                    case "white":
                        return Brushes.White;
                    case "black":
                        return Brushes.Black;
                    default:
                        return Brushes.Gray;
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class PieceColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is string pieceColor && values[1] is Brush backgroundColor)
            {
                if (pieceColor.ToLower() == "white")
                {
                    if (backgroundColor == Brushes.White || backgroundColor == Brushes.LightGray)
                    {
                        return Brushes.Black;
                    }
                    else
                    {
                        return Brushes.White;
                    }
                }
                else if (pieceColor.ToLower() == "black")
                {
                    return Brushes.Black;
                }
            }
            return Brushes.Gray;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
