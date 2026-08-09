using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HashCompare.Core;

namespace HashCompare.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly IBrush NewBrush = new SolidColorBrush(Color.Parse("#A5D6A7"));       // Light Green
    private static readonly IBrush MissingBrush = new SolidColorBrush(Color.Parse("#EF9A9A"));   // Light Red
    private static readonly IBrush DifferentBrush = new SolidColorBrush(Color.Parse("#FFF59D")); // Light Yellow
    private static readonly IBrush IdenticalBrush = Brushes.White;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            FileStatus.New => NewBrush,
            FileStatus.Missing => MissingBrush,
            FileStatus.Different => DifferentBrush,
            FileStatus.Identical => IdenticalBrush,
            _ => Brushes.Transparent
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}