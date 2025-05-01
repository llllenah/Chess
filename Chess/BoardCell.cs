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

        private Piece? _piece;
        public Piece? Piece
        {
            get { return _piece; }
            set
            {
                _piece = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PieceSymbol));
            }
        }

        public string? Color
        {
            get { return Piece?.Color; }
        }

        public string? PieceSymbol
        {
            get
            {
                if (Piece == null) return null;
                return Piece.Color == "white" ? GetWhiteSymbol(Piece.Type) : GetBlackSymbol(Piece.Type);
            }
        }

        public BoardCell(int row, int col, Brush? backgroundColor, Piece? piece)
        {
            Row = row;
            Col = col;
            BackgroundColor = backgroundColor;
            Piece = piece;
        }

        private string GetWhiteSymbol(string type)
        {
            return type switch
            {
                "pawn" => "♙",
                "rook" => "♖",
                "knight" => "♘",
                "bishop" => "♗",
                "queen" => "♕",
                "king" => "♔",
                _ => ""
            };
        }

        private string GetBlackSymbol(string type)
        {
            return type switch
            {
                "pawn" => "♟",
                "rook" => "♜",
                "knight" => "♞",
                "bishop" => "♝",
                "queen" => "♛",
                "king" => "♚",
                _ => ""
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}