using System.ComponentModel;
using System.Windows.Media;

public class BoardCell : INotifyPropertyChanged
{
    public int Row { get; }
    public int Col { get; }
    public Brush CellColor { get; }
    public Piece? Piece { get; }
    private bool _isHighlighted;


    public BoardCell(int row, int col, Brush cellColor, Piece? piece)
    {
        Row = row;
        Col = col;
        CellColor = cellColor;
        Piece = piece;
        _isHighlighted = false;
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}