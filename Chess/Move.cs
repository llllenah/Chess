public class Move
{
    public int StartRow { get; }
    public int StartCol { get; }
    public int EndRow { get; }
    public int EndCol { get; }
    public int Score { get; set; } // Для алгоритму Minimax

    public Move(int startRow, int startCol, int endRow, int endCol)
    {
        StartRow = startRow;
        StartCol = startCol;
        EndRow = endRow;
        EndCol = endCol;
        Score = 0;
    }
}