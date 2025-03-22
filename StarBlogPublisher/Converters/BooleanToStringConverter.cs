using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace StarBlogPublisher.Converters {
    public class BooleanToStringConverter : IValueConverter {
        public static readonly BooleanToStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is bool boolValue && parameter is string str) {
                return boolValue ? "隐藏" : str;
            }

            return "隐藏";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}