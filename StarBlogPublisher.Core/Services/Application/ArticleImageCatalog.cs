using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace StarBlogPublisher.Services.Application;

public sealed record ArticleImageSource(string Source, string Name);

/// <summary>Reads the images actually rendered in the article, including reference and HTML images.</summary>
public static class ArticleImageCatalog {
    public static IReadOnlyList<ArticleImageSource> FromHtml(string html, string? sourceFilePath) {
        var images = new List<ArticleImageSource>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var directory = string.IsNullOrWhiteSpace(sourceFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(sourceFilePath))!;
        html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);
        foreach (Match tag in Regex.Matches(html, @"<img\b(?:[^>""']|""[^""]*""|'[^']*')*>", RegexOptions.IgnoreCase)) {
            var source = Attribute(tag.Value, "src");
            if (string.IsNullOrWhiteSpace(source)) continue;
            try {
                if (source.StartsWith("//", StringComparison.Ordinal)) source = "https:" + source;
                if (!Path.IsPathRooted(source) && Uri.TryCreate(source, UriKind.Absolute, out var uri)) {
                    if (uri.Scheme is "http" or "https") source = uri.AbsoluteUri;
                    else if (uri.IsFile) source = uri.LocalPath;
                    else continue;
                }
                else {
                    source = Path.GetFullPath(Path.Combine(directory,
                        Uri.UnescapeDataString(source).Replace('/', Path.DirectorySeparatorChar)));
                }

                if (!seen.Add(source)) continue;
                var name = Attribute(tag.Value, "alt");
                if (string.IsNullOrWhiteSpace(name)) name = $"正文图片 {images.Count + 1}";
                images.Add(new ArticleImageSource(source, name));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or UriFormatException) {
                // A malformed image source should not prevent choosing the other images.
            }
        }

        return images;
    }

    private static string Attribute(string tag, string name) {
        foreach (Match match in Regex.Matches(tag,
                     @"\s(?<name>[^\s=/>]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))")) {
            if (string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase)) {
                return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
            }
        }
        return "";
    }
}
