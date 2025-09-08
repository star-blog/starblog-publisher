using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace StarBlogPublisher.Converters {
    /// <summary>
    /// 布尔值转换器，根据布尔值选择不同的值
    /// 参数格式："TrueValue|FalseValue"
    /// </summary>
    public class BooleanToValueConverter : IValueConverter {
        public static readonly BooleanToValueConverter Instance = new();

        /// <summary>
        /// 将布尔值转换为指定的值
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数，格式为 "TrueValue|FalseValue"</param>
        /// <param name="culture">文化信息</param>
        /// <returns>根据布尔值返回对应的值</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is bool boolValue && parameter is string paramStr) {
                var parts = paramStr.Split('|');
                if (parts.Length == 2) {
                    return boolValue ? parts[0] : parts[1];
                }
            }

            return null;
        }

        /// <summary>
        /// 反向转换（未实现）
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}