using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ChessTrainer
{
    public class BoardCell : INotifyPropertyChanged
    {
        private int _row;
        public int Row
        {
            get { return _row; }
            set
            {
                _row = value;
                OnPropertyChanged();
            }
        }

        private int _col;
        public int Col
        {
            get { return _col; }
            set
            {
                _col = value;
                OnPropertyChanged();
            }
        }

        private Brush? _backgroundColor;
        public Brush? BackgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                OnPropertyChanged();
            }
        }

        private string? _piece;
        public string? Piece
        {
            get { return _piece; }
            set
            {
                _piece = value;
                OnPropertyChanged();
            }
        }

        private string? _color;
        public string? Color
        {
            get { return _color; }
            set
            {
                _color = value;
                OnPropertyChanged();
            }
        }

        public BoardCell(int row, int col, Brush? backgroundColor, string? piece, string? color)
        {
            Row = row;
            Col = col;
            BackgroundColor = backgroundColor;
            Piece = piece;
            Color = color;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}