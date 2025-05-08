using System.ComponentModel;
using System.Windows.Media;

namespace ChessTrainer
{
    /// <summary>
    /// Represents a cell on the chess board in the UI
    /// </summary>
    public class BoardCell : INotifyPropertyChanged
    {
        #region Fields

        private bool _isHighlighted;
        private Piece? _piece;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the row index (0-7)
        /// </summary>
        public int Row { get; }

        /// <summary>
        /// Gets the column index (0-7)
        /// </summary>
        public int Col { get; }

        /// <summary>
        /// Gets the background brush for this cell
        /// </summary>
        public Brush Background { get; }

        /// <summary>
        /// Gets or sets whether this cell is highlighted
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged(nameof(IsHighlighted));
                }
            }
        }

        /// <summary>
        /// Gets or sets the chess piece on this cell
        /// </summary>
        public Piece? Piece
        {
            get => _piece;
            set
            {
                if (_piece != value)
                {
                    _piece = value;
                    OnPropertyChanged(nameof(Piece));
                }
            }
        }

        #endregion

        #region Event

        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new board cell
        /// </summary>
        /// <param name="row">Row index (0-7)</param>
        /// <param name="col">Column index (0-7)</param>
        /// <param name="background">Background brush color</param>
        /// <param name="piece">Chess piece on this cell (or null)</param>
        public BoardCell(int row, int col, Brush background, Piece? piece)
        {
            Row = row;
            Col = col;
            Background = background;
            _piece = piece;
            _isHighlighted = false;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Gets the algebraic notation for this cell (e.g., "e4")
        /// </summary>
        /// <returns>Algebraic notation string</returns>
        public string GetAlgebraicNotation()
        {
            return $"{(char)('a' + Col)}{8 - Row}";
        }

        /// <summary>
        /// Creates a clone of this board cell
        /// </summary>
        /// <returns>A new BoardCell with the same properties</returns>
        public BoardCell Clone()
        {
            return new BoardCell(Row, Col, Background, Piece?.Clone());
        }

        /// <summary>
        /// Returns a string representation of this cell
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"{GetAlgebraicNotation()}: {(Piece != null ? Piece.ToString() : "empty")}";
        }

        #endregion
    }
}