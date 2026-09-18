using System;
using System.Collections.Generic;

namespace StarBlogPublisher.Utils;

public enum MarkdownHighlightKind {
    Heading,
    Bold,
    InlineCode,
    FencedCode,
    Link,
    Quote
}

public readonly record struct MarkdownHighlightSpan(int Start, int Length, MarkdownHighlightKind Kind);

public static class MarkdownHighlightTokenizer {
    public static bool IsFenceDelimiter(string line) {
        var trimmed = line.AsSpan().TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
               || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    public static IReadOnlyList<MarkdownHighlightSpan> Tokenize(string line, bool inFencedCode) {
        if (string.IsNullOrEmpty(line)) {
            return [];
        }

        if (inFencedCode || IsFenceDelimiter(line)) {
            return [new MarkdownHighlightSpan(0, line.Length, MarkdownHighlightKind.FencedCode)];
        }

        if (IsHeading(line)) {
            return [new MarkdownHighlightSpan(0, line.Length, MarkdownHighlightKind.Heading)];
        }

        if (IsQuote(line)) {
            return [new MarkdownHighlightSpan(0, line.Length, MarkdownHighlightKind.Quote)];
        }

        return CollectInlineSpans(line);
    }

    private static bool IsHeading(string line) {
        var hashes = 0;
        while (hashes < line.Length && line[hashes] == '#' && hashes < 6) {
            hashes++;
        }

        return hashes > 0 && hashes < line.Length && line[hashes] == ' ';
    }

    private static bool IsQuote(string line) {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) {
            i++;
        }

        return i < line.Length && line[i] == '>';
    }

    private static List<MarkdownHighlightSpan> CollectInlineSpans(string line) {
        var spans = new List<MarkdownHighlightSpan>();
        var i = 0;
        while (i < line.Length) {
            if (line[i] == '`') {
                var end = line.IndexOf('`', i + 1);
                if (end > i) {
                    spans.Add(new MarkdownHighlightSpan(i, end - i + 1, MarkdownHighlightKind.InlineCode));
                    i = end + 1;
                    continue;
                }
            }

            if (i + 1 < line.Length && line[i] == '*' && line[i + 1] == '*') {
                var end = line.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end >= 0) {
                    spans.Add(new MarkdownHighlightSpan(i, end + 2 - i, MarkdownHighlightKind.Bold));
                    i = end + 2;
                    continue;
                }
            }

            if (TryParseLink(line, i, out var linkLength)) {
                spans.Add(new MarkdownHighlightSpan(i, linkLength, MarkdownHighlightKind.Link));
                i += linkLength;
                continue;
            }

            i++;
        }

        return spans;
    }

    private static bool TryParseLink(string line, int start, out int length) {
        length = 0;
        var i = start;
        if (i < line.Length && line[i] == '!') {
            i++;
        }

        if (i >= line.Length || line[i] != '[') {
            return false;
        }

        var closeBracket = line.IndexOf(']', i + 1);
        if (closeBracket < 0 || closeBracket + 1 >= line.Length || line[closeBracket + 1] != '(') {
            return false;
        }

        var closeParen = line.IndexOf(')', closeBracket + 2);
        if (closeParen < 0) {
            return false;
        }

        length = closeParen - start + 1;
        return length > 0;
    }
}
