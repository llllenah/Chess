using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace ChessTrainer
{
    public class GameLogic
    {
        private Board _board;
        private string _currentPlayer = "white";
        private bool _isComputerMode = false;
        private int _computerDifficulty = 1; // Поки що не використовується

        public GameLogic()
        {
            _board = new Board();
        }


        public ObservableCollection<BoardCell> GetCurrentBoard()
        {
            var boardCells = new ObservableCollection<BoardCell>();
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Piece? piece = _board.GetPiece(row, col);
                    string? pieceSymbol = GetPieceSymbol(piece);
                    string? pieceColor = piece?.Color;
                    boardCells.Add(new BoardCell(row, col, (row + col) % 2 == 0 ? Brushes.LightGray : Brushes.White, pieceSymbol, pieceColor));
                }
            }
            return boardCells;
        }

        public bool TryMovePiece(int startRow, int startCol, int endRow, int endCol)
        {
            if (_board.IsValidMove(startRow, startCol, endRow, endCol, _currentPlayer))
            {
                _board.MovePiece(startRow, startCol, endRow, endCol);
                SwitchPlayer();
                return true;
            }
            return false;
        }

        public string GetCurrentPlayer()
        {
            return _currentPlayer;
        }

        public void SetComputerMode(bool isComputerMode)
        {
            _isComputerMode = isComputerMode;
            _currentPlayer = "white"; // Починає завжди білий гравець
        }

        private void SwitchPlayer()
        {
            _currentPlayer = _currentPlayer == "white" ? "black" : "white";
            if (_isComputerMode && _currentPlayer == "black")
            {
                MakeComputerMove();
            }
        }

        private void MakeComputerMove()
        {
            // ТУТ БУДЕ ЛОГІКА КОМП'ЮТЕРНОГО ГРАВЦЯ
            // Наразі робимо випадковий допустимий хід
            var possibleMoves = GetPossibleMoves("black");
            if (possibleMoves.Any())
            {
                var random = new System.Random();
                var move = possibleMoves[random.Next(possibleMoves.Count)];
                _board.MovePiece(move.StartRow, move.StartCol, move.EndRow, move.EndCol);
                SwitchPlayer();
                // Потрібно оновити UI після ходу комп'ютера
            }
        }

        private List<Move> GetPossibleMoves(string playerColor)
        {
            var moves = new List<Move>();
            for (int startRow = 0; startRow < 8; startRow++)
            {
                for (int startCol = 0; startCol < 8; startCol++)
                {
                    for (int endRow = 0; endRow < 8; endRow++)
                    {
                        for (int endCol = 0; endCol < 8; endCol++)
                        {
                            if (startRow != endRow || startCol != endCol)
                            {
                                if (_board.IsValidMove(startRow, startCol, endRow, endCol, playerColor))
                                {
                                    moves.Add(new Move(startRow, startCol, endRow, endCol));
                                }
                            }
                        }
                    }
                }
            }
            return moves;
        }

        private string? GetPieceSymbol(Piece? piece)
        {
            if (piece == null) return null;
            return piece.Color == "white" ? GetWhiteSymbol(piece.Type) : GetBlackSymbol(piece.Type);
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
    }

    public class Move
    {
        public int StartRow { get; }
        public int StartCol { get; }
        public int EndRow { get; }
        public int EndCol { get; }

        public Move(int startRow, int startCol, int endRow, int endCol)
        {
            StartRow = startRow;
            StartCol = startCol;
            EndRow = endRow;
            EndCol = endCol;
        }
    }
}