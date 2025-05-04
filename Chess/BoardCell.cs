using System.Windows.Media;

public class BoardCell
{
    public int Row { get; }
    public int Col { get; }
    public Brush CellColor { get; }
    public Piece? Piece { get; }

    public BoardCell(int row, int col, Brush cellColor, Piece? piece)
    {
        Row = row;
        Col = col;
        CellColor = cellColor;
        Piece = piece;
    }
}