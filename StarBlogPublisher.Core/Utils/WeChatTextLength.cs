using System;
using System.Globalization;

namespace StarBlogPublisher.Utils;

/// <summary>Counts WeChat title/digest length in half-word units, without normalizing the text.</summary>
public static class WeChatTextLength {
    /// <summary>
    /// Latin-1 characters count as half a word, other UTF-16 code units as one word.
    /// A supplementary-plane character therefore counts as two words.
    /// </summary>
    public static decimal GetLength(string? value) => GetHalfUnits(value.AsSpan()) / 2m;

    /// <summary>Returns the longest prefix within the word limit, keeping text elements intact.</summary>
    public static string Truncate(string? value, int maxLength) {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var maxUnits = (long)maxLength * 2;
        long units = 0;
        var end = 0;
        while (end < value.Length) {
            var elementLength = StringInfo.GetNextTextElementLength(value.AsSpan(end));
            var elementUnits = GetHalfUnits(value.AsSpan(end, elementLength));
            if (units + elementUnits > maxUnits) break;

            units += elementUnits;
            end += elementLength;
        }

        return end == value.Length ? value : value[..end];
    }

    private static long GetHalfUnits(ReadOnlySpan<char> value) {
        long units = 0;
        foreach (var character in value) {
            units += character <= '\u00FF' ? 1 : 2;
        }
        return units;
    }
}
