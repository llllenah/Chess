using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChessTrainer
{
    /// <summary>
    /// Provides the XAML App class for the Chess Trainer application.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Handles application startup.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SetupGlobalExceptionHandling();
        }

        /// <summary>
        /// Sets up global exception handling for the application.
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            // Handler for unhandled exceptions in the UI thread
            DispatcherUnhandledException += (sender, e) =>
            {
                HandleUnhandledException(e.Exception);
                e.Handled = true;
            };

            // Handler for unhandled exceptions in non-UI threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                {
                    HandleUnhandledException(exception);
                }
            };

            // Handler for unhandled exceptions in tasks
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                HandleUnhandledException(e.Exception);
                e.SetObserved();
            };
        }

        /// <summary>
        /// Handles unhandled exceptions by displaying an error message.
        /// </summary>
        /// <param name="exception">The unhandled exception.</param>
        private void HandleUnhandledException(Exception exception)
        {
            string message = $"An unexpected error occurred:\n{exception.Message}";
            string details = exception.StackTrace?.ToString() ?? "No stack trace available";

            try
            {
                // Create a custom error dialog
                Window errorWindow = new Window
                {
                    Title = "Application Error",
                    Width = 600,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.CanResize
                };

                Grid grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                TextBlock headerBlock = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red
                };
                Grid.SetRow(headerBlock, 0);

                ScrollViewer scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10, 0, 10, 10)
                };
                TextBox detailsBox = new TextBox
                {
                    Text = details,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5)
                };
                scrollViewer.Content = detailsBox;
                Grid.SetRow(scrollViewer, 1);

                Button closeButton = new Button
                {
                    Content = "Close",
                    Width = 100,
                    Height = 30,
                    Margin = new Thickness(0, 10, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                closeButton.Click += (s, e) => errorWindow.Close();
                Grid.SetRow(closeButton, 2);

                grid.Children.Add(headerBlock);
                grid.Children.Add(scrollViewer);
                grid.Children.Add(closeButton);

                errorWindow.Content = grid;
                errorWindow.ShowDialog();
            }
            catch
            {
                // If the custom dialog fails, fall back to a simple message box
                MessageBox.Show(message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}