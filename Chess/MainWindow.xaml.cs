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
        private GameLogic _gameLogic;

        public MainWindow()
        {
            InitializeComponent();
            _gameLogic = new GameLogic();
            _gameLogic.BoardUpdated += _gameLogic_BoardUpdated;
            _gameLogic.MoveMade += _gameLogic_MoveMade;
            Board = _gameLogic.GetCurrentBoard();
            DataContext = this;
            InitializeTimers();
            StartTimers();
            UpdateStatusText();

            DifficultyComboBox.SelectedIndex = 0; // За замовчуванням - Легкий
        }

        private void _gameLogic_MoveMade(object? sender, string move)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateMoveHistory(move);
            });
        }

        private void _gameLogic_BoardUpdated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateBoardUI();
            });
        }
        private void _gameLogic_GameEnded(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Кінець гри", MessageBoxButton.OK, MessageBoxImage.Information);
                // Можливо, тут ви захочете запропонувати почати нову гру
            });
        }
        private void UpdateBoardUI()
        {
            Board = _gameLogic.GetCurrentBoard();
        }

        private void UpdateMoveHistory(string move)
        {
            MoveHistory.Add(move);
            MoveHistoryListBox.Items.Add(move);
            MoveHistoryListBox.ScrollIntoView(move);
        }

        private string GetSquareNotation(int col, int row)
        {
            char file = (char)('a' + col);
            int rank = 8 - row;
            return $"{file}{rank}";
        }

        private void ClearBoard_Click(object sender, RoutedEventArgs e)
        {
            _gameLogic = new GameLogic();
            UpdateBoardUI();
            MoveHistory.Clear();
            _currentPlayer = "white";
            UpdateStatusText();
            ResetTimers();
            StartTimers();
        }

        private void SetTwoPlayersMode_Click(object sender, RoutedEventArgs e)
        {
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
            _isComputerMode = true;
            _isTwoPlayersMode = false;
            _gameLogic.SetComputerMode(true);
            DifficultyComboBox.Visibility = Visibility.Visible;
            UpdateStatusText();
            ResetTimers();
            StartTimers();
        }

        private void SavePosition_Click(object sender, RoutedEventArgs e)
        {
            // Логіка збереження позиції (потрібно оновити з урахуванням нових класів)
        }

        private void LoadPosition_Click(object sender, RoutedEventArgs e)
        {
            // Логіка завантаження позиції (потрібно оновити з урахуванням нових класів)
        }


        private void SwitchPlayer()
        {
            _currentPlayer = _gameLogic.GetCurrentPlayer();
            UpdateStatusText();

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

        private void UpdateStatusText()
        {
            StatusTextBlock.Text = _isComputerMode ? $"Режим проти комп'ютера. Хід {_currentPlayer}." : $"Режим для двох гравців. Хід {_currentPlayer}.";
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

        private Point _dragStartPoint;
        private BoardCell? _draggedCell;

        private void BoardCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border clickedBorder) || !(clickedBorder.DataContext is BoardCell clickedCell))
                return;

            _dragStartPoint = e.GetPosition(null);
            _draggedCell = clickedCell;

            DataObject dragData = new DataObject(typeof(BoardCell), clickedCell);
            DragDrop.DoDragDrop(clickedBorder, dragData, DragDropEffects.Move);
        }

        private void BoardCell_DragEnter(object sender, DragEventArgs e)
        {
            if (!(sender is Border targetBorder) || !(targetBorder.DataContext is BoardCell targetCell) || _draggedCell == null || targetCell == _draggedCell)
                return;

            if (e.Data.GetDataPresent(typeof(BoardCell)))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void BoardCell_Drop(object sender, DragEventArgs e)
        {
            if (!(sender is Border dropBorder) || !(dropBorder.DataContext is BoardCell dropCell) || _draggedCell == null || dropCell == _draggedCell)
                return;

            int startRow = _draggedCell.Row;
            int startCol = _draggedCell.Col;
            int endRow = dropCell.Row;
            int endCol = dropCell.Col;

            if (_gameLogic.TryMovePiece(startRow, startCol, endRow, endCol))
            {
                string moveNotation = GetMoveNotation(_draggedCell, dropCell);
                UpdateMoveHistory(moveNotation);
                UpdateBoardUI();
                SwitchPlayer();
            }

            _draggedCell = null;
        }

        private string GetMoveNotation(BoardCell startCell, BoardCell endCell)
        {
            // Потрібно буде додати більш складну логіку для нотації (враховувати тип фігури, взяття тощо)
            string startSquare = GetSquareNotation(startCell.Col, startCell.Row);
            string endSquare = GetSquareNotation(endCell.Col, endCell.Row);
            return $"{startSquare}-{endSquare}";
        }


        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void DifficultyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (selectedItem.Content.ToString() == "Легкий")
                {
                    _gameLogic.ComputerDifficulty = 1;
                }
                else if (selectedItem.Content.ToString() == "Середній")
                {
                    _gameLogic.ComputerDifficulty = 2;
                }
                else if (selectedItem.Content.ToString() == "Складний")
                {
                    _gameLogic.ComputerDifficulty = 3;
                }
            }
        }
    }
}