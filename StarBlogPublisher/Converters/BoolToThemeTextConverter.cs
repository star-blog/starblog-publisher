using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace StarBlogPublisher.Converters
{
    public class BoolToThemeTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isDarkTheme)
            {
                return isDarkTheme ? "🌙 深色模式" : "☀️ 浅色模式";
            }
            return "☀️ 浅色模式";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}