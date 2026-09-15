using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 将 Markdown 转为适合粘贴或上传到微信公众号的内联样式 HTML。
/// </summary>
public sealed class WeChatFormattingService {
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static IReadOnlyList<WeChatTheme> Themes { get; } = new[] {
        new WeChatTheme("newspaper", "报刊", "#202020", "#B48A52", "#FFFDF8", "#333333"),
        new WeChatTheme("warm-card", "暖色卡片", "#9A5A44", "#D99A6C", "#FFF8F2", "#4A332B"),
        new WeChatTheme("ocean-card", "海洋卡片", "#176B87", "#64B6D6", "#F4FBFD", "#1F3D4A"),
        new WeChatTheme("tech", "科技简报", "#2563EB", "#38BDF8", "#F8FAFC", "#1E293B")
    };

    public WeChatFormatResult Format(string markdown, string fallbackTitle, string? themeId = null) {
        if (string.IsNullOrWhiteSpace(markdown)) {
            throw new ArgumentException("文章内容为空", nameof(markdown));
        }

        var theme = Themes.FirstOrDefault(item => item.Id == themeId) ?? Themes[0];
        var title = ExtractTitle(markdown, fallbackTitle);
        var source = PrepareMarkdown(markdown);
        var html = Markdown.ToHtml(source, Pipeline);

        return new WeChatFormatResult {
            Html = ApplyWeChatStyles(html, theme),
            Title = title,
            WordCount = CountWords(source),
            Theme = theme
        };
    }

    private static string PrepareMarkdown(string markdown) {
        var source = Regex.Replace(markdown, @"\A---\s*\r?\n.*?\r?\n---\s*\r?\n", string.Empty, RegexOptions.Singleline);
        return FixCjkSpacing(source);
    }

    private static string ExtractTitle(string markdown, string fallbackTitle) {
        var frontMatterTitle = Regex.Match(markdown, @"\A---\s*\r?\n.*?^title:\s*[""']?(?<title>.+?)[""']?\s*$.*?\r?\n---", RegexOptions.Singleline | RegexOptions.Multiline);
        if (frontMatterTitle.Success) return frontMatterTitle.Groups["title"].Value.Trim();

        var h1 = Regex.Match(markdown, @"^#\s+(?<title>.+)$", RegexOptions.Multiline);
        return h1.Success ? h1.Groups["title"].Value.Trim() : fallbackTitle.Trim();
    }

    private static int CountWords(string markdown) {
        var chineseCharacters = Regex.Matches(markdown, "[一-鿿]").Count;
        var latinWords = Regex.Matches(markdown, "[A-Za-z0-9]+", RegexOptions.CultureInvariant).Count;
        return chineseCharacters + latinWords;
    }

    private static string FixCjkSpacing(string markdown) {
        var protectedValues = new List<string>();
        string Protect(Match match) {
            protectedValues.Add(match.Value);
            return $"\u0000P{protectedValues.Count - 1}\u0000";
        }

        var result = Regex.Replace(markdown, @"`[^`]+`|https?://\S+|!?\[[^\]]*\]\([^)]*\)", Protect);
        result = Regex.Replace(result, "([一-鿿])([A-Za-z0-9])", "$1 $2");
        result = Regex.Replace(result, "([A-Za-z0-9])([一-鿿])", "$1 $2");
        for (var index = 0; index < protectedValues.Count; index++) {
            result = result.Replace($"\u0000P{index}\u0000", protectedValues[index], StringComparison.Ordinal);
        }

        return result;
    }

