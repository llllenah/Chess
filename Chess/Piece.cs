public class Piece
{
    public string Color { get; }
    public string Type { get; }

    public Piece(string color, string type)
    {
        Color = color.ToLower(); // "white" або "black"
        Type = type.ToLower();  // Наприклад, "pawn", "rook"
    }

    public Piece Clone()
    {
        return new Piece(this.Color, this.Type);
    }
}