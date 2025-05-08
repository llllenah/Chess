using System;
using System.Windows;
using System.Windows.Controls;

namespace ChessTrainer
{
    public partial class PieceSelectionDialog : Window
    {
        public Piece SelectedPiece { get; private set; }

        public PieceSelectionDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (RadioButton radioButton in LogicalTreeHelper.GetChildren(this).OfType<RadioButton>())
            {
                if (radioButton.IsChecked == true)
                {
                    string[] tagValues = radioButton.Tag.ToString().Split(',');
                    if (tagValues.Length == 2)
                    {
                        SelectedPiece = new Piece(tagValues[0], tagValues[1]);
                        DialogResult = true;
                        return;
                    }
                }
            }
            DialogResult = false; // Якщо жодна кнопка не вибрана
        }
    }
}