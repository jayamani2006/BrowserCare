using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BrowserCare.Core;

namespace BrowserCare
{
    /// <summary>
    /// Binds a SafetyLevel directly to its status color (green/yellow/red)
    /// so Safe/Review/Protected are readable at a glance, not just as text.
    /// </summary>
    public sealed class SafetyToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not SafetyLevel level || Application.Current == null)
                return Brushes.Gray;

            var key = level switch
            {
                SafetyLevel.Safe => "SafeGreen",
                SafetyLevel.Review => "ReviewYellow",
                SafetyLevel.Protected => "ProtectedRed",
                _ => "TextSecondary"
            };
            return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
