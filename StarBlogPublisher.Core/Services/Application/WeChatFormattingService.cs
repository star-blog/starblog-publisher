using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdown.ColorCode;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 将 Markdown 转为适合粘贴或上传到微信公众号的内联样式 HTML。
/// </summary>
public sealed class WeChatFormattingService {
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseColorCode(HtmlFormatterType.Style)
        .Build();

    public static IReadOnlyList<WeChatTheme> Themes => WeChatThemeCatalog.All;

    public WeChatFormatResult Format(string markdown, string fallbackTitle, string? themeId = null, bool convertExternalLinksToReferences = true) {
        if (string.IsNullOrWhiteSpace(markdown)) {
            throw new ArgumentException("文章内容为空", nameof(markdown));
        }

        var theme = WeChatThemeCatalog.Resolve(themeId);
        var title = ExtractTitle(markdown, fallbackTitle);
        var source = PrepareMarkdown(markdown);
        var html = Markdig.Markdown.ToHtml(source, Pipeline);

        return new WeChatFormatResult {
            Html = ApplyWeChatStyles(html, theme, convertExternalLinksToReferences),
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

        // Fenced blocks are source code, not prose. Protect them before applying CJK spacing
        // so that string literals, comments, and identifiers remain byte-for-byte unchanged.
        var result = Regex.Replace(markdown, @"(?ms)^(?<fence>`{3,}|~{3,})[^\r\n]*\r?\n.*?^\k<fence>[ \t]*\r?$|`[^`\r\n]+`|https?://\S+|!?\[[^\]]*\]\([^)]*\)", Protect);
        result = Regex.Replace(result, "([一-鿿])([A-Za-z0-9])", "$1 $2");
        result = Regex.Replace(result, "([A-Za-z0-9])([一-鿿])", "$1 $2");
        for (var index = 0; index < protectedValues.Count; index++) {
            result = result.Replace($"\u0000P{index}\u0000", protectedValues[index], StringComparison.Ordinal);
        }

        return result;
    }

    private static string ApplyWeChatStyles(string html, WeChatTheme theme, bool convertExternalLinksToReferences) {
        if (convertExternalLinksToReferences) html = ConvertExternalLinksToReferences(html, theme);
        html = ConvertLists(html, theme);
        html = StyleTag(html, "h1", StyleOf(theme, "h1", $"font-size:26px;line-height:1.45;color:{theme.PrimaryColor};font-weight:700;margin:28px 0 18px;text-align:center"));
        html = StyleTag(html, "h2", StyleOf(theme, "h2", $"font-size:22px;line-height:1.5;color:{theme.PrimaryColor};font-weight:700;border-left:4px solid {theme.AccentColor};padding-left:10px;margin:28px 0 16px"));
        html = StyleTag(html, "h3", StyleOf(theme, "h3", $"font-size:19px;line-height:1.5;color:{theme.PrimaryColor};font-weight:700;margin:24px 0 12px"));
        html = StyleTag(html, "h4", StyleOf(theme, "h4", $"font-size:17px;line-height:1.45;color:{theme.PrimaryColor};font-weight:700;margin:20px 0 10px"));
        html = StyleTag(html, "h5", StyleOf(theme, "h5", $"font-size:16px;line-height:1.45;color:{theme.TextColor};font-weight:700;margin:18px 0 8px"));
        html = StyleTag(html, "h6", StyleOf(theme, "h6", $"font-size:15px;line-height:1.45;color:{theme.TextColor};font-weight:700;margin:16px 0 8px"));
        html = StyleTag(html, "p", StyleOf(theme, "p", $"font-size:16px;line-height:1.85;color:{theme.TextColor};letter-spacing:0.5px;margin:0 0 16px;text-align:justify"));
        html = StyleTag(html, "blockquote", StyleOf(theme, "blockquote", $"margin:20px 0;padding:14px 16px;border-left:4px solid {theme.AccentColor};background:{theme.BackgroundColor};color:{theme.TextColor}"));
        html = StyleBlockquoteParagraphs(html, theme);
        html = StyleTag(html, "a", StyleOf(theme, "a", $"color:{theme.PrimaryColor};text-decoration:none;border-bottom:1px solid {theme.AccentColor}"));
        html = StyleTag(html, "strong", StyleOf(theme, "strong", $"color:{theme.PrimaryColor};font-weight:700"));
        html = StyleTag(html, "em", StyleOf(theme, "em", "font-style:italic"));
        html = StyleTag(html, "ul", StyleOf(theme, "ul", "padding-left:24px;margin:0 0 16px"));
        html = StyleTag(html, "ol", StyleOf(theme, "ol", "padding-left:24px;margin:0 0 16px"));
        html = StyleTag(html, "li", StyleOf(theme, "li", $"font-size:16px;line-height:1.8;color:{theme.TextColor};margin:6px 0"));
        html = StyleTag(html, "table", StyleOf(theme, "table", "width:100%;border-collapse:collapse;margin:20px 0;font-size:14px"));
        html = StyleTag(html, "th", StyleOf(theme, "th", $"padding:9px;border:1px solid {theme.AccentColor};background:{theme.PrimaryColor};color:#FFFFFF;text-align:left"));
        html = StyleTag(html, "td", StyleOf(theme, "td", "padding:9px;border:1px solid #E5E7EB;color:#374151"));
        html = StyleTag(html, "img", AppendWidth(StyleOf(theme, "img", "display:block;max-width:100%;height:auto;margin:18px auto;border-radius:4px")));
        html = StyleTag(html, "hr", StyleOf(theme, "hr", $"border:0;border-top:1px solid {theme.AccentColor};margin:28px 0"));
        html = StyleCodeBlocks(html, theme);
        html = StyleInlineCode(html, theme);
        html = WrapCards(html, theme);

        var wrapper = theme.Card == null
            ? "max-width:677px;margin:0 auto;box-sizing:border-box;" + StyleOf(theme, "wrapper", "padding:8px 16px;background:#FFFFFF")
            : $"max-width:677px;margin:0 auto;box-sizing:border-box;padding:40px 10px;background-color:{theme.Card.Background};display:flex;flex-direction:column;align-items:center;gap:24px";
        return $"<section style=\"{wrapper}\">{html}</section>";
    }

    private static string ConvertExternalLinksToReferences(string html, WeChatTheme theme) {
        var references = new List<(string Title, string Url)>();
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var linkStyle = StyleOf(theme, "a", $"color:{theme.PrimaryColor};text-decoration:none;border-bottom:1px solid {theme.AccentColor}");
        var superscriptStyle = StyleOf(theme, "footnote_sup", $"font-size:12px;color:{theme.PrimaryColor};vertical-align:super;margin-left:2px");

        // Work on rendered anchors so inline, reference-style, automatic and raw HTML links
        // share the same numbering. Skip code and comments, including raw HTML code samples.
        html = Regex.Replace(html,
            """<!--.*?-->|<(?<protected>pre|code|script|style)\b(?:"[^"]*"|'[^']*'|[^'"<>])*?>.*?</\k<protected>\s*>|<a\b(?<attributes>(?:"[^"]*"|'[^']*'|[^'"<>])*)>(?<content>.*?)</a\s*>""",
            match => {
                if (!match.Groups["attributes"].Success) return match.Value;
                var attributes = match.Groups["attributes"].Value;
                var href = FindHtmlAttribute(attributes, "href");
                if (href == null) return match.Value;
                var url = WebUtility.HtmlDecode(href.Groups["value"].Value).Trim();
                var absoluteUrl = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
                if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                    uri.Host.Equals("mp.weixin.qq.com", StringComparison.OrdinalIgnoreCase)) {
                    return match.Value;
                }

                var content = match.Groups["content"].Value;
                if (!numbers.TryGetValue(url, out var number)) {
                    var title = Regex.Replace(content, """<img\b(?<attributes>(?:"[^"]*"|'[^']*'|[^'"<>])*)>""", image =>
                        FindHtmlAttribute(image.Groups["attributes"].Value, "alt")?.Groups["value"].Value ?? string.Empty,
                        RegexOptions.IgnoreCase);
                    title = WebUtility.HtmlDecode(Regex.Replace(title, @"<[^>]*>", string.Empty));
                    title = Regex.Replace(title, @"\s+", " ").Trim();
                    number = references.Count + 1;
                    references.Add((string.IsNullOrWhiteSpace(title) ? url : title, url));
                    numbers.Add(url, number);
                }

                // Use visible text for both the marker and URL; neither depends on an href
                // surviving WeChat's editor. Retain the link's inline formatting and images.
                return $"<span{MergeStyle(attributes.Remove(href.Index, href.Length), linkStyle)}>{content}</span><sup style=\"{superscriptStyle}\">[{number}]</sup>";
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (references.Count == 0) return html;

        var sectionStyle = StyleOf(theme, "footnote_section", "margin-top:32px;padding-top:16px;border-top:1px solid #E5E7EB");
        var titleStyle = StyleOf(theme, "footnote_title", $"font-size:16px;font-weight:bold;color:{theme.PrimaryColor};margin-bottom:12px");
        var itemStyle = StyleOf(theme, "footnote_item", $"font-size:14px;color:{theme.TextColor};margin-bottom:6px;line-height:1.6;word-break:break-all");
        var output = new StringBuilder(html);
        output.Append($"<section style=\"{sectionStyle}\"><section style=\"{titleStyle}\">引用链接</section>");
        for (var index = 0; index < references.Count; index++) {
            var reference = references[index];
            output.Append($"<section style=\"{itemStyle}\">[{index + 1}] {WebUtility.HtmlEncode(reference.Title)}：<span style=\"font-style:italic;word-break:break-all;overflow-wrap:anywhere\">{WebUtility.HtmlEncode(reference.Url)}</span></section>");
        }
        return output.Append("</section>").ToString();
    }

    private static Match? FindHtmlAttribute(string attributes, string name) {
        foreach (Match match in Regex.Matches(attributes,
                     """(?<name>[^\s=/>]+)(?:\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s"'=<>`]+)))?""")) {
            if (match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase)) return match;
        }
        return null;
    }

    private static string StyleCodeBlocks(string html, WeChatTheme theme) {
        var containerStyle = StyleOf(theme, "code_block", "margin:20px 0;border-radius:6px;overflow:hidden;background:#1E293B");
        var preStyle = StyleOf(theme, "pre", "margin:0;padding:16px;overflow:auto;line-height:1.65;background:#1E293B;color:#E2E8F0;font-size:13px;font-family:Consolas,Menlo,monospace");
        var headerStyle = StyleOf(theme, "code_header", string.Empty);
        var headerHtml = string.IsNullOrWhiteSpace(headerStyle)
            ? string.Empty
            // Empty decoration spans can disappear when WeChat imports draft HTML.
            // Visible glyphs do not depend on CSS backgrounds for their presence.
            : $"<section style=\"{headerStyle}\"><span style=\"color:#FF5F56;font-size:18px;line-height:1;margin-right:6px\">●</span><span style=\"color:#FFBD2E;font-size:18px;line-height:1;margin-right:6px\">●</span><span style=\"color:#27C93F;font-size:18px;line-height:1;margin-right:6px\">●</span></section>";

        // Markdown.ColorCode emits a div around its pre element. Collapse that wrapper into
        // our existing WeChat container so the formatter's default background and padding
        // do not create a second card around the code sample.
        // The alternative matches ordinary Markdig output as the fallback, including
        // languages that ColorCode does not recognize. Keeping both alternatives in a
        // single replacement prevents the generated pre element from being wrapped twice.
        return Regex.Replace(html, @"<div(?<divAttributes>[^>]*)>\s*<pre(?<attributes>[^>]*)>(?<content>.*?)</pre>\s*</div>|<pre(?<fallbackAttributes>[^>]*)>(?<fallbackContent>.*?)</pre>", match => {
            var attributes = match.Groups["attributes"].Success
                ? match.Groups["attributes"].Value
                : match.Groups["fallbackAttributes"].Value;
            var content = match.Groups["content"].Success
                ? match.Groups["content"].Value
                : match.Groups["fallbackContent"].Value;
            // The WeChat editor normalizes text nodes in <pre> as ordinary HTML whitespace.
            // Explicit breaks and nonbreaking spaces survive that normalization, while the
            // same fragment still renders correctly in the local preview.
            var codeStyle = $"{preStyle.TrimEnd(';')};white-space:normal";
            return $"<section style=\"{containerStyle}\">{headerHtml}<pre{MergeStyle(attributes, codeStyle)}>{PreserveCodeWhitespace(content)}</pre></section>";
        }, RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string PreserveCodeWhitespace(string content) {
        // ColorCode can place syntax spans across lines. Transform only text nodes so
        // highlighted markup and escaped code characters remain intact.
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
        return Regex.Replace(normalized, @"<[^>]*>|[^<]+", match => {
            if (match.Value[0] == '<') return match.Value;
            return match.Value.Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;", StringComparison.Ordinal)
                .Replace(" ", "&nbsp;", StringComparison.Ordinal)
                .Replace("\n", "<br/>", StringComparison.Ordinal);
        });
    }

    private static string StyleInlineCode(string html, WeChatTheme theme) {
        var inlineCodeStyle = StyleOf(theme, "code", "font-family:Consolas,Menlo,monospace;font-size:0.9em;background:#F1F5F9;padding:2px 4px;border-radius:3px;color:#C2410C");

        // Match complete pre regions first so only inline code receives the light code-chip
        // style; ColorCode's block-level <code> and colour spans are left intact.
        return Regex.Replace(html, @"<pre\b[^>]*>.*?</pre>|<code(?<attributes>\s[^>]*)?>", match => {
            if (match.Value.StartsWith("<pre", StringComparison.OrdinalIgnoreCase)) return match.Value;
            return $"<code{MergeStyle(match.Groups["attributes"].Value, inlineCodeStyle)}>";
        }, RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string StyleOf(WeChatTheme theme, string key, string fallback) =>
        theme.Styles.TryGetValue(key, out var style) && !string.IsNullOrWhiteSpace(style) ? style : fallback;

    private static string AppendWidth(string style) =>
        Regex.IsMatch(style, @"(^|;)\s*width\s*:", RegexOptions.IgnoreCase) ? style : $"{style};width:100%";

    private static string StyleBlockquoteParagraphs(string html, WeChatTheme theme) {
        if (!theme.Styles.TryGetValue("blockquote_p", out var style) || string.IsNullOrWhiteSpace(style)) {
            return html;
        }

        return Regex.Replace(html, @"<blockquote(?<attributes>[^>]*)>(?<body>.*?)</blockquote>", match =>
            $"<blockquote{match.Groups["attributes"].Value}>{StyleTag(match.Groups["body"].Value, "p", style)}</blockquote>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string ConvertLists(string html, WeChatTheme theme) {
        if (!theme.Styles.ContainsKey("list_item_row")) return html;

        var wrapperStyle = StyleOf(theme, "list_wrapper", "margin-top:16px;margin-bottom:16px");
        var rowStyle = StyleOf(theme, "list_item_row", "display:flex;margin-bottom:8px;align-items:flex-start");
        var bulletStyle = StyleOf(theme, "list_item_bullet", $"color:{theme.AccentColor};margin-right:8px;font-weight:bold");
        var orderedBulletStyle = StyleOf(theme, "ol_item_bullet", bulletStyle);
        var textStyle = StyleOf(theme, "list_item_text", $"font-size:15px;color:{theme.TextColor};line-height:1.75");
        var listPattern = new Regex(@"<(?<tag>ul|ol)[^>]*>(?<body>.*?)</\k<tag>>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        while (true) {
            Match? innermost = null;
            foreach (Match match in listPattern.Matches(html)) {
                if (listPattern.IsMatch(match.Groups["body"].Value)) continue;
                innermost = match;
            }

            if (innermost == null) break;

            var isOrdered = innermost.Groups["tag"].Value.Equals("ol", StringComparison.OrdinalIgnoreCase);
            var converted = ConvertSingleList(
                innermost.Groups["body"].Value,
                isOrdered,
                wrapperStyle,
                rowStyle,
                isOrdered ? orderedBulletStyle : bulletStyle,
                textStyle);
            html = html.Remove(innermost.Index, innermost.Length).Insert(innermost.Index, converted);
        }

        return html;
    }

    private static string ConvertSingleList(
        string body,
        bool isOrdered,
        string wrapperStyle,
        string rowStyle,
        string bulletStyle,
        string textStyle) {
        var items = Regex.Matches(body, @"<li[^>]*>(?<content>.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var rows = new List<string>();
        var index = 1;
        foreach (Match item in items) {
            var content = item.Groups["content"].Value.Trim();
            content = Regex.Replace(content, @"^<p[^>]*>|</p>$", string.Empty, RegexOptions.IgnoreCase);
            var bullet = isOrdered ? index.ToString() : "•";
            rows.Add($"<section style=\"{rowStyle}\"><span style=\"{bulletStyle}\">{bullet}</span><span style=\"{textStyle}\">{content}</span></section>");
            index++;
        }

        return $"<section style=\"{wrapperStyle}\">{string.Join("", rows)}</section>";
    }

    private static string WrapCards(string html, WeChatTheme theme) {
        if (theme.Card == null) return html;

        var parts = Regex.Split(html, "(?=<h[12]\\b)", RegexOptions.IgnoreCase)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length == 0) return html;

        var card = theme.Card;
        var cardStyle =
            $"max-width:800px;width:100%;padding:25px;box-sizing:border-box;background-color:{card.CardBackground};background-image:{card.Texture};background-size:{card.TextureSize};border:{card.Border};box-shadow:{card.Shadow};border-radius:{card.Radius}";
        return string.Join("", parts.Select(part => $"<section style=\"{cardStyle}\">{part}</section>"));
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
