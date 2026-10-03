using System;
using System.IO;
using System.Net;
using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace StarBlogPublisher.Services;

internal static class MarkdownPreviewService {
    public static Uri? Render(string content, string previewId, Uri? baseUri, bool isDark) {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var previewDirectory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "markdown-preview");
        Directory.CreateDirectory(previewDirectory);
        var previewPath = Path.Combine(previewDirectory, previewId + ".html");
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();
        var baseHref = baseUri?.AbsoluteUri ?? string.Empty;

        var markdown = Markdig.Markdown.Parse(content, pipeline);
        foreach (var block in markdown.Descendants<Block>()) {
            var line = content.AsSpan(0, Math.Clamp(block.Span.Start, 0, content.Length)).Count('\n') + 1;
            var endLine = content.AsSpan(0, Math.Clamp(block.Span.End + 1, 0, content.Length)).Count('\n') + 1;
            var attributes = block.GetAttributes();
            attributes.AddProperty("data-source-line", line.ToString(System.Globalization.CultureInfo.InvariantCulture));
            attributes.AddProperty("data-source-end", endLine.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (block is HeadingBlock)
                attributes.AddProperty("data-outline-line", line.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        var body = Markdig.Markdown.ToHtml(markdown, pipeline);
        var previewThemeClass = isDark
            ? "preview-dark"
            : "preview-light";
        var document = $$"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <base href="{{WebUtility.HtmlEncode(baseHref)}}">
              <style>
                :root { font-family: Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", "Microsoft YaHei", sans-serif; }
                body { box-sizing: border-box; max-width: 920px; margin: 0 auto; padding: 28px 36px 56px; color: #1f2328; background: #fff; font-size: 16px; line-height: 1.72; }
                h1, h2, h3, h4, h5, h6 { line-height: 1.3; margin: 1.45em 0 .65em; color: #111827; }
                h1 { font-size: 2em; border-bottom: 1px solid #d8dee4; padding-bottom: .35em; } h2 { font-size: 1.55em; border-bottom: 1px solid #e5e7eb; padding-bottom: .3em; } h3 { font-size: 1.25em; }
                p, ul, ol, blockquote { margin: 0 0 1em; } li + li { margin-top: .3em; }
                a { color: #0969da; text-decoration: none; } a:hover { text-decoration: underline; }
                code { padding: .16em .36em; border-radius: 5px; background: #f1f3f5; color: #c7254e; font: .9em "Cascadia Code", Consolas, monospace; }
                pre { overflow: auto; padding: 16px 18px; border-radius: 8px; background: #1f2937; color: #e5e7eb; line-height: 1.55; } pre code { padding: 0; background: transparent; color: inherit; }
                blockquote { margin-left: 0; padding: .15em 1em; border-left: 4px solid #d0d7de; color: #57606a; background: #f6f8fa; }
                table { display: block; width: max-content; max-width: 100%; overflow: auto; border-collapse: collapse; margin-bottom: 1em; } th, td { padding: .45em .75em; border: 1px solid #d0d7de; } th { background: #f6f8fa; }
                img { display: block; max-width: 100%; height: auto; margin: 1em 0; border-radius: 6px; }
                hr { height: 1px; border: 0; background: #d8dee4; margin: 2em 0; }
                body.preview-dark { color-scheme: dark; color: #e6edf3; background: #0d1117; }
                body.preview-dark h1, body.preview-dark h2, body.preview-dark h3, body.preview-dark h4, body.preview-dark h5, body.preview-dark h6 { color: #f0f6fc; border-color: #30363d; }
                body.preview-dark a { color: #58a6ff; }
                body.preview-dark code { background: #161b22; color: #ff7b72; }
                body.preview-dark blockquote, body.preview-dark th { color: #b1bac4; background: #161b22; border-color: #3b434b; }
                body.preview-dark th, body.preview-dark td { border-color: #30363d; }
                body.preview-dark hr { background: #30363d; }
              </style>
            </head>
            <body class="{{previewThemeClass}}">{{body}}</body>
            </html>
            """;
        File.WriteAllText(previewPath, document, Encoding.UTF8);
        return new Uri($"{new Uri(previewPath).AbsoluteUri}?v={DateTime.UtcNow.Ticks}");
    }
}
