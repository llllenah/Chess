﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChessTrainer
{
    /// <summary>
    /// Interaction logic for PawnPromotionDialog.xaml
    /// </summary>
    public partial class PawnPromotionDialog : Window
    {
        /// <summary>
        /// Gets the selected piece type for promotion
        /// </summary>
        public string SelectedPieceType { get; private set; }

        /// <summary>
        /// Gets the color of the pawn being promoted
        /// </summary>
        private string PawnColor { get; }

        /// <summary>
        /// Creates a new pawn promotion dialog
        /// </summary>
        /// <param name="pawnColor">Color of the pawn to promote ("white" or "black")</param>
        public PawnPromotionDialog(string pawnColor)
        {
            InitializeComponent();
            PawnColor = pawnColor;
            SelectedPieceType = "queen";

            Title = $"{(pawnColor == "white" ? "White" : "Black")} pawn has reached the last rank";

            InitializePromotionOptions();

            Width = 600;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        /// <summary>
        /// Initializes the promotion options with piece icons
        /// </summary>
        private void InitializePromotionOptions()
        {
            FontFamily chessFontFamily = new FontFamily("Segoe UI Symbol");

            WrapPanel optionsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            AddPieceOption(optionsPanel, "queen", chessFontFamily);
            AddPieceOption(optionsPanel, "rook", chessFontFamily);
            AddPieceOption(optionsPanel, "bishop", chessFontFamily);
            AddPieceOption(optionsPanel, "knight", chessFontFamily);

            Content = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "Select a piece for promotion:",
                        Margin = new Thickness(10),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top,
                        FontSize = 16
                    },
                    optionsPanel
                }
            };

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 10)
            };

            Button okButton = new Button
            {
                Content = "OK",
                Padding = new Thickness(20, 5, 20, 5),
                Margin = new Thickness(5),
                IsDefault = true,
                MinWidth = 80
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(20, 5, 20, 5),
                Margin = new Thickness(5),
                IsCancel = true,
                MinWidth = 80
            };
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            ((Grid)Content).Children.Add(buttonPanel);

            ((Grid)Content).RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ((Grid)Content).RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            ((Grid)Content).RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            ((Grid)Content).Children[0].SetValue(Grid.RowProperty, 0);
            optionsPanel.SetValue(Grid.RowProperty, 1);
            buttonPanel.SetValue(Grid.RowProperty, 2);
        }

        /// <summary>
        /// Adds a piece option to the options panel
        /// </summary>
        /// <param name="panel">The panel to add the option to</param>
        /// <param name="pieceType">The type of piece</param>
        /// <param name="fontFamily">The font family for the piece icon</param>
        private void AddPieceOption(WrapPanel panel, string pieceType, FontFamily fontFamily)
        {
            Piece piece = new Piece(PawnColor, pieceType);

            string pieceName = GetPieceLocalizedName(pieceType);

            Border container = new Border
            {
                BorderBrush = pieceType == "queen" ? Brushes.Green : Brushes.Transparent,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(10),
                Padding = new Thickness(5),
                Background = new SolidColorBrush(Color.FromArgb(20, 0, 100, 0))
            };

            RadioButton option = new RadioButton
            {
                Tag = pieceType,
                GroupName = "PromotionOptions",
                IsChecked = pieceType == "queen",
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };

            StackPanel contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock pieceIcon = new TextBlock
            {
                Text = piece.GetUnicodeSymbol(),
                FontFamily = fontFamily,
                FontSize = 48,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock pieceNameText = new TextBlock
            {
                Text = pieceName,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            contentPanel.Children.Add(pieceIcon);
            contentPanel.Children.Add(pieceNameText);
            option.Content = contentPanel;
            container.Child = option;

            option.Checked += Option_Checked;

            panel.Children.Add(container);
        }

        /// <summary>
        /// Returns localized piece name in English
        /// </summary>
        private string GetPieceLocalizedName(string pieceType)
        {
            return pieceType switch
            {
                "queen" => "Queen",
                "rook" => "Rook",
                "bishop" => "Bishop",
                "knight" => "Knight",
                _ => pieceType
            };
        }

        /// <summary>
        /// Handles the Checked event for promotion options
        /// </summary>
        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string pieceType)
            {
                SelectedPieceType = pieceType;

                foreach (var child in ((WrapPanel)((Grid)Content).Children[1]).Children)
                {
                    if (child is Border border)
                    {
                        if (border.Child is RadioButton radioButton)
                        {
                            border.BorderBrush = radioButton.IsChecked == true ?
                                Brushes.Green : Brushes.Transparent;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the OK button click
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}