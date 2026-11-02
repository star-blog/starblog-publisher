using System.Text.RegularExpressions;

namespace StarBlogPublisher.Utils;

/// <summary>Filters a closed YAML metadata block at the start of a Markdown document.</summary>
public static class MarkdownFrontMatter {
    private static readonly Regex Header = new(
        @"\A\uFEFF?---[ \t]*\r?\n.*?^(?:---|\.\.\.)[ \t]*(?:\r?\n|\z)",
        RegexOptions.Singleline | RegexOptions.Multiline);

    /// <summary>
    /// Removes front matter without changing the source document. Preview and outline callers
    /// can replace it with whitespace to keep source offsets and line numbers intact.
    /// Unclosed headers are left untouched to avoid discarding article content.
    /// </summary>
    public static string Remove(string markdown, bool preserveSourcePositions = false) {
        var header = Header.Match(markdown);
        if (!header.Success) return markdown;
        if (!preserveSourcePositions) return markdown[header.Length..];

        var characters = markdown.ToCharArray();
        for (var index = 0; index < header.Length; index++) {
            if (characters[index] is not '\r' and not '\n') characters[index] = ' ';
        }
        return new string(characters);
    }
}
