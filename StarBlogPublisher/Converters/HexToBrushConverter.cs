using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StarBlogPublisher.Converters;

public sealed class HexToBrushConverter : IValueConverter {
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) {
            return Brushes.Transparent;
        }

        try {
            return new SolidColorBrush(Color.Parse(hex));
        } catch {
            return Brushes.Transparent;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