    private static string ApplyWeChatStyles(string html, WeChatTheme theme) {
        html = StyleTag(html, "h1", $"font-size:26px;line-height:1.45;color:{theme.PrimaryColor};font-weight:700;margin:28px 0 18px;text-align:center");
        html = StyleTag(html, "h2", $"font-size:22px;line-height:1.5;color:{theme.PrimaryColor};font-weight:700;border-left:4px solid {theme.AccentColor};padding-left:10px;margin:28px 0 16px");
        html = StyleTag(html, "h3", $"font-size:19px;line-height:1.5;color:{theme.PrimaryColor};font-weight:700;margin:24px 0 12px");
        html = StyleTag(html, "p", $"font-size:16px;line-height:1.85;color:{theme.TextColor};letter-spacing:0.5px;margin:0 0 16px;text-align:justify");
        html = StyleTag(html, "blockquote", $"margin:20px 0;padding:14px 16px;border-left:4px solid {theme.AccentColor};background:{theme.BackgroundColor};color:{theme.TextColor}");
        html = StyleTag(html, "a", $"color:{theme.PrimaryColor};text-decoration:none;border-bottom:1px solid {theme.AccentColor}");
        html = StyleTag(html, "strong", $"color:{theme.PrimaryColor};font-weight:700");
        html = StyleTag(html, "ul", "padding-left:24px;margin:0 0 16px");
        html = StyleTag(html, "ol", "padding-left:24px;margin:0 0 16px");
        html = StyleTag(html, "li", $"font-size:16px;line-height:1.8;color:{theme.TextColor};margin:6px 0");
        html = StyleTag(html, "table", "width:100%;border-collapse:collapse;margin:20px 0;font-size:14px");
        html = StyleTag(html, "th", $"padding:9px;border:1px solid {theme.AccentColor};background:{theme.PrimaryColor};color:#FFFFFF;text-align:left");
        html = StyleTag(html, "td", "padding:9px;border:1px solid #E5E7EB;color:#374151");
        html = StyleTag(html, "img", "display:block;max-width:100%;height:auto;margin:18px auto;border-radius:4px");
        html = StyleTag(html, "hr", $"border:0;border-top:1px solid {theme.AccentColor};margin:28px 0");
        html = Regex.Replace(html, @"<pre(?<attributes>[^>]*)>(?<content>.*?)</pre>", match =>
            $"<section style=\"margin:20px 0;border-radius:6px;overflow:hidden;background:#1E293B\"><pre{MergeStyle(match.Groups["attributes"].Value, "margin:0;padding:16px;overflow:auto;line-height:1.65;background:#1E293B;color:#E2E8F0;font-size:13px")}>{match.Groups["content"].Value}</pre></section>", RegexOptions.Singleline);
        html = StyleTag(html, "code", "font-family:Consolas,Menlo,monospace;font-size:0.9em;background:#F1F5F9;padding:2px 4px;border-radius:3px;color:#C2410C");

        return $"<section style=\"max-width:677px;margin:0 auto;padding:8px 16px;background:#FFFFFF;box-sizing:border-box\">{html}</section>";
    }

    private static string StyleTag(string html, string tagName, string style) {
        return Regex.Replace(html, $@"<{tagName}(?<attributes>\s[^>]*)?>", match =>
            $"<{tagName}{MergeStyle(match.Groups["attributes"].Value, style)}>", RegexOptions.IgnoreCase);
    }

    private static string MergeStyle(string attributes, string style) {
        var isSelfClosing = Regex.IsMatch(attributes, @"/\s*$");
        if (isSelfClosing) {
            attributes = Regex.Replace(attributes, @"/\s*$", string.Empty);
        }

        var existingStyle = Regex.Match(attributes, @"\sstyle\s*=\s*""(?<style>[^""]*)""", RegexOptions.IgnoreCase);
        if (existingStyle.Success) {
            var mergedStyle = $"{existingStyle.Groups["style"].Value.TrimEnd(';')};{style}";
            var mergedAttributes = Regex.Replace(attributes, @"\sstyle\s*=\s*""[^""]*""", $" style=\"{mergedStyle}\"", RegexOptions.IgnoreCase);
            return isSelfClosing ? $"{mergedAttributes} /" : mergedAttributes;
        }

        return isSelfClosing ? $"{attributes} style=\"{style}\" /" : $"{attributes} style=\"{style}\"";
    }
}
