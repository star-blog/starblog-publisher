using System;
using System.IO;
using System.Linq;
using CodeLab.Share.Extensions;
using Markdig;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using StarBlogPublisher.Models;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Services;

public class MarkdownProcessor {
    private readonly BlogPost _post;
    private readonly string _importPath;
    private readonly string _assetsPath;

    public MarkdownProcessor(string importPath, string assetsPath, BlogPost post) {
        _post = post;
        _assetsPath = assetsPath;
        _importPath = importPath;
    }

    /// <summary>
    /// Markdown内容解析，复制图片 & 替换图片链接
    /// </summary>
    /// <returns></returns>
    public string MarkdownParse() {
        if (_post.Content == null) {
            return string.Empty;
        }

        var document = Markdig.Markdown.Parse(_post.Content);

        foreach (var node in document.AsEnumerable()) {
            if (node is not ParagraphBlock { Inline: { } } paragraphBlock) continue;
            foreach (var inline in paragraphBlock.Inline) {
                if (inline is not LinkInline { IsImage: true } linkInline) continue;


                if (string.IsNullOrWhiteSpace(linkInline.Url)) continue;
                var imgUrl = Uri.UnescapeDataString(linkInline.Url);
                if (imgUrl.StartsWith("http")) continue;

                // 路径处理
                var imgPath = Path.Combine(_importPath, _post.Path ?? "", imgUrl);
                var imgFilename = Path.GetFileName(imgUrl);
                var destDir = Path.Combine(_assetsPath, _post.Id);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                var destPath = Path.Combine(destDir, imgFilename);
                if (File.Exists(destPath)) {
                    // 图片重名处理
                    var imgId = GuidUtils.GuidTo16String();
                    imgFilename =
                        $"{Path.GetFileNameWithoutExtension(imgFilename)}-{imgId}.{Path.GetExtension(imgFilename)}";
                    destPath = Path.Combine(destDir, imgFilename);
                }

                // 替换图片链接
                linkInline.Url = imgFilename;
                // 复制图片
                File.Copy(imgPath, destPath);

                Console.WriteLine($"复制 {imgPath} 到 {destPath}");
            }
        }


        using var writer = new StringWriter();
        var render = new NormalizeRenderer(writer);
        render.Render(document);
        return writer.ToString();
    }

    /// <summary>
    /// 从文章正文提取前 <paramref name="length"/> 字的梗概
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public string GetSummary(int length) {
        return _post.Content == null
            ? string.Empty
            : Markdig.Markdown.ToPlainText(_post.Content).Limit(length);
    }
}