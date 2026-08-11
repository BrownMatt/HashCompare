using Avalonia.Data.Converters;
using System;
using System.Globalization;
using HashCompare.Core;

namespace HashCompare.App.Views.Converters;

/// <summary>Converts FileStatus enum to Avalonia Brushes for cell background coloring.</summary>
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
        {
            return status switch
            {
                FileStatus.New => (Avalonia.Media.Brush)new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A5D6A7")),
                FileStatus.Missing => (Avalonia.Media.Brush)new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#EF9A9A")),
                FileStatus.Different => (Avalonia.Media.Brush)new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFF59D")),
                FileStatus.Identical => Avalonia.Media.Brushes.White,
                _ => Avalonia.Media.Brushes.White
            };
        }

        return Avalonia.Media.Brushes.White;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}