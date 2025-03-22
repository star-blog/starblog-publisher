using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace StarBlogPublisher.Converters {
    public class BooleanToCharConverter : IValueConverter {
        public static readonly BooleanToCharConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is bool boolValue && parameter is string charStr) {
                return boolValue ? '\0' : charStr[0];
            }

            return '\0';
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}