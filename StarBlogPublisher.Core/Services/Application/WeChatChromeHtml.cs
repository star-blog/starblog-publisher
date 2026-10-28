using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 公众号编辑器复制出来的 HTML Format，以及插到正文前后的开头、文末片段。
/// </summary>
public static class WeChatChromeHtml {
    private const string StartFragmentMarker = "<!--StartFragment-->";
    private const string EndFragmentMarker = "<!--EndFragment-->";

    private static readonly Regex ProfileCardPattern = new(
        @"<(?<tag>mp-common-profile|mpprofile)\b(?<attrs>[^>]*)>(?<body>.*?)</\k<tag>>|<(?<tag>mp-common-profile|mpprofile)\b(?<attrs>[^>]*)/>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ScriptPattern = new(
        @"<script\b[^>]*>[\s\S]*?</script\s*>|<script\b[^>]*/>|<script\b[\s\S]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventHandlerPattern = new(
        @"\son[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JavaScriptUrlPattern = new(
        @"\b(?<attr>href|src)\s*=\s*(?<q>[""'])\s*javascript:[^""']*\k<q>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// HTML Format 的字节是 UTF-8。用系统 ANSI 代码页解码会把中文变成乱码。
    /// </summary>
    public static string DecodeClipboardBytes(byte[] bytes) {
        if (bytes.Length == 0) return string.Empty;

        var length = bytes.Length;
        while (length > 0 && bytes[length - 1] == 0) length--;
        if (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
            return Encoding.Unicode.GetString(bytes, 2, length - 2);
        }

        if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
            return Encoding.UTF8.GetString(bytes, 3, length - 3);
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    /// <summary>取出 CF_HTML 里 StartFragment 与 EndFragment 之间的片段。</summary>
    public static string? TryExtractFragment(string? clipboardHtml) {
        if (string.IsNullOrWhiteSpace(clipboardHtml)) return null;

        var start = clipboardHtml.IndexOf(StartFragmentMarker, StringComparison.Ordinal);
        var end = clipboardHtml.IndexOf(EndFragmentMarker, StringComparison.Ordinal);
        if (start < 0 || end <= start) return null;

        var fragment = clipboardHtml.Substring(
            start + StartFragmentMarker.Length,
            end - (start + StartFragmentMarker.Length)).Trim();
        return fragment.Length == 0 ? null : fragment;
    }

    /// <summary>去掉脚本和事件，保留公众号名片标签与内联样式。</summary>
    public static string Sanitize(string? html) {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var result = ScriptPattern.Replace(html, string.Empty);
        result = EventHandlerPattern.Replace(result, string.Empty);
        result = JavaScriptUrlPattern.Replace(result, "${attr}=${q}#${q}");
        return result.Trim();
    }

    public static string Summarize(string? html) {
        if (string.IsNullOrWhiteSpace(html)) return "未设置";

        var cards = ProfileCardPattern.Matches(html).Count;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length > 42) text = text[..42] + "…";
        if (text.Length == 0) text = cards > 0 ? "公众号名片" : "已设置 HTML";

        var size = Encoding.UTF8.GetByteCount(html);
        var sizeText = size < 1024 ? $"{size} 字节" : $"{size / 1024.0:0.#} KB";
        var cardText = cards > 0 ? $" · {cards} 张公众号名片" : "";
        return $"{text}{cardText} · {sizeText}";
    }

    /// <summary>
    /// 预览画不出可点击的名片。复制和上传仍用原始标签。
    /// </summary>
    public static string ToPreviewHtml(string html) {
        if (string.IsNullOrEmpty(html) || !ProfileCardPattern.IsMatch(html)) return html;

        return ProfileCardPattern.Replace(html, match => {
            var name = Attribute(match.Groups["attrs"].Value, "data-nickname");
            var signature = Attribute(match.Groups["attrs"].Value, "data-signature");
            if (string.IsNullOrWhiteSpace(name)) name = "公众号";
            var title = WebUtility.HtmlEncode(name);
            var detail = string.IsNullOrWhiteSpace(signature)
                ? ""
                : $"<p style=\"margin:0;font-size:13px;line-height:1.6;color:#6b7280;\">{WebUtility.HtmlEncode(signature)}</p>";
            return "<section style=\"margin:18px 0;padding:14px 16px;border:1px solid #e5e7eb;border-radius:8px;background:#f7f7f7;\">"
                   + $"<p style=\"margin:0 0 4px;font-size:15px;font-weight:700;color:#576b95;\">公众号名片 · {title}</p>"
                   + detail
                   + "</section>";
        });
    }

    public static bool ContainsIndexInProfileCard(string html, int index) {
        if (string.IsNullOrEmpty(html) || index < 0) return false;
        foreach (Match match in ProfileCardPattern.Matches(html)) {
            if (index >= match.Index && index < match.Index + match.Length) return true;
        }

        return false;
    }

    private static string Attribute(string attributes, string name) {
        var match = Regex.Match(
            attributes,
            $@"\b{name}\s*=\s*(?<q>[""'])(?<value>.*?)\k<q>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : string.Empty;
    }
}
