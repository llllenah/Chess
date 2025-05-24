using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChessTrainer
{
    /// <summary>
    /// Converts boolean values to Visibility enum values
    /// </summary>

    /// <summary>
    /// Converts boolean values to Visibility enum values
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to Visibility (true = Visible, false = Collapsed)
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <param name="targetType">Target type for conversion</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Visibility.Visible for true, Visibility.Collapsed for false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            // Added fallback for null or non-boolean values
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts from Visibility back to boolean (not implemented)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Converting from Visibility to boolean is not supported");
        }
    }
}