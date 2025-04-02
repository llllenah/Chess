using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ChessTrainer
{
    public partial class MainWindow : Window
    {
        private const int CellSize = 60;
        private const int BoardSize = 8;
        private readonly string[,] InitialBoard = {
            { "♜", "♞", "♝", "♛", "♚", "♝", "♞", "♜" },
            { "♟", "♟", "♟", "♟", "♟", "♟", "♟", "♟" },
            { "", "", "", "", "", "", "", "" },
            { "", "", "", "", "", "", "", "" },
            { "", "", "", "", "", "", "", "" },
            { "", "", "", "", "", "", "", "" },
            { "♙", "♙", "♙", "♙", "♙", "♙", "♙", "♙" },
            { "♖", "♘", "♗", "♕", "♔", "♗", "♘", "♖" }
        };

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeSelector.SelectionChanged += OnThemeChanged;
            DrawChessBoard();
            DrawPieces();
            DrawCoordinates();
        }

        private void DrawChessBoard()
        {
            ChessBoardCanvas.Children.Clear();
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = CellSize,
                        Height = CellSize,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1,
                        Fill = (row + col) % 2 == 0 ? lightCellColor : darkCellColor
                    };
                    Canvas.SetLeft(rect, col * CellSize);
                    Canvas.SetTop(rect, row * CellSize);
                    ChessBoardCanvas.Children.Add(rect);
                }
            }
        }

        private void DrawCoordinates()
        {
            string letters = "ABCDEFGH";
            for (int i = 0; i < BoardSize; i++)
            {
                DrawOutlinedText(letters[i].ToString(), i * CellSize + CellSize / 3, BoardSize * CellSize + 5, Brushes.Black, Brushes.White, 1);
                DrawOutlinedText((8 - i).ToString(), -CellSize / 2, i * CellSize + CellSize / 3, Brushes.Black, Brushes.White, 1);
            }
        }

        private void DrawOutlinedText(string text, double x, double y, Brush fill, Brush stroke, double thickness)
        {
            FormattedText formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Symbol"),
                CellSize * 0.7,
                fill,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Geometry textGeometry = formattedText.BuildGeometry(new Point(0, 0));
            Path textPath = new Path
            {
                Data = textGeometry,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = thickness
            };
            Canvas.SetLeft(textPath, x);
            Canvas.SetTop(textPath, y);
            ChessBoardCanvas.Children.Add(textPath);
        }

        private Brush lightCellColor = Brushes.White;
        private Brush darkCellColor = Brushes.Black;

        private void ApplyTheme(string theme)
        {
            switch (theme)
            {
                case "Класична":
                    lightCellColor = Brushes.White;
                    darkCellColor = Brushes.Black;
                    break;
                case "Синя":
                    lightCellColor = Brushes.LightBlue;
                    darkCellColor = Brushes.DarkBlue;
                    break;
                case "Зелена":
                    lightCellColor = Brushes.LightSeaGreen;
                    darkCellColor = Brushes.DarkGreen;
                    break;
            }
            DrawChessBoard();
            DrawPieces();
            DrawCoordinates();
        }

        private void DrawPieces()
        {
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    string piece = InitialBoard[row, col];
                    if (!string.IsNullOrEmpty(piece))
                    {
                        bool isWhitePiece = "♙♖♘♗♕♔".Contains(piece);
                        DrawOutlinedText(piece, col * CellSize + CellSize / 4, row * CellSize + CellSize / 6, isWhitePiece ? Brushes.White : Brushes.Black, Brushes.Black, 0.5);
                    }
                }
            }
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChessBoardCanvas == null)
                return;
            ComboBox combo = sender as ComboBox;
            ComboBoxItem selectedItem = combo?.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                ApplyTheme(selectedItem.Content.ToString());
            }
        }
    }
}
