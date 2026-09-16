using System;

namespace StarBlogPublisher.Utils;

public static class StringExtensions
{
    /// <summary>
    /// Returns the original string when it fits within <paramref name="length"/>,
    /// otherwise returns its leading characters.
    /// </summary>
    public static string Limit(this string value, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        return value.Length <= length ? value : value[..length];
    }
}
